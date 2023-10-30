using System.Runtime.CompilerServices;
using System.Text;
using CoreRCON.Extensions;

/// <summary> Represents a Valve RCON Packet. </summary>
/// <seealso cref="https://developer.valvesoftware.com/wiki/Source_RCON_Protocol">Valve: Source RCON Protocoal</see>
namespace CoreRCON.PacketFormats;

/// <summary> Represents the contents of a RCON packet. </summary>
/// <param name="Id"> Some kind of identifier to keep track of responses from the server. </param>
/// <param name="Type"> What the server is supposed to do with the body of this packet. </param>
/// <param name="Body"> The actual information held within. </param>
public readonly record struct RCONPacket(int Id, RCONPacketType Type, string Body)
{
    /// <summary> The size of the packet. </summary>
    /// <remarks> The size of the packet does not include the (4) bytes represented by the Size integer itself. </remarks>
    public int Size { get; } = RCONPacketDefaults.MinPacketSize - sizeof(int) + Body.Length;

    /// <summary> Serializes the packet to the given <paramref name="buffer"/>. </summary>
    /// <seealso cref="https://developer.valvesoftware.com/wiki/Source_RCON_Protocol#Basic_Packet_Structure" />
    /// <remarks> Body is serialized as UTF8. </remarks>
    /// <returns> The number of bytes written. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> The given buffer is not large enough to serialize the packet. </exception>
    public int GetBytes(Span<byte> buffer)
    {
        if (buffer.Length < Size + sizeof(int)) throw new ArgumentOutOfRangeException(nameof(buffer), buffer.Length, $"Buffer must be have a length of at least {Size + sizeof(int)} to serialize packet.");

        var written = 0;
        Write(buffer, ref written, BitConverter.GetBytes(Size));
        Write(buffer, ref written, BitConverter.GetBytes(Id));
        Write(buffer, ref written, BitConverter.GetBytes((int)Type));
        Write(buffer, ref written, Encoding.UTF8.GetBytes(Body));
        Write(buffer, ref written, [0x00, 0x00]);

        return written;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Write(Span<byte> buffer, ref int offset, Span<byte> value)
        {
            for (var i = 0; i < value.Length; i++, offset++)
            {
                buffer[offset] = value[i];
            }
        }
    }

    public static bool TryFromBytes(byte[] buffer, out RCONPacket packet)
    {
        if (buffer.Length >= RCONPacketDefaults.MinPacketSize)
        {
            var size = BitConverter.ToInt32(buffer, 0);
            if (size + sizeof(int) >= RCONPacketDefaults.MinPacketSize && size + sizeof(int) == buffer.Length)
            {
                if (BufferHelper.TryGetString(buffer, 10, buffer.Length - (RCONPacketDefaults.MinPacketSize - 2), out var body))
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
    }
}

public static class RCONPacketDefaults
{
    /// <summary> The maximum size of an RCON packet. </summary>
    /// <remarks> (Officially 4096, larger packet where received when running cvarlist). </remarks>
    public const int MaxPacketSize = 4096;

    /// <summary> The minimum size of a RCON packet. </summary>
    public const int MinPacketSize = /* size */ sizeof(int) + /* id */ sizeof(int) + /* type */ sizeof(int) + /* null terminators */ 2;
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
