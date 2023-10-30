using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace CoreRCON.Extensions;

internal static class SocketExtensions
{
    /// <summary> Receives a block of memory from the socket. </summary>
    /// <param name="socket"> The socket to receive from. </param>
    /// <param name="memory"> A segment of memory write received data to. </param>
    /// <param name="socketFlags"> Flags for socket. </param>
    /// <returns> A task which completes with the number of bytes written. </returns>
    public static Task<int> ReceiveAsync(this Socket socket, Memory<byte> memory, SocketFlags socketFlags)
    {
        ReadOnlyMemory<byte> casted = memory;
        if (!MemoryMarshal.TryGetArray(casted, out var buffer))
        {
            throw new ArgumentException("Expected an Array-backed buffer.", nameof(memory));
        }

        return SocketTaskExtensions.ReceiveAsync(socket, buffer, socketFlags);
    }
}
