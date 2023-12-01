using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CoreRCON.PacketFormats;
using CoreRCON.Parsers;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON;

/// <summary> Create a RCON client. </summary>
/// <param name="endpoint"> The server to connect to. </param>
/// <param name="password"> The password to authenticate with. </param>
/// <param name="options"> The options to use for connecting to the host. </param>
public sealed class RCONClient(IPEndPoint endpoint, string password, RCONClientOptions? options = null) : IDisposable
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
    private readonly RCONClientOptions _options = options ?? new();
    private readonly string _password = password;

    /// <summary> Indicates the current state of the underlying connection. </summary>
    public RCONClientState State => _authenticationCompletion?.Task.Status switch
    {
        null => RCONClientState.Disconnected,
        < TaskStatus.RanToCompletion => _connection?.Connected is true ? RCONClientState.Connected : RCONClientState.Connecting,
        TaskStatus.RanToCompletion => _connection?.Connected is true ? _authenticationCompletion.Task.Result is true ? RCONClientState.Authenticated : RCONClientState.Connected : RCONClientState.Disconnected,
        _ => RCONClientState.Disconnected,
    };

    /// <summary> Fired if connection is lost. </summary>
    public event EventHandler? Disconnected;

    /// <summary> Fired when an RCON package has been received. </summary>
    public event EventHandler<PacketReceivedEventArgs>? PacketReceived;

    /// <summary> Create an instance of an RCON client. </summary>
    public RCONClient(IPAddress host, ushort port, string password, RCONClientOptions? options = null)
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
            try
            {
                await Authenticated();
            }
            catch (TaskCanceledException exception)
            {
                ResetAndThrow(exception);
            }

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
        using (cancellation.Token.Register(_authenticationCompletion.SetCanceled, false))
        {
            _connection = new RCONConnection(socket);
            _connection.Disconnected += OnDisconnected;
            _connection.PacketReceived += OnPacketReceived;

            if (_options.Timeout != TimeSpan.Zero)
            {
                cancellation.CancelAfter(_options.Timeout);
            }

            try
            {
                await _connection.SendAsync(
                    new(0, RCONPacketType.Auth, _password),
                    cancellation.Token).ConfigureAwait(false);

                await Authenticated();
            }
            catch (TaskCanceledException exception)
            {
                ResetAndThrow(exception);
            }
        }

        async Task Authenticated()
        {
            var completed = await Task.WhenAny(
                _authenticationCompletion.Task,
                _connection!.Closed).ConfigureAwait(false);

            if (completed == _authenticationCompletion?.Task)
            {
                if (await _authenticationCompletion.Task.ConfigureAwait(false)) return;
            }

            throw new RCONAuthenticationException();
        }

        void ResetAndThrow(TaskCanceledException exception)
        {
            if (_connection is not null)
            {
                _connection.Dispose();
                _connection = null;
            }

            _authenticationCompletion = null;
            throw new RCONException($"Failed to authenticate with the host within the configured timeout of '{_options.Timeout}'.", exception);
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

    private void OnDisconnected(object? sender, EventArgs e)
    {
        if (_connection is not null)
        {
            _connection.Dispose();
            _connection = null;
        }

        _authenticationCompletion = null;
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    private void OnPacketReceived(object? sender, PacketReceivedEventArgs e)
    {
        PacketReceived?.Invoke(this, e);
        if (!e.Handled && e.Packet.Type is RCONPacketType.AuthResponse)
        {
            e.Handled = true;

            // Failed auth responses return with an ID of -1
            if (e.Packet.Id is -1) _authenticationCompletion?.SetResult(false);

            _authenticationCompletion?.SetResult(true);
            return;
        }

        if (_responseByPacketId.TryRemove(e.Packet.Id, out var response))
        {
            if (!_options.UseKoraktorMethod)
            {
                response.Complete(e.Packet.Body);
                return;
            }

            // NOTE: Koraktor method: if an existing response body exists, and this packet indicates completion, complete the request
            if (e.Packet.Body is "" or "\0\u0001\0\0")
            {
                response.Complete(e.Packet.Body);
                return;
            }

            response.Append(e.Packet.Body);
            _responseByPacketId[e.Packet.Id] = response;
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
            using (cancellationSource.Token.Register(response.Cancel, false))
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
                    _connection.Closed).ConfigureAwait(false);

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
            if (completion.TrySetCanceled()) Reset();
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

/// <summary> Represents options for an RCON client. </summary>
public sealed class RCONClientOptions
{
    /// <summary> A <see cref="ParserPool"/> to be used for parsing the responses of commands.  </summary>
    public ParserPool Parsers { get; set; } = ParserPool.Shared;

    /// <summary> Whether the 'Koraktor' method of handling multi-packet responses should be used. </summary>
    public bool UseKoraktorMethod { get; set; }

    /// <summary> A timeout to be used when connecting and executing commands. </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    public RCONClientOptions(bool useKoraktorMethod = false)
    {
        UseKoraktorMethod = useKoraktorMethod;
    }

    public RCONClientOptions(TimeSpan timeout, bool useKoraktorMethod = false)
    {
        Timeout = timeout;
        UseKoraktorMethod = useKoraktorMethod;
    }
}

public enum RCONClientState
{
    Disconnected,
    Connecting,
    Connected,
    Authenticated
}
