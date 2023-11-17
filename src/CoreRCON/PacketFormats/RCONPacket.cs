using System.Runtime.CompilerServices;
using System.Text;
using CoreRCON.Internal;

namespace CoreRCON.PacketFormats;

/// <summary> Represents a Source RCON Packet. </summary>
/// <param name="Id"> Some kind of identifier to keep track of responses from the server. </param>
/// <param name="Type"> What the server is supposed to do with the body of this packet. </param>
/// <param name="Body"> The actual information held within. </param>
/// <seealso cref="https://developer.valvesoftware.com/wiki/Source_RCON_Protocol">Valve: Source RCON Protocoal</see>
public readonly record struct RCONPacket(int Id, RCONPacketType Type, string Body)
{
    /// <summary> The size of the packet. </summary>
    /// <remarks> The size of the packet does not include the (4) bytes represented by the Size integer itself. </remarks>
    public readonly int Size => RCONPacketDefaults.MinPacketSize + Body.Length;

    /// <summary> Writes the packet as bytes to the given <paramref name="buffer"/>. </summary>
    /// <remarks> Body is serialized as UTF8. </remarks>
    /// <returns> The number of bytes written. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> The given buffer is not large enough to serialize the packet. </exception>
    /// <seealso cref="https://developer.valvesoftware.com/wiki/Source_RCON_Protocol#Basic_Packet_Structure" />
    public int GetBytes(in Span<byte> buffer)
    {
        var size = Size;
        if (buffer.Length < size + sizeof(int)) throw new ArgumentOutOfRangeException(nameof(buffer), buffer.Length, $"Buffer must be have a length of at least {size + sizeof(int)} to serialize packet.");

        var written = 0;
        Write(buffer, ref written, BitConverter.GetBytes(size));
        Write(buffer, ref written, BitConverter.GetBytes(Id));
        Write(buffer, ref written, BitConverter.GetBytes((int)Type));
        Write(buffer, ref written, Encoding.UTF8.GetBytes(Body));
        Write(buffer, ref written, [0x00, 0x00]);

        return written;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Write(in Span<byte> buffer, ref int written, in ReadOnlySpan<byte> value)
        {
            for (var i = 0; i < value.Length; i++, written++)
            {
                buffer[written] = value[i];
            }
        }
    }

    public static bool TryFromBytes(in byte[] buffer, out RCONPacket packet)
    {
#if NETSTANDARD2_1_OR_GREATER
        ReadOnlySpan<byte> casted = buffer;
        return TryFromBytes(casted, out packet);
#else
        if (buffer.Length >= RCONPacketDefaults.MinPacketSize + sizeof(int))
        {
            var size = BitConverter.ToInt32(buffer, 0);
            if (size >= RCONPacketDefaults.MinPacketSize && buffer.Length == size + sizeof(int))
            {
                if (BufferHelper.TryGetString(buffer, RCONPacketDefaults.MinPacketSize + 2, size - RCONPacketDefaults.MinPacketSize, out var body))
                {
                    packet = new(
                        BitConverter.ToInt32(buffer, 4),
                        (RCONPacketType)BitConverter.ToInt32(buffer, 8),
                        NewLineSanitizer.Sanitize(body).TrimEnd());

                    return true;
                }
            }
        }

        packet = default;
        return false;
#endif
    }

#if NETSTANDARD2_1_OR_GREATER
    public static bool TryFromBytes(in ReadOnlySpan<byte> buffer, out RCONPacket packet)
    {
        if (buffer.Length >= RCONPacketDefaults.MinPacketSize + sizeof(int))
        {
            var size = BitConverter.ToInt32(buffer[..sizeof(int)]);
            if (size >= RCONPacketDefaults.MinPacketSize && buffer.Length == size + sizeof(int))
            {
                if (BufferHelper.TryGetString(
                    buffer.Slice(RCONPacketDefaults.MinPacketSize + 2, size - RCONPacketDefaults.MinPacketSize),
                    out var body))
                {
                    packet = new(
                        BitConverter.ToInt32(buffer.Slice(4, sizeof(int))),
                        (RCONPacketType)BitConverter.ToInt32(buffer.Slice(8, sizeof(int))),
                        NewLineSanitizer.Sanitize(body).TrimEnd());

                    return true;
                }
            }
        }

        packet = default;
        return false;
    }
#endif
}

public static class RCONPacketDefaults
{
    /// <summary> The maximum size of an RCON packet. </summary>
    /// <remarks> (Officially 4096, larger packet where received when running cvarlist). </remarks>
    public const int MaxPacketSize = 4096;

    /// <summary> The minimum size of a RCON packet. </summary>
    public const int MinPacketSize = /* id */ sizeof(int) + /* type */ sizeof(int) + /* null terminators */ 2;
}

/// <summary> Defines the type of data represented in an <see cref="RCONPacket"/>. </summary>
/// <see cref="https://developer.valvesoftware.com/wiki/Source_RCON_Protocol#Packet_Type"/>
public enum RCONPacketType
{
    /// <summary> Indicates a packet is a response to an <see cref="ExecCommand"/> packet. </summary>
    Response = 0,

    /// <summary> Indicates a packet is the response to an <see cref="Auth"/> packet. </summary>
    AuthResponse = 2,

#pragma warning disable CA1069 // ExecCommand has the same value '2', because that's just how valve did it.

    /// <summary> Indicates a packet is a request to execute a command against a server. </summary>
    ExecCommand = 2,
#pragma warning restore CA1069

    /// <summary> Indicates a packet is a request to authenticate with an RCON server. </summary>
    Auth = 3
}
