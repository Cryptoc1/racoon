using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CoreRCON.PacketFormats;
using CoreRCON.Parsers;

namespace CoreRCON;

/// <summary> Create an instance of an RCON client. </summary>
/// <param name="endpoint"> The server to connect to. </param>
/// <param name="password"> The password to authenticate with. </param>
/// <param name="timeout"> A timeout to be used for connecting & executing commands. </param>
/// <param name="sourceMultiPacketSupport"> Enable support for 'Koraktor' handling of packets for large responses. </param>
/// <param name="logger"> An (optional) logger for logging internal connection details. </param>
public sealed class RCON(IPEndPoint endpoint, string password, RCONOptions? options = null) : IDisposable
{
    // Allows us to keep track of when authentication succeeds, so we can block Connect from returning until it does.
    private TaskCompletionSource<bool>? _authenticationCompletion;

    // When generating the packet ID, use a never-been-used (for automatic packets) ID.
    private int _packetId = 1;
    private Socket? _socket;
    private Task? _socketReader;
    private Task? _socketWriter;

    // Map of pending command references.  These are called when a command with the matching Id (key) is received.  Commands are called only once.
    private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _completionByPacketId = new();
    private readonly Dictionary<int, StringBuilder> _responseByPacketId = [];

    private readonly IPEndPoint _endpoint = endpoint;
    private readonly RCONOptions _options = options ?? new();
    private readonly string _password = password;

    // NOTE: Use (1,1) to lock connection per command (only a single command may execute against the connection at a time)
    private readonly SemaphoreSlim _commandLock = new(1, 1);

    /// <summary> Indicates whether the underlying socket is connected. </summary>
    public RCONConnectionState ConnectionState => _authenticationCompletion?.Task.Status switch
    {
        null => RCONConnectionState.Disconnected,
        < TaskStatus.RanToCompletion => _socket?.Connected is true ? RCONConnectionState.Connecting : RCONConnectionState.Connected,
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

    /// <summary> Connect to the RCON server. </summary>
    /// <returns> A task which completes after successful authentication with the host. </returns>
    /// <exception cref="RCONException"> An unexpected error occurred attempting to connec to the server, catchers should check the InnerException for details. </exception>
    /// <exception cref="RCONAuthenticationException"> Connection with the server was successful, but authentication failed. </exception>
    public async Task ConnectAsync()
    {
        if (_authenticationCompletion is null)
        {
            _authenticationCompletion = new TaskCompletionSource<bool>();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveTimeout = (int)_options.Timeout.TotalMilliseconds,
                SendTimeout = (int)_options.Timeout.TotalMilliseconds,
                NoDelay = true
            };

            try
            {
                await _socket.ConnectAsync(_endpoint)
                    .ConfigureAwait(false);
            }
            catch (SocketException exception)
            {
                _socket.Dispose();
                _socket = null;

                // NOTE: reset completion, allow callers to implement retry logic
                _authenticationCompletion = null;
                throw new RCONException("An error occurred while attempting to connect to with the host.", exception);
            }

            var pipe = new Pipe();
            _socketWriter = FillPipeAsync(pipe.Writer);
            _socketReader = ReadPipeAsync(pipe.Reader);

#if DEBUG
            _socketReader = _socketReader.ContinueWith(Log);
            _socketWriter = _socketWriter.ContinueWith(Log);

            static void Log(Task task) => System.Diagnostics.Debug.WriteLine("pipe exited!");
#endif

            await SendPacketAsync(new(0, RCONPacketType.Auth, _password)).ConfigureAwait(false);
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

    /// <inheritdoc/>
    public void Dispose()
    {
        _commandLock.Dispose();
        if (_socket is not null)
        {
            if (_socket.Connected)
            {
                _socket.Shutdown(SocketShutdown.Both);
            }

            _socket.Dispose();
        }
    }

    /// <summary> Fill the given <paramref name="writer"/> with data from <see cref="_socket"/>. </summary>
    private async Task FillPipeAsync(PipeWriter writer)
    {
        try
        {
            while (_socket!.Connected)
            {
                // read from socket
                int read = await _socket.ReceiveAsync(
                    writer.GetMemory(RCONPacketDefaults.MinPacketSize),
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

            Disconnected?.Invoke();
        }
    }

    /// <summary> Merges RCON packet bodies and resolves the waiting task with the full body when full response has been recived. </summary>
    private void OnPacketReceived(RCONPacket packet)
    {
        PacketReceived?.Invoke(packet);
        if (_completionByPacketId.TryRemove(packet.Id, out var completion))
        {
            if (!_options.IsMultiPacketSupported)
            {
                completion.SetResult(packet.Body);
                return;
            }

            if (_responseByPacketId.TryGetValue(packet.Id, out var body) && (packet.Body is "" or "\0\u0001\0\0"))
            {
                _responseByPacketId.Remove(packet.Id);
                completion.SetResult(body.ToString());

                return;
            }

            _responseByPacketId[packet.Id] = (body ?? new()).Append(packet.Body);
            _completionByPacketId[packet.Id] = completion;
        }
    }

    /// <summary> Read & serialize <see cref="RCONPacket"/>s from the given <paramref name="reader"/>. </summary>
    private async Task ReadPipeAsync(PipeReader reader)
    {
        try
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync().ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = result.Buffer;
                SequencePosition start = buffer.Start;

                if (buffer.Length < 4)
                {
                    if (result.IsCompleted)
                    {
                        break;
                    }

                    // header not fully received
                    reader.AdvanceTo(start, buffer.End);
                    continue;
                }

                int size = BitConverter.ToInt32(buffer.Slice(start, 4).ToArray(), 0);
                if (buffer.Length >= size + 4)
                {
                    SequencePosition end = buffer.GetPosition(size + 4, start);
                    byte[] bytes = buffer.Slice(start, end).ToArray();

                    var packet = RCONPacket.FromBytes(bytes);
                    if (packet.Type is RCONPacketType.AuthResponse)
                    {
                        // Failed auth responses return with an ID of -1
                        if (packet.Id is -1)
                        {
                            _authenticationCompletion!.SetResult(false);
                        }

                        // inform success
                        _authenticationCompletion!.SetResult(true);
                    }

                    // Forward rcon packet to handler
                    OnPacketReceived(packet);

                    reader.AdvanceTo(end);
                }
                else
                {
                    reader.AdvanceTo(start, buffer.End);
                }

                // Tell the PipeReader how much of the buffer we have consumed

                // Stop reading if there's no more data coming
                if (buffer.IsEmpty && result.IsCompleted)
                {
                    // escape
                    break;
                }
            }
        }
        finally
        {
            // Mark the PipeReader as complete
            await reader.CompleteAsync().ConfigureAwait(false);
        }
    }

    /// <summary> Send a command to the server. </summary>
    /// <typeparam name="T"> The type of command response. </typeparam>
    /// <param name="command"> The command text to be sent. </param>
    /// <exception cref="RCONCommandException"> An error occurred while sending the command. </exception>
    public async Task<T> SendCommandAsync<T>(string command)
        where T : class, IParseable<T>
    {
        string response = await SendCommandAsync(command).ConfigureAwait(false);

        var parser = ParserPool.Shared.Get<T>();
        if (!parser.IsMatch(response))
        {
            throw RCONCommandException.Failed("The command response could not be parsed.");
        }

        return parser.Parse(response);
    }

    /// <summary> Send a command to the server. </summary>
    /// <param name="command"> The command text to be sent. </param>
    /// <exception cref="RCONCommandException"> An error occurred while sending the command. </exception>
    public async Task<string> SendCommandAsync(string command)
    {
        // lock command execution
        await _commandLock.WaitAsync();

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
            completed = await Task.WhenAny(completion.Task, _socketWriter, _socketReader)
                .WaitAsync(_options.Timeout)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw RCONCommandException.Timeout(command, exception);
        }
        finally
        {
            _commandLock.Release();
            _completionByPacketId.TryRemove(packet.Id, out _);
            _responseByPacketId.Remove(packet.Id);
        }

        if (completed == completion.Task)
        {
            return await completion.Task;
        }

        throw RCONCommandException.Failed(command, completed.Exception);
    }

    /// <summary> Send a packet over the socket connection. </summary>
    /// <param name="packet"> Packet to send, which will be serialized. </param>
    /// <exception cref="RCONException"> Not Connected. </exception>
    private async Task SendPacketAsync(RCONPacket packet)
    {
        if (_socket?.Connected is not true)
        {
            throw new RCONException($"The connection has not been opened. Ensure {nameof(ConnectAsync)} was called before {nameof(SendCommandAsync)}.");
        }

        var segment = new ArraySegment<byte>(packet.ToBytes());
        await _socket.SendAsync(segment, SocketFlags.None)
            .ConfigureAwait(false);

        if (packet.Type is RCONPacketType.ExecCommand && _options.IsMultiPacketSupported)
        {
            // Send an extra packet to find end of large packets
            packet = new(packet.Id, RCONPacketType.Response, "");
            segment = new(packet.ToBytes());

            await _socket.SendAsync(segment, SocketFlags.None)
                .ConfigureAwait(false);
        }
    }
}

public sealed class RCONOptions
{
    public bool IsMultiPacketSupported { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    public RCONOptions(bool isMultiPacketSupported = false)
    {
        IsMultiPacketSupported = isMultiPacketSupported;
    }

    public RCONOptions(TimeSpan timeout, bool isMultiPacketSupported = false)
    {
        IsMultiPacketSupported = isMultiPacketSupported;
        Timeout = timeout;
    }
}
