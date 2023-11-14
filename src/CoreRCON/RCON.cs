using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CoreRCON.Internal;
using CoreRCON.PacketFormats;
using CoreRCON.Parsers;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON;

/// <summary> Create a RCON client. </summary>
/// <param name="endpoint"> The server to connect to. </param>
/// <param name="password"> The password to authenticate with. </param>
/// <param name="options"> The options to use for connecting to the host. </param>
public sealed class RCON(IPEndPoint endpoint, string password, RCONOptions? options = null) : IDisposable
{
    // Allows us to keep track of when authentication succeeds, so we can block Connect from returning until it does.
    private TaskCompletionSource<bool>? _authenticationCompletion;

    // When generating the packet ID, use a never-been-used (for automatic packets) ID.
    private int _packetId = 1;

    // NOTE: Use (1,1) to lock connection per command (only a single command may execute against the connection at a time)
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _completionByPacketId = new();
    private RCONConnection? _connection;
    private readonly IPEndPoint _endpoint = endpoint;
    private readonly RCONOptions _options = options ?? new();
    private readonly string _password = password;
    private readonly Dictionary<int, StringBuilder> _responseByPacketId = [];

    /// <summary> Indicates whether the underlying socket is connected. </summary>
    public RCONConnectionState ConnectionState => _authenticationCompletion?.Task.Status switch
    {
        null => RCONConnectionState.Disconnected,
        < TaskStatus.RanToCompletion => _connection is null ? RCONConnectionState.Connecting : RCONConnectionState.Connected,
        TaskStatus.RanToCompletion => _authenticationCompletion.Task.Result is true ? RCONConnectionState.Authenticated : RCONConnectionState.Connected,
        _ => RCONConnectionState.Disconnected,
    };

    /// <summary> Fired if connection is lost. </summary>
    public event Action? Disconnected;

    /// <summary> Fired when an RCON package has been received. </summary>
    public event Action<RCONPacket>? PacketReceived;

    /// <summary> Create an instance of an RCON client. </summary>
    public RCON(IPAddress host, ushort port, string password, RCONOptions? options = null)
        : this(new IPEndPoint(host, port), password, options)
    {
    }

    /// <summary> Connect to the configured RCON host. </summary>
    /// <returns> A task which completes when authentication with the host succeeds. </returns>
    /// <exception cref="RCONException"> An unexpected error occurred attempting to connect to the server, caller may check the InnerException for further details. </exception>
    /// <exception cref="RCONAuthenticationException"> Connection with the server was successful, but authentication failed. </exception>
    public async Task ConnectAsync()
    {
        if (_authenticationCompletion is null)
        {
            _authenticationCompletion = new();
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
                ReceiveTimeout = (int)_options.Timeout.TotalMilliseconds,
                SendTimeout = (int)_options.Timeout.TotalMilliseconds,
            };

            try
            {
                await socket.ConnectAsync(_endpoint)
                    .ConfigureAwait(false);
            }
            catch (SocketException exception)
            {
                socket.Dispose();
                socket = null;

                // NOTE: reset completion, allow callers to implement retry logic
                _authenticationCompletion = null;
                throw new RCONException("An attempt to connect to with the host failed.", exception);
            }

            _connection = new RCONConnection(socket, OnDisconnected, OnPacketReceived);
            await _connection.SendAsync(new(0, RCONPacketType.Auth, _password));
        }

        bool authenticated;
        try
        {
            authenticated = await _authenticationCompletion.Task
                .WaitAsync(_options.Timeout)
                .ConfigureAwait(false);
        }
        catch (TimeoutException timeout)
        {
            throw new RCONException("A timeout occurred while authenticating with the host.", timeout);
        }
        catch (Exception exception)
        {
            throw new RCONException("An unexpected error occurred while connecting to the host.", exception);
        }

        if (!authenticated)
        {
            // authentication failed
            throw new RCONAuthenticationException();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_connection is not null)
        {
            _connection.Dispose();
            _connection = null;
        }

        _commandLock.Dispose();
    }

    private void EnsureConnected()
    {
        if (_connection is null)
        {
            throw new RCONException($"The connection has not been created. Ensure '{nameof(ConnectAsync)}' is invoked prior to {nameof(SendCommandAsync)}.");
        }
    }

    private void OnDisconnected()
    {
        _authenticationCompletion = null;
        _connection!.Dispose();
        _connection = null;

        Disconnected?.Invoke();
    }

    private void OnPacketReceived(RCONPacket packet)
    {
        PacketReceived?.Invoke(packet);
        if (packet.Type is RCONPacketType.AuthResponse)
        {
            // Failed auth responses return with an ID of -1
            if (packet.Id is -1)
            {
                _authenticationCompletion!.SetResult(false);
            }

            // inform success
            _authenticationCompletion!.SetResult(true);
            return;
        }

        if (_completionByPacketId.TryRemove(packet.Id, out var completion))
        {
            if (!_options.UseKoraktorMethod)
            {
                completion.SetResult(packet.Body);
                return;
            }

            // NOTE: Koraktor method: if an existing response body exists, and this packet indicates completion, complete the request
            if (_responseByPacketId.TryGetValue(packet.Id, out var body) && (packet.Body is "" or "\0\u0001\0\0"))
            {
                _responseByPacketId.Remove(packet.Id);
                completion.SetResult(body.ToString());

                return;
            }

            // NOTE: Koraktor method: this packet did not indicate completion, aggregate body and continue recieving packets
            _responseByPacketId[packet.Id] = (body ?? new()).Append(packet.Body);
            _completionByPacketId[packet.Id] = completion;
        }
    }

    /// <summary> Send a RCON Command. </summary>
    /// <typeparam name="T"> The type of command response. </typeparam>
    /// <param name="command"> The command text to be sent. </param>
    /// <exception cref="RCONCommandException"> An error occurred while sending the command. </exception>
    public async Task<T> SendCommandAsync<T>(string command)
        where T : class, IParseable<T>
    {
        var response = await SendCommandAsync(command).ConfigureAwait(false);

        var parser = _options.Parsers.Get<T>();
        if (!parser.IsMatch(response))
        {
            throw RCONCommandException.Failed("The command response could not be parsed.");
        }

        return parser.Parse(response);
    }

    /// <summary> Send a RCON Command. </summary>
    /// <param name="command"> The command text to be sent. </param>
    /// <exception cref="RCONCommandException"> An error occurred while sending the command. </exception>
    public async Task<string> SendCommandAsync(string command)
    {
        EnsureConnected();

        // lock client for the execution of this command
        await _commandLock.WaitAsync().ConfigureAwait(false);

        var packet = new RCONPacket(
            Interlocked.Increment(ref _packetId),
            RCONPacketType.ExecCommand,
            command);

        /*
          TaskCompletionSource *could* be initialized with TaskCreationOptions.RunContinuationsAsynchronously, but this library is designed to be able to run without its own thread.
          See: https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md#always-create-taskcompletionsourcet-with-taskcreationoptionsruncontinuationsasynchronously
        */
        var completion = _completionByPacketId[packet.Id] = new TaskCompletionSource<string>();

        Task completed;
        try
        {
            await SendPacketAsync(packet).ConfigureAwait(false);
            completed = await Task.WhenAny(completion.Task, _connection!.Closed)
                .WaitAsync(_options.Timeout)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw RCONCommandException.Timeout(command, exception);
        }
        finally
        {
            if (_completionByPacketId.TryRemove(packet.Id, out _))
            {
                _responseByPacketId.Remove(packet.Id);
            }

            _commandLock.Release();
        }

        if (completed == completion.Task)
        {
            return await completion.Task;
        }

        throw RCONCommandException.Failed(command, completed.Exception);
    }

    private async Task SendPacketAsync(RCONPacket packet)
    {
        EnsureConnected();

        await _connection!.SendAsync(packet);
        if (packet.Type is RCONPacketType.ExecCommand && _options.UseKoraktorMethod)
        {
            // NOTE: Koraktor method: send an additional empty packet; the server will respond with an empty packet, indicating completion of the request
            packet = new(packet.Id, RCONPacketType.Response, string.Empty);
            await _connection.SendAsync(packet);
        }
    }

    private sealed class RCONConnection : IDisposable
    {
        private readonly ArrayPool<byte> _arrayPool;
        private readonly Pipe _pipe;
        private readonly Socket _socket;

        /// <summary> A task which completes when the underlying pipe stops receiving data from the underlying socket. </summary>
        public Task Closed { get; }

        public RCONConnection(Socket socket, Action? onClosed, Action<RCONPacket> onPacket)
        {
            _arrayPool = ArrayPool<byte>.Shared;
            _pipe = new();
            _socket = socket;

            Closed = Task.WhenAny(
                Read(_pipe.Writer, socket, onClosed),
                Receive(_pipe.Reader, onPacket));
        }

        public void Dispose()
        {
            if (_socket.Connected) _socket.Shutdown(SocketShutdown.Both);
            _socket.Dispose();
        }

        private static async Task Read(PipeWriter writer, Socket socket, Action? onClosed)
        {
            try
            {
                while (socket.Connected)
                {
                    // read from socket
                    int read = await socket.ReceiveAsync(
                        writer.GetMemory(RCONPacketDefaults.MinPacketSize + sizeof(int)),
                        SocketFlags.None)
                        .ConfigureAwait(false);

                    if (read is 0)
                    {
                        break;
                    }

                    // inform writer socket was read
                    writer.Advance(read);

                    var result = await writer.FlushAsync().ConfigureAwait(false);
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            finally
            {
                await writer.FlushAsync().ConfigureAwait(false);
                await writer.CompleteAsync().ConfigureAwait(false);

                onClosed?.Invoke();
            }
        }

        private static async Task Receive(PipeReader reader, Action<RCONPacket> onPacket)
        {
            try
            {
                while (true)
                {
                    var result = await reader.ReadAsync().ConfigureAwait(false);
                    var buffer = result.Buffer;
                    var start = buffer.Start;

                    if (buffer.Length < RCONPacketDefaults.MinPacketSize + sizeof(int))
                    {
                        if (result.IsCompleted)
                        {
                            break;
                        }

                        reader.AdvanceTo(start, buffer.End);
                        continue;
                    }

                    int length = BitConverter.ToInt32(buffer.Slice(start, sizeof(int)).ToArray(), 0) + sizeof(int);
                    if (buffer.Length >= length)
                    {
                        var end = buffer.GetPosition(length, start);

                        var bytes = buffer.Slice(start, end).ToArray();
                        if (RCONPacket.TryFromBytes(bytes, out var packet))
                        {
                            onPacket(packet);
                        }

                        reader.AdvanceTo(end);
                    }
                    else
                    {
                        reader.AdvanceTo(start, buffer.End);
                    }

                    if (buffer.IsEmpty && result.IsCompleted)
                    {
                        // escape
                        break;
                    }
                }
            }
            finally
            {
                await reader.CompleteAsync().ConfigureAwait(false);
            }
        }

        public async Task SendAsync(RCONPacket packet)
        {
            var buffer = _arrayPool.Rent(packet.Size + sizeof(int));

            try
            {
                var written = packet.GetBytes(buffer);
                var segment = new ArraySegment<byte>(buffer, 0, written);

                await _socket.SendAsync(segment, SocketFlags.None)
                    .ConfigureAwait(false);
            }
            finally
            {
                _arrayPool.Return(buffer);
            }
        }
    }
}

public enum RCONConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Authenticated
}

/// <summary> Represents options for an RCON client. </summary>
public sealed class RCONOptions
{
    /// <summary> A <see cref="ParserPool"/> to be used for parsing the responses of commands.  </summary>
    public ParserPool Parsers { get; set; } = ParserPool.Shared;

    /// <summary> Whether the 'Koraktor' method of handling multi-packet responses should be used. </summary>
    public bool UseKoraktorMethod { get; set; }

    /// <summary> A timeout to be used when connecting and executing commands. </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public RCONOptions(bool useKoraktorMethod = false)
    {
        UseKoraktorMethod = useKoraktorMethod;
    }

    public RCONOptions(TimeSpan timeout, bool useKoraktorMethod = false)
    {
        Timeout = timeout;
        UseKoraktorMethod = useKoraktorMethod;
    }
}
