using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using CoreRCON.PacketFormats;

#if NETSTANDARD2_1_OR_GREATER
#else
using CoreRCON.Internal;
#endif

namespace CoreRCON;

public sealed class RCONConnection : IDisposable
{
    private readonly Pipe _pipe;

    private bool _disposed;
    private Socket? _socket;

    /// <summary> Indicates whether the underlying connection is currently open. </summary>
    public bool Connected => _socket?.Connected is true;

    /// <summary> A task that completes when the underlying connection is closed. </summary>
    public Task Closed { get; }

    public event EventHandler? Disconnected;
    public event EventHandler<PacketReceivedEventArgs>? PacketReceived;

    internal RCONConnection(Socket socket)
    {
        _pipe = new();
        _socket = socket;

        var writing = Write(_pipe.Writer, _socket);
        var reading = Read(_pipe.Reader, OnPacketReceived);
        Closed = Task.WhenAny(reading, writing)
            .ContinueWith(_ => Disconnected?.Invoke(this, EventArgs.Empty));
    }

    public void Dispose()
    {
        if (_disposed) return;

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

        _disposed = true;
    }

    private static async Task Write(PipeWriter writer, Socket socket)
    {
        while (socket?.Connected is true)
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

    private void OnPacketReceived(RCONPacket packet) => PacketReceived?.Invoke(this, new(this, packet));

    /// <summary> Send the given <paramref name="packet"/> on the connection. </summary>
    /// <param name="packet"> The packet to be sent. </param>
    /// <param name="cancellation"> A token to cancel sending of the packet. </param>
    /// <returns> A task that completes when the packet has been sent. </returns>
    /// <exception cref="RCONException"> The underlying socket is no longer connected. </exception>
    public async ValueTask SendAsync(RCONPacket packet, CancellationToken cancellation = default)
    {
        if (_socket?.Connected is not true) throw new RCONException("The underlying socket is no longer connected.");

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

/// <summary> Represents the arguments of an event that occurs when a packet is received by an RCONServer connection. </summary>
/// <param name="connection"> The connection that receieved the packet. </param>
/// <param name="packet"> The packet that was received. </param>
public sealed class PacketReceivedEventArgs(RCONConnection connection, RCONPacket packet) : EventArgs
{
    /// <summary> The connection that received the packet. </summary>
    public RCONConnection Connection { get; } = connection;

    /// <summary> Indicates whether a handler of this event has handled the received packet, and default handling should be skipped. </summary>
    public bool Handled { get; set; }

    /// <summary> The packet that was received. </summary>
    public RCONPacket Packet { get; } = packet;
}
