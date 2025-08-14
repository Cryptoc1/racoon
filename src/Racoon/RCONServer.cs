using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Racoon;

/// <summary> Create a RCON server. </summary>
/// <param name="endpoint"> The endpoint to bind to. </param>
/// <param name="password"> The password to authenticate with. </param>
/// <param name="options"> The options to use for serving connections. </param>
public sealed class RCONServer( IPEndPoint endpoint, string password, RCONServerOptions? options = null ) : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly ConcurrentDictionary<Guid, RCONConnection> connections = new();
    private readonly RCONServerOptions options = options ?? new();

    private bool disposed;
    private Socket? socket;

    /// <summary> Occurs when the server recieves a new <see cref="RCONConnection"/>. </summary>
    public event EventHandler<ConnectionEventArgs>? Connection;

    /// <summary> Occurs when a connection to the server receives a <see cref="RCONPacket"/>. </summary>
    public event EventHandler<PacketReceivedEventArgs>? PacketReceived;

    /// <inheritdoc />
    public void Dispose( )
    {
        if( disposed )
        {
            return;
        }

        cancellation.Cancel();
        DestroySocket();

        disposed = true;
    }

    /// <summary> Binds the underlying socket and starts listening for RCON packets. </summary>
    /// <param name="cancellation"> A token to cancel listening. </param>
    /// <returns> A long-running task that completes when listening has been cancelled, or the underlying socket becomes unbound. </returns>
    /// <remarks> The returned <see cref="Task"/> is gracefully cancelled upon disposal of the server. </remarks>
    /// <exception cref="RCONException" />
    public async Task ListenAsync( CancellationToken cancellation = default )
    {
        if( socket is not null )
        {
            throw new RCONException( "Server is already listening." );
        }

        socket = new Socket( endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp )
        {
            ExclusiveAddressUse = true,
            NoDelay = true,
        };

        socket.Bind( endpoint );
        socket.Listen( options.MaxConnections );

        using var combined = CancellationTokenSource.CreateLinkedTokenSource( this.cancellation.Token, cancellation );
        try
        {
            while( !combined.IsCancellationRequested )
            {
                var socket = await this.socket.AcceptAsync( combined.Token ).ConfigureAwait( false );
                if( socket is not null )
                {
                    OnAccepted( socket );
                }
            }
        }
        finally
        {
            DestroySocket();
        }
    }

    private void OnAccepted( Socket socket )
    {
        var identifier = Guid.NewGuid();
        var connection = new RCONConnection( socket );
        if( connections.TryAdd( identifier, connection ) )
        {
            connection.PacketReceived += OnPacketReceived;
            connection.Disconnected += ( _, _ ) =>
            {
                if( connections.TryRemove( identifier, out var c ) )
                {
                    c.Dispose();
                }
            };

            Connection?.Invoke( this, new( connection, identifier ) );
        }
    }

    private async void OnPacketReceived( object? sender, PacketReceivedEventArgs e )
    {
        PacketReceived?.Invoke( this, e );
        if( !e.Handled && e.Packet.Type is RCONPacketType.Auth )
        {
            e.Handled = true;
            await e.Connection.SendAsync(
                new( e.Packet.Body == password ? e.Packet.Id : -1,
                    RCONPacketType.AuthResponse,
                    string.Empty ) ).ConfigureAwait( false );
        }
    }

    private void DestroySocket( )
    {
        foreach( var connection in connections.Values )
        {
            connection.Dispose();
        }

        connections.Clear();
        if( socket is not null )
        {
            if( socket.Connected )
            {
                socket.Shutdown( SocketShutdown.Both );
                socket.Disconnect( false );
            }

            socket.Dispose();
            socket = null;
        }
    }
}

/// <summary> Represents the arguments of an event that occurs when a client connects to an RCON server. </summary>
/// <param name="connection"> The accepted connection. </param>
/// <param name="identifier"> The unique identifier of the connection assigned by the server. </param>
public sealed class ConnectionEventArgs( RCONConnection connection, Guid identifier ) : EventArgs
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
