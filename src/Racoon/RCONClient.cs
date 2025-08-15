using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Racoon.Parsers;
using Racoon.Parsers.Abstractions;

namespace Racoon;

/// <summary> Create a RCON client. </summary>
/// <param name="endpoint"> The server to connect to. </param>
/// <param name="password"> The password to authenticate with. </param>
/// <param name="options"> The options to use for connecting to the host. </param>
public sealed class RCONClient( IPEndPoint endpoint, string password, RCONClientOptions? options = null ) : IDisposable
{
    // Allows us to keep track of when authentication succeeds, so we can block Connect from returning until it does.
    private TaskCompletionSource<bool>? authenticationCompletion;
    private RCONConnection? connection;

    // When generating the packet ID, use a never-been-used (for automatic packets) ID.
    private volatile int packetId;

    // NOTE: Use (1,1) to lock connection per command (only a single command may execute against the connection at a time)
    private readonly SemaphoreSlim commandLock = new( 1, 1 );
    private readonly ConcurrentDictionary<int, RCONResponse> responseByPacketId = new();
    private readonly RCONClientOptions options = options ?? new();
    private readonly ParserPool parsers = ParserPool.CreateDefault( builder =>
    {
        ArgumentNullException.ThrowIfNull( builder );

        options?.OnCreatingParserPool?.Invoke( builder );
    } );

    /// <summary> Indicates the current state of the underlying connection. </summary>
    public RCONClientState State => authenticationCompletion?.Task.Status switch
    {
        null => RCONClientState.Disconnected,
        < TaskStatus.RanToCompletion => connection?.Connected is true ? RCONClientState.Connected : RCONClientState.Connecting,
        TaskStatus.RanToCompletion => connection?.Connected is true ? authenticationCompletion.Task.Result ? RCONClientState.Authenticated : RCONClientState.Connected : RCONClientState.Disconnected,
        _ => RCONClientState.Disconnected,
    };

    /// <summary> Occurs when the connection is closed. </summary>
    public event EventHandler? Disconnected;

    /// <summary> Occurs when a <see cref="RCONPacket"/> is recieved by the connection. </summary>
    public event EventHandler<PacketReceivedEventArgs>? PacketReceived;

    /// <summary> Create an instance of an RCON client. </summary>
    public RCONClient( IPAddress host, ushort port, string password, RCONClientOptions? options = null )
        : this( new IPEndPoint( host, port ), password, options )
    {
    }

    /// <summary> Connect to the configured RCON host. </summary>
    /// <returns> A task which completes when authentication with the host succeeds. </returns>
    /// <exception cref="RCONException"> An unexpected error occurred attempting to connect to the server, caller may check the InnerException for further details. </exception>
    /// <exception cref="RCONAuthenticationException"> Connection with the server was successful, but authentication failed. </exception>
    public async Task ConnectAsync( CancellationToken cancellation = default )
    {
        if( authenticationCompletion is not null )
        {
            try
            {
                await Authenticated();
            }
            catch( TaskCanceledException exception )
            {
                ResetAndThrow( exception );
            }

            return;
        }

        using var activity = Tracing.Source.StartActivity( nameof( ConnectAsync ), ActivityKind.Client )
            ?.AddTag( Tracing.Tags.Address, endpoint.Address.ToString() )
            ?.AddTag( Tracing.Tags.Port, endpoint.Port.ToString( CultureInfo.InvariantCulture ) );

        authenticationCompletion = new();
        var socket = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp )
        {
            NoDelay = true,
            ReceiveTimeout = ( int )options.Timeout.TotalMilliseconds,
            SendTimeout = ( int )options.Timeout.TotalMilliseconds,
        };

        try
        {
            socket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true );
            await socket.ConnectAsync( endpoint, cancellation ).ConfigureAwait( false );
        }
        catch( SocketException exception )
        {
            socket.Dispose();
            socket = null;

            // NOTE: reset completion, allow callers to implement retry logic
            authenticationCompletion = null;
            throw new RCONException( "An attempt to connect to with the host failed.", exception );
        }

        using( var combined = CancellationTokenSource.CreateLinkedTokenSource( cancellation ) )
        using( combined.Token.Register( authenticationCompletion.SetCanceled, false ) )
        {
            connection = new RCONConnection( socket );
            connection.Disconnected += OnDisconnected;
            connection.PacketReceived += OnPacketReceived;

            if( options.Timeout != TimeSpan.Zero )
            {
                combined.CancelAfter( options.Timeout );
            }

            try
            {
                await connection.SendAsync(
                    new( 0, RCONPacketType.Auth, password ),
                    combined.Token ).ConfigureAwait( false );

                await Authenticated().ConfigureAwait( false );
            }
            catch( TaskCanceledException exception )
            {
                ResetAndThrow( exception );
            }
        }

        async Task Authenticated( )
        {
            var completed = await Task.WhenAny(
                authenticationCompletion.Task,
                connection!.Closed ).ConfigureAwait( false );

            if( completed == authenticationCompletion?.Task )
            {
                if( await authenticationCompletion.Task.ConfigureAwait( false ) )
                {
                    return;
                }
            }

            throw new RCONAuthenticationException();
        }

        void ResetAndThrow( TaskCanceledException exception )
        {
            connection?.Dispose();
            connection = null;

            authenticationCompletion = null;
            throw new RCONException( $"Failed to authenticate with the host within the configured timeout of '{options.Timeout}'.", exception );
        }
    }

    /// <inheritdoc />
    public void Dispose( )
    {
        connection?.Dispose();
        connection = null;

        commandLock.Dispose();
    }

    private void OnDisconnected( object? sender, EventArgs e )
    {
        connection?.Dispose();
        connection = null;

        authenticationCompletion = null;
        Disconnected?.Invoke( this, EventArgs.Empty );
    }

    private void OnPacketReceived( object? sender, PacketReceivedEventArgs e )
    {
        PacketReceived?.Invoke( this, e );
        if( !e.Handled && e.Packet.Type is RCONPacketType.AuthResponse )
        {
            e.Handled = true;

            // Failed auth responses return with an ID of -1
            if( e.Packet.Id is -1 )
            {
                authenticationCompletion?.SetResult( false );
            }

            authenticationCompletion?.SetResult( true );
            return;
        }

        if( responseByPacketId.TryRemove( e.Packet.Id, out var response ) )
        {
            if( !options.UseKoraktorMethod )
            {
                response.Complete( e.Packet.Body );
                return;
            }

            // NOTE: Koraktor method: if an existing response body exists, and this packet indicates completion, complete the request
            if( e.Packet.Body is "" or "\0\u0001\0\0" )
            {
                response.Complete( e.Packet.Body );
                return;
            }

            response.Append( e.Packet.Body );
            responseByPacketId[ e.Packet.Id ] = response;
        }
    }

    /// <summary> Send a RCON Command. </summary>
    /// <typeparam name="T"> The type of command response. </typeparam>
    /// <param name="command"> The command text to be sent. </param>
    /// <param name="cancellation"> A token that may cancel the command execution. </param>
    /// <exception cref="RCONCommandException"> An error occurred while sending the command. </exception>
    public async Task<T> SendCommandAsync<T>( string command, CancellationToken cancellation = default )
        where T : class, IParsed<T>
    {
        var response = await SendCommandAsync( command, cancellation ).ConfigureAwait( false );

        var parser = parsers.Get<T>();
        if( !parser.IsMatch( response ) )
        {
            throw RCONCommandException.Failed( "The command response could not be parsed." );
        }

        return parser.Parse( response );
    }

    /// <summary> Send a RCON Command. </summary>
    /// <param name="command"> The command text to be sent. </param>
    /// <param name="cancellation"> A token that may cancel the command execution. </param>
    /// <exception cref="RCONCommandException"> An error occurred while sending the command. </exception>
    public async Task<string> SendCommandAsync( string command, CancellationToken cancellation = default )
    {
        if( options.AutoConnect && State is not RCONClientState.Connected )
        {
            await ConnectAsync( cancellation );
        }

        if( connection is null )
        {
            throw new RCONCommandException( $"The connection has not been established. Ensure '{nameof( ConnectAsync )}' is called before '{nameof( SendCommandAsync )}'.", command );
        }

        await commandLock.WaitAsync( cancellation ).ConfigureAwait( false );

        var packet = new RCONPacket(
            Interlocked.Increment( ref packetId ),
            RCONPacketType.ExecCommand,
            command );

        using var activity = Tracing.Source.StartActivity( nameof( ConnectAsync ), ActivityKind.Client )
            ?.AddTag( Tracing.Tags.Address, endpoint.Address.ToString() )
            ?.AddTag( Tracing.Tags.Port, endpoint.Port.ToString( CultureInfo.InvariantCulture ) )
            ?.AddTag( Tracing.Tags.PacketId, packet.Id )
            ?.AddTag( Tracing.Tags.CommandText, command );

        var response = responseByPacketId[ packet.Id ] = new RCONResponse();
        try
        {
            using( var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource( cancellation ) )
            using( cancellationSource.Token.Register( response.Cancel, false ) )
            {
                await connection.SendAsync( packet, cancellationSource.Token ).ConfigureAwait( false );
                if( packet.Type is RCONPacketType.ExecCommand && options.UseKoraktorMethod )
                {
                    // NOTE: Koraktor method: send an additional empty packet; the server will respond with an empty packet, indicating completion of the request
                    packet = new( packet.Id, RCONPacketType.Response, string.Empty );
                    await connection.SendAsync( packet, cancellationSource.Token ).ConfigureAwait( false );
                }

                if( options.Timeout != TimeSpan.Zero )
                {
                    cancellationSource.CancelAfter( options.Timeout );
                }

                var completed = await Task.WhenAny(
                    response.Completed,
                    connection.Closed ).ConfigureAwait( false );

                if( completed == response.Completed )
                {
                    try
                    {
                        var content = await response.Completed.ConfigureAwait( false );
                        activity?.AddTag( Tracing.Tags.CommandResponse, content );

                        return content;
                    }
                    catch( TaskCanceledException exception )
                    {
                        throw new RCONCommandException( $"Failed to execute command within the configured timeout of '{options.Timeout}'.", command, exception );
                    }
                }

                throw RCONCommandException.Failed( command, completed.Exception );
            }
        }
        finally
        {
            if( responseByPacketId.TryRemove( packet.Id, out response ) )
            {
                response.Cancel();
            }

            commandLock.Release();
        }
    }

    private sealed class RCONResponse
    {
        private StringBuilder? body;
        private readonly TaskCompletionSource<string> completion = new();

        public Task<string> Completed => completion.Task;

        public void Append( string content )
        {
            if( body is null )
            {
                body = new( content );
                return;
            }

            body.Append( content );
        }

        public void Cancel( )
        {
            if( completion.TrySetCanceled() )
            {
                Reset();
            }
        }

        public void Complete( string content )
        {
            var value = body?.Append( content ).ToString() ?? content;
            Reset();

            completion.SetResult( value );
        }

        private void Reset( )
        {
            body?.Clear();
            body = null;
        }
    }

    private static class Tracing
    {
        private static readonly Version AssemblyVersion = typeof( RCONClient ).Assembly.GetName().Version!;
        private static string LibraryVersion => $"{AssemblyVersion.Major}.{AssemblyVersion.Minor}.{AssemblyVersion.Build}";

        public static readonly ActivitySource Source = new( "Racoon.RCONClient", LibraryVersion );

        public static class Tags
        {
            // Same Semantic Conventions for HTTP Spans
            public const string Address = "host.address";
            public const string Port = "host.port";

            // RCON Specific tags
            public const string PacketId = "rcon.packet_id";
            public const string CommandText = "rcon.command.text";
            public const string CommandResponse = "rcon.command.response";
        }
    }
}

/// <summary> Represents options for an RCON client. </summary>
public sealed class RCONClientOptions
{
    /// <summary> Indicate whether <see cref="RCONClient.ConnectAsync"/> should be invoked when invoking <see cref="RCONClient.SendCommandAsync(string, CancellationToken)"/>.  </summary>
    public bool AutoConnect { get; set; }

    /// <summary> An optional delegating that configure the <see cref="ParserPool"/> to be used for parsing the responses of commands.  </summary>
    public Action<IParserPoolBuilder>? OnCreatingParserPool { get; set; }

    /// <summary> A timeout to be used when connecting and executing commands. </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds( 15 );

    /// <summary> Whether the 'Koraktor' method of handling multi-packet responses should be used. </summary>
    public bool UseKoraktorMethod { get; set; }

    /// <summary> Create an instance of <see cref="RCONClientOptions"/>. </summary>
    /// <param name="useKoraktorMethod"> Whether to use the "Koraktor" method of handling multi-packet responses. </param>
    public RCONClientOptions( bool useKoraktorMethod = false )
    {
        UseKoraktorMethod = useKoraktorMethod;
    }

    /// <summary> Create an instance of <see cref="RCONClientOptions"/>. </summary>
    /// <param name="timeout"> The timeout to use for connecting and executing commands. </param>
    /// <param name="useKoraktorMethod"> Whether to use the "Koraktor" method of handling multi-packet responses. </param>
    public RCONClientOptions( TimeSpan timeout, bool useKoraktorMethod = false )
    {
        Timeout = timeout;
        UseKoraktorMethod = useKoraktorMethod;
    }
}

/// <summary> Represents the current state of an <see cref="RCONClient"/>. </summary>
public enum RCONClientState
{
    /// <summary> The client has been disconnected from, or failed to connect to, the server. </summary>
    Disconnected,

    /// <summary> The client is negotiating a connection. </summary>
    Connecting,

    /// <summary> The client successfully negotiated a connection, but has not yet been authenticated. </summary>
    Connected,

    /// <summary> The client successfully established an authenticated connection to the server. </summary>
    Authenticated
}
