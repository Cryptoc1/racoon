using System.Text;

/// <summary> Represents a Valve RCON Packet. </summary>
/// <seealso href="https://developer.valvesoftware.com/wiki/Source_RCON_Protocol">Valve: Source RCON Protocoal</see>
namespace CoreRCON.PacketFormats;

/// <summary> Initialize a new packet. </summary>
/// <param name="Id"> Some kind of identifier to keep track of responses from the server. </param>
/// <param name="Type"> What the server is supposed to do with the body of this packet. </param>
/// <param name="Body"> The actual information held within. </param>
public readonly record struct RCONPacket(int Id, RCONPacketType Type, string Body)
{
    /// <summary> Deserializes a packet from the given <paramref name="buffer"/>. </summary>
    /// <param name="buffer"> The buffer to deserialize. </param>
    /// <returns> The deserialized packet. </returns>
    /// <exception cref="InvalidDataException" />
    internal static RCONPacket FromBytes(byte[] buffer)
    {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer), "Byte buffer cannot be null.");
        if (buffer.Length < 4) throw new InvalidDataException("Buffer does not contain a size field.");

        int size = BitConverter.ToInt32(buffer, 0);
        if (size > buffer.Length - 4) throw new InvalidDataException("Packet size specified was larger then buffer");

        if (size < 10) throw new InvalidDataException("Packet received was invalid.");

        int id = BitConverter.ToInt32(buffer, 4);
        RCONPacketType type = (RCONPacketType)BitConverter.ToInt32(buffer, 8);

        try
        {
            var body = NewLineSanitizer.Sanitize(
                Encoding.UTF8.GetString(buffer, 12, size - 10));

            return new RCONPacket(id, type, body.TrimEnd());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{DateTime.Now} - Error reading RCON packet body exception was: {ex.Message}");
            return new RCONPacket(id, type, "");
        }
    }

    /// <summary> Serializes a packet to a byte array for transporting over a network. </summary>
    /// <remarks> Body is serialized as UTF8. </remarks>
    internal byte[] ToBytes()
    {
        // NOTE: should also be compatible with ASCII only servers
        byte[] body = Encoding.UTF8.GetBytes(Body + '\0');

        using var packet = new MemoryStream(body.Length + 12);
        packet.Write(BitConverter.GetBytes(9 + body.Length), 0, 4);
        packet.Write(BitConverter.GetBytes(Id), 0, 4);
        packet.Write(BitConverter.GetBytes((int)Type), 0, 4);
        packet.Write(body, 0, body.Length);
        packet.Write([0], 0, 1);

        return packet.ToArray();
    }
}

public static class RCONPacketDefaults
{
    /// <summary> The maximum size of an RCON packet. </summary>
    /// <remarks> (Officially 4096, larger packet where received when running cvarlist). </remarks>
    public const int MaxPacketSize = 4096;

    /// <summary> The minimum size of a RCON packet. </summary>
    public const int MinPacketSize = 14;
}
