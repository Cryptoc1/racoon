using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace CoreRCON;

/// <summary> Create a RCON server. </summary>
/// <param name="endpoint"> The endpoint to bind to. </param>
/// <param name="password"> The password to authenticate with. </param>
/// <param name="options"> The options to use for serving connections. </param>
public sealed class RCONServer(IPEndPoint endpoint, string password, RCONServerOptions? options = null) : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ConcurrentDictionary<Guid, RCONConnection> _connections = new();
    private readonly IPEndPoint _endpoint = endpoint;
    private readonly RCONServerOptions _options = options ?? new();
    private readonly string _password = password;

    private bool _disposed;
    private Socket? _socket;

    public event EventHandler<ConnectionEventArgs>? Connection;
    public event EventHandler<PacketReceivedEventArgs>? PacketReceived;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cancellation.Cancel();
        DestroySocket();

        _disposed = true;
    }

    /// <summary> Binds the underlying socket and starts listening for RCON packets. </summary>
    /// <param name="cancellation"> A token to cancel listening. </param>
    /// <returns> A long-running task that completes when listening has been cancelled, or the underlying socket becomes unbound. </returns>
    /// <remarks> The returned <see cref="Task"/> is gracefully cancelled upon disposal of the server. </remarks>
    /// <exception cref="RCONException" />
    public async Task ListenAsync(CancellationToken cancellation = default)
    {
        if (_socket is not null)
        {
            throw new RCONException("Server is already listening.");
        }

        _socket = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            ExclusiveAddressUse = true,
            NoDelay = true,
        };

        _socket.Bind(_endpoint);
        _socket.Listen(_options.MaxConnections);

        using var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token, cancellation);
        try
        {
            while (true)
            {
                var socket = await _socket.AcceptAsync(combinedCancellation.Token).ConfigureAwait(false);
                if (socket is not null)
                {
                    OnAccepted(socket);
                }
            }
        }
        finally
        {
            DestroySocket();
        }
    }

    private void OnAccepted(Socket socket)
    {
        var identifier = Guid.NewGuid();
        var connection = new RCONConnection(socket);
        if (_connections.TryAdd(identifier, connection))
        {
            connection.PacketReceived += OnPacketReceived;
            connection.Disconnected += (_, _) =>
            {
                if (_connections.TryRemove(identifier, out var c))
                {
                    c.Dispose();
                }
            };

            Connection?.Invoke(this, new(connection, identifier));
        }
    }

    private async void OnPacketReceived(object? sender, PacketReceivedEventArgs e)
    {
        PacketReceived?.Invoke(this, e);
        if (!e.Handled && e.Packet.Type is RCONPacketType.Auth)
        {
            e.Handled = true;
            await e.Connection.SendAsync(
                new(e.Packet.Body == _password ? e.Packet.Id : -1,
                    RCONPacketType.AuthResponse,
                    string.Empty)).ConfigureAwait(false);
        }
    }

    private void DestroySocket()
    {
        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }

        _connections.Clear();

        if (_socket is not null)
        {
            if (_socket.Connected)
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Disconnect(false);
            }

            _socket.Dispose();
            _socket = null;
        }
    }
}

/// <summary> Represents the arguments of an event that occurs when a client connects to an RCON server. </summary>
/// <param name="connection"> The accepted connection. </param>
public sealed class ConnectionEventArgs(RCONConnection connection, Guid identifier) : EventArgs
{
    /// <summary> The RCON connection. </summary>
    public RCONConnection Connection { get; } = connection;

    /// <summary> The server's unique identifier of the connection. </summary>
    public Guid Identifier { get; } = identifier;
}

/// <summary> Represents the options for server RCON connections. </summary>
public sealed class RCONServerOptions
{
    /// <summary> The maximum number of allowed connections. </summary>
    public int MaxConnections { get; set; } = 10;
}
