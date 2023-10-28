using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CoreRCON.PacketFormats;

// Structure of a LogAddress packet from SRCDS:
// 255 255 255 255
// 82 or 83 (82 = no password, 83 = password)
// if 82, the rest of the packet is the body.
// Not sure what happens if it's 83, since I can't get my test server to return one even with sv_logsecret set.
// https://developer.valvesoftware.com/wiki/HL_Log_Standard
public readonly record struct LogAddressPacket
{
    private static readonly Regex DateExtractor = new(@"L (\d{2}/\d{2}/\d{4} - \d{2}:\d{2}:\d{2}):", RegexOptions.Compiled);

    /// <summary> The body of the packet with the timestamp removed. </summary>
    public readonly string Body { get; }

    /// <summary> [UNSUPPORTED] If the packet was sent with sv_logsecret set. </summary>
    public readonly bool HasPassword { get; }

    /// <summary> The raw body of the packet. </summary>
    public readonly string RawBody { get; }

    /// <summary> The timestamp at which the packet was sent (not received). </summary>
    public readonly DateTime Timestamp { get; }

    /// <summary> Create a new packet.</summary>
    /// <param name="hasPassword">[UNSUPPORTED] Whether the server returned this packet with sv_logsecret set.</param>
    /// <param name="value">The raw body from the packet.</param>
    public LogAddressPacket(bool hasPassword, string value)
    {
        HasPassword = hasPassword;
        RawBody = value;

        // Get timestamp
        var match = DateExtractor.Match(value);
        if (match.Success)
        {
            Timestamp = DateTime.ParseExact(match.Groups[1].Value, "MM/dd/yyyy - HH:mm:ss", CultureInfo.InvariantCulture);
        }
        else
        {
            Timestamp = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        // Get body without the date/time
        Body = value[25..];
    }

    /// <summary> Converts a buffer to a packet. </summary>
    /// <param name="buffer">Buffer to read.</param>
    /// <exception cref="InvalidDataException"/>
    internal static bool TryFromBytes(byte[] buffer, out LogAddressPacket packet)
    {
        if (buffer.Length < 7 || !buffer.Take(4).SequenceEqual(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }))
        {
            packet = default;
            return false;
        }

        // 83 = magic byte
        bool hasPassword = buffer[5] == 83;

        try
        {
            string body = NewLineSanitizer.Sanitize(
                Encoding.UTF8.GetString(buffer, 5, buffer.Length - 7));

            packet = new(hasPassword, body);
            return true;
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"{nameof(LogAddressPacket)}: {DateTime.Now} - Error reading logaddress packet from server: {ex.Message}");
#endif

            packet = default;
            return false;
        }
    }
}
