using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace Racoon;

/// <summary> Represents a RCON connection that can send and recieve <see cref="RCONPacket"/>s. </summary>
public sealed class RCONConnection : IDisposable
{
    private readonly Lock disposing = new();
    private readonly Pipe pipe;

    private Socket? socket;

    /// <summary> Indicates whether the underlying connection is currently open. </summary>
    public bool Connected => socket?.Connected is true;

    /// <summary> A task that completes when the underlying connection is closed. </summary>
    public Task Closed { get; }

    /// <summary> Occurs when the connection is closed. </summary>
    public event EventHandler? Disconnected;

    /// <summary> Occurs when a <see cref="RCONPacket"/> is recieved by the connection. </summary>
    public event EventHandler<PacketReceivedEventArgs>? PacketReceived;

    internal RCONConnection( Socket socket )
    {
        pipe = new();
        this.socket = socket;

        var writing = Task.Run( ( ) => Write( pipe.Writer, this.socket ) );
        var reading = Task.Run( ( ) => Read( pipe.Reader, OnPacketReceived ) );
        Closed = Task.WhenAny( reading, writing )
            .ContinueWith( _ => Disconnected?.Invoke( this, EventArgs.Empty ), TaskContinuationOptions.ExecuteSynchronously );
    }

    /// <inheritdoc/>
    public void Dispose( )
    {
        lock( disposing )
        {
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

    private static async Task Write( PipeWriter writer, Socket socket )
    {
        while( socket?.Connected is true )
        {
            var buffer = writer.GetMemory( RCONPacketDefaults.MinPacketSize + sizeof( int ) );
            try
            {
                var read = await socket.ReceiveAsync( buffer, SocketFlags.None, CancellationToken.None );
                if( read is 0 )
                {
                    break;
                }

                writer.Advance( read );
            }
            catch
            {
                break;
            }

            var result = await writer.FlushAsync().ConfigureAwait( false );
            if( result.IsCompleted )
            {
                break;
            }
        }

        await writer.CompleteAsync().ConfigureAwait( false );
    }

    private static async Task Read( PipeReader reader, Action<RCONPacket> onPacket )
    {
        while( true )
        {
            var result = await reader.ReadAsync()
                .ConfigureAwait( false );

            var buffer = result.Buffer;
            var start = buffer.Start;

            if( TryReadPacketSize( buffer, out var size ) && buffer.Length >= (size += sizeof( int )) )
            {
                var end = buffer.GetPosition( size, start );
                if( RCONPacket.TryFromBytes(
                    buffer.Slice( start, end ).ToArray(),
                    out var packet ) )
                {
                    onPacket( packet );
                }

                reader.AdvanceTo( end );
            }
            else
            {
                reader.AdvanceTo( start, buffer.End );
            }

            if( (buffer.IsEmpty && result.IsCompleted) || result.IsCompleted )
            {
                break;
            }
        }

        await reader.CompleteAsync().ConfigureAwait( false );

        static bool TryReadPacketSize( in ReadOnlySequence<byte> buffer, out int size )
        {
            if( buffer.Length >= sizeof( int ) )
            {
                size = BitConverter.ToInt32( buffer.Slice( buffer.Start, sizeof( int ) ).ToArray() );
                return true;
            }

            size = default;
            return false;
        }
    }

    private void OnPacketReceived( RCONPacket packet )
    {
        PacketReceived?.Invoke( this, new( this, packet ) );
    }

    /// <summary> Send the given <paramref name="packet"/> on the connection. </summary>
    /// <param name="packet"> The packet to be sent. </param>
    /// <param name="cancellation"> A token to cancel sending of the packet. </param>
    /// <returns> A task that completes when the packet has been sent. </returns>
    /// <exception cref="RCONException"> The underlying socket is no longer connected. </exception>
    public async ValueTask SendAsync( RCONPacket packet, CancellationToken cancellation = default )
    {
        if( socket?.Connected is not true )
        {
            throw new RCONException( "The underlying socket is no longer connected." );
        }

        using var rented = MemoryPool<byte>.Shared.Rent( packet.Size + sizeof( int ) );

        var written = packet.GetBytes( rented.Memory.Span );
        if( written < packet.Size + sizeof( int ) )
        {
            return;
        }

        await socket.SendAsync(
            rented.Memory[ ..written ],
            SocketFlags.None,
            cancellation ).ConfigureAwait( false );
    }
}

/// <summary> Represents the arguments of an event that occurs when a packet is received by an RCONServer connection. </summary>
/// <param name="connection"> The connection that receieved the packet. </param>
/// <param name="packet"> The packet that was received. </param>
public sealed class PacketReceivedEventArgs( RCONConnection connection, RCONPacket packet ) : EventArgs
{
    /// <summary> The connection that received the packet. </summary>
    public RCONConnection Connection { get; } = connection;

    /// <summary> Indicates whether a handler of this event has handled the received packet, and default handling should be skipped. </summary>
    public bool Handled { get; set; }

    /// <summary> The packet that was received. </summary>
    public RCONPacket Packet { get; } = packet;
}
