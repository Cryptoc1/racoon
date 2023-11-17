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
    private RCONConnection? _connection;

    // When generating the packet ID, use a never-been-used (for automatic packets) ID.
    private volatile int _packetId = 1;

    // NOTE: Use (1,1) to lock connection per command (only a single command may execute against the connection at a time)
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private readonly ConcurrentDictionary<int, RCONResponse> _responseByPacketId = new();
    private readonly IPEndPoint _endpoint = endpoint;
    private readonly RCONOptions _options = options ?? new();
    private readonly string _password = password;
    // private readonly Dictionary<int, StringBuilder> _responseByPacketId = [];

    /// <summary> Indicates whether the underlying socket is connected. </summary>
    public RCONConnectionState ConnectionState => _authenticationCompletion?.Task.Status switch
    {
        null => RCONConnectionState.Disconnected,
        < TaskStatus.RanToCompletion => _connection?.Connected is true ? RCONConnectionState.Connected : RCONConnectionState.Connecting,
        TaskStatus.RanToCompletion => _connection?.Connected is true ? _authenticationCompletion.Task.Result is true ? RCONConnectionState.Authenticated : RCONConnectionState.Connected : RCONConnectionState.Disconnected,
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
        if (_authenticationCompletion is not null)
        {
            await Authenticated().ConfigureAwait(false);
            return;
        }

        _authenticationCompletion = new();
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
            ReceiveTimeout = (int)_options.Timeout.TotalMilliseconds,
            SendTimeout = (int)_options.Timeout.TotalMilliseconds,
        };

        try
        {
            await socket.ConnectAsync(_endpoint).ConfigureAwait(false);
        }
        catch (SocketException exception)
        {
            socket.Dispose();
            socket = null;

            // NOTE: reset completion, allow callers to implement retry logic
            _authenticationCompletion = null;
            throw new RCONException("An attempt to connect to with the host failed.", exception);
        }

        using (var cancellation = new CancellationTokenSource())
        using (cancellation.Token.Register(_authenticationCompletion.SetCanceled))
        {
            _connection = new RCONConnection(socket, OnDisconnected, OnPacketReceived);
            if (_options.Timeout != TimeSpan.Zero)
            {
                cancellation.CancelAfter(_options.Timeout);
            }

            await Authenticated(async () =>
            {
                await _connection.SendAsync(new(0, RCONPacketType.Auth, _password), cancellation.Token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        async Task Authenticated(Func<Task>? authenticate = null)
        {
            try
            {
                if (authenticate is not null)
                {
                    await authenticate().ConfigureAwait(false);
                }

                if (!await _authenticationCompletion.Task) throw new RCONAuthenticationException();
            }
            catch (TaskCanceledException exception)
            {
                _connection?.Dispose();
                _connection = null;

                _authenticationCompletion = null;
                throw new RCONAuthenticationException($"Failed to authenticate with the host within the configured timeout of '{_options.Timeout}'.", exception);
            }
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

    private void OnDisconnected()
    {
        _connection!.Dispose();
        _connection = null;

        _authenticationCompletion = null;
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
                _authenticationCompletion?.SetResult(false);
            }

            // inform success
            _authenticationCompletion?.SetResult(true);
            return;
        }

        if (_responseByPacketId.TryRemove(packet.Id, out var response))
        {
            if (!_options.UseKoraktorMethod)
            {
                response.Complete(packet.Body);
                return;
            }

            // NOTE: Koraktor method: if an existing response body exists, and this packet indicates completion, complete the request
            if (packet.Body is "" or "\0\u0001\0\0")
            {
                response.Complete(packet.Body);
                return;
            }

            response.Append(packet.Body);
            _responseByPacketId[packet.Id] = response;
        }
    }

    /// <summary> Send a RCON Command. </summary>
    /// <typeparam name="T"> The type of command response. </typeparam>
    /// <param name="command"> The command text to be sent. </param>
    /// <exception cref="RCONCommandException"> An error occurred while sending the command. </exception>
    public async Task<T> SendCommandAsync<T>(string command, CancellationToken cancellation = default)
        where T : class, IParseable<T>
    {
        var response = await SendCommandAsync(command, cancellation).ConfigureAwait(false);

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
    public async Task<string> SendCommandAsync(string command, CancellationToken cancellation = default)
    {
        if (_connection is null) throw new RCONCommandException($"The connection has not been established. Ensure '{nameof(ConnectAsync)}' is called before '{nameof(SendCommandAsync)}'.", command);

        await _commandLock.WaitAsync(cancellation).ConfigureAwait(false);

        var packet = new RCONPacket(
            Interlocked.Increment(ref _packetId),
            RCONPacketType.ExecCommand,
            command);

        var response = _responseByPacketId[packet.Id] = new RCONResponse();
        try
        {
            using (var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (cancellationSource.Token.Register(response.Cancel))
            {
                await _connection.SendAsync(packet, cancellationSource.Token).ConfigureAwait(false);
                if (packet.Type is RCONPacketType.ExecCommand && _options.UseKoraktorMethod)
                {
                    // NOTE: Koraktor method: send an additional empty packet; the server will respond with an empty packet, indicating completion of the request
                    packet = new(packet.Id, RCONPacketType.Response, string.Empty);
                    await _connection.SendAsync(packet, cancellationSource.Token).ConfigureAwait(false);
                }

                if (_options.Timeout != TimeSpan.Zero)
                {
                    cancellationSource.CancelAfter(_options.Timeout);
                }

                var completed = await Task.WhenAny(
                    response.Completed,
                    _connection.Closed()).ConfigureAwait(false);

                if (completed == response.Completed)
                {
                    try
                    {
                        return await response.Completed.ConfigureAwait(false);
                    }
                    catch (TaskCanceledException exception)
                    {
                        throw new RCONCommandException($"Failed to execute command within the configured timeout of '{_options.Timeout}'.", command, exception);
                    }
                }

                throw RCONCommandException.Failed(command, completed.Exception);
            }
        }
        finally
        {
            if (_responseByPacketId.TryRemove(packet.Id, out response))
            {
                response.Cancel();
            }

            _commandLock.Release();
        }
    }

    private sealed class RCONConnection : IDisposable
    {
        private readonly Pipe _pipe;
        public readonly Socket _socket;

        public bool Connected => _socket.Connected;
        public Task Reading { get; }
        public Task Writing { get; }

        public RCONConnection(Socket socket, Action onClosed, Action<RCONPacket> onPacket)
        {
            _pipe = new();
            _socket = socket;

            Writing = Write(_pipe.Writer, _socket, onClosed);
            Reading = Read(_pipe.Reader, onPacket);
        }

        public async Task Closed()
        {
            _ = await Task.WhenAny(Reading, Writing).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Dispose();
        }

        private static async Task Write(PipeWriter writer, Socket socket, Action onClosed)
        {
            while (socket.Connected)
            {
                var buffer = writer.GetMemory(RCONPacketDefaults.MinPacketSize + sizeof(int));
                try
                {
#if NETSTANDARD2_1_OR_GREATER
                    int read = await socket.ReceiveAsync(
                        buffer,
                        SocketFlags.None,
                        CancellationToken.None).ConfigureAwait(false);
#else
                    int read = await SocketTaskExtensions.ReceiveAsync(
                        socket,
                        BufferHelper.AsSegment(buffer),
                        SocketFlags.None).ConfigureAwait(false);
#endif
                    if (read is 0)
                    {
                        break;
                    }

                    writer.Advance(read);
                }
                catch
                {
                    break;
                }

                var result = await writer.FlushAsync().ConfigureAwait(false);
                if (result.IsCompleted)
                {
                    break;
                }
            }

            await writer.CompleteAsync().ConfigureAwait(false);
            onClosed();
        }

        private static async Task Read(PipeReader reader, Action<RCONPacket> onPacket)
        {
            while (true)
            {
                var result = await reader.ReadAsync()
                    .ConfigureAwait(false);

                var buffer = result.Buffer;
                var start = buffer.Start;

                if (TryReadPacketSize(buffer, out var size) && buffer.Length >= (size += sizeof(int)))
                {
                    var end = buffer.GetPosition(size, start);
                    if (RCONPacket.TryFromBytes(
                        buffer.Slice(start, end).ToArray(),
                        out var packet))
                    {
                        onPacket(packet);
                    }

                    reader.AdvanceTo(end);
                }
                else
                {
                    reader.AdvanceTo(start, buffer.End);
                }

                if ((buffer.IsEmpty && result.IsCompleted) || result.IsCompleted) break;
            }

            await reader.CompleteAsync().ConfigureAwait(false);

            static bool TryReadPacketSize(in ReadOnlySequence<byte> buffer, out int size)
            {
                if (buffer.Length >= sizeof(int))
                {
                    var data = buffer.Slice(buffer.Start, sizeof(int)).ToArray();

#if NETSTANDARD2_1_OR_GREATER
                    size = BitConverter.ToInt32(data);
#else
                    size = BitConverter.ToInt32(data, 0);
#endif
                    return true;
                }

                size = default;
                return false;
            }
        }

        public async ValueTask SendAsync(RCONPacket packet, CancellationToken cancellation = default)
        {
            if (!_socket.Connected) throw new RCONException($"The underlying socket is no longer connected.");

            using var rented = MemoryPool<byte>.Shared.Rent(packet.Size + sizeof(int));

            var written = packet.GetBytes(rented.Memory.Span);
            if (written < packet.Size + sizeof(int))
            {
                return;
            }

#if NETSTANDARD2_1_OR_GREATER
            await _socket.SendAsync(
                rented.Memory[..written],
                SocketFlags.None,
                cancellation).ConfigureAwait(false);
#else
            await _socket.SendAsync(
                BufferHelper.AsSegment(rented.Memory[..written]),
                SocketFlags.None).ConfigureAwait(false);
#endif
        }
    }

    private sealed class RCONResponse
    {
        private StringBuilder? body;
        private readonly TaskCompletionSource<string> completion = new();

        public Task<string> Completed => completion.Task;

        public void Append(string content)
        {
            if (body is null)
            {
                body = new(content);
                return;
            }

            body.Append(content);
        }

        public void Cancel()
        {
            if (completion.TrySetCanceled())
            {
                Reset();
            }
        }

        public void Complete(string content)
        {
            var value = body?.Append(content).ToString() ?? content;
            Reset();

            completion.SetResult(value);
        }

        private void Reset()
        {
            if (body is not null)
            {
                body.Clear();
                body = null;
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
