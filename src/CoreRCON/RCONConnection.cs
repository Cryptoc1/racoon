using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace CoreRCON;

public sealed class RCONConnection : IDisposable
{
#if NET9_0_OR_GREATER
    private readonly Lock _disposing = new();
#else
    private readonly object _disposing = new();
#endif

    private readonly Pipe _pipe;

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
            .ContinueWith(_ => Disconnected?.Invoke(this, EventArgs.Empty), TaskContinuationOptions.ExecuteSynchronously);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_disposing)
        {
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

    private static async Task Write(PipeWriter writer, Socket socket)
    {
        while (socket?.Connected is true)
        {
            var buffer = writer.GetMemory(RCONPacketDefaults.MinPacketSize + sizeof(int));
            try
            {
                var read = await socket.ReceiveAsync(buffer, SocketFlags.None, CancellationToken.None);
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

            if ((buffer.IsEmpty && result.IsCompleted) || result.IsCompleted)
            {
                break;
            }
        }

        await reader.CompleteAsync().ConfigureAwait(false);

        static bool TryReadPacketSize(in ReadOnlySequence<byte> buffer, out int size)
        {
            if (buffer.Length >= sizeof(int))
            {
                size = BitConverter.ToInt32(buffer.Slice(buffer.Start, sizeof(int)).ToArray());
                return true;
            }

            size = default;
            return false;
        }
    }

    private void OnPacketReceived(RCONPacket packet)
    {
        PacketReceived?.Invoke(this, new(this, packet));
    }

    /// <summary> Send the given <paramref name="packet"/> on the connection. </summary>
    /// <param name="packet"> The packet to be sent. </param>
    /// <param name="cancellation"> A token to cancel sending of the packet. </param>
    /// <returns> A task that completes when the packet has been sent. </returns>
    /// <exception cref="RCONException"> The underlying socket is no longer connected. </exception>
    public async ValueTask SendAsync(RCONPacket packet, CancellationToken cancellation = default)
    {
        if (_socket?.Connected is not true)
        {
            throw new RCONException("The underlying socket is no longer connected.");
        }

        using var rented = MemoryPool<byte>.Shared.Rent(packet.Size + sizeof(int));

        var written = packet.GetBytes(rented.Memory.Span);
        if (written < packet.Size + sizeof(int))
        {
            return;
        }

        await _socket.SendAsync(
            rented.Memory[..written],
            SocketFlags.None,
            cancellation).ConfigureAwait(false);
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
