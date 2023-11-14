using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CoreRCON.Internal;

namespace CoreRCON.PacketFormats;

/// <summary> Represents the contents of a log_address packet. </summary>
/// <param name="Body"> The body of the log. </param>
/// <param name="HasPassword"> Whether the packet includes a password. </param>
/// <param name="Timestamp"> The parsed timestamp of the log. </param>
public readonly record struct LogAddressPacket(string Body, bool HasPassword, DateTime Timestamp)
{
    private static readonly Regex DateExtractor = new(@"(L )?(\d{2}/\d{2}/\d{4} - \d{2}:\d{2}:\d{2}(\.\d{3})?)\s?(:|-)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly string[] DateFormats = [
        "MM/dd/yyyy - HH:mm:ss",
        "MM/dd/yyyy - HH:mm:ss.fff"
    ];

    /// <summary> Attempt to serialize a <see cref="LogAddressPacket"/> from a buffer of bytes. </summary>
    /// <param name="buffer"> The buffer to be read. </param>
    /// <see cref="https://developer.valvesoftware.com/wiki/HL_Log_Standard"/>
    /// <exception cref="InvalidDataException"/>
    public static bool TryFromBytes(byte[] buffer, out LogAddressPacket packet)
    {
        if (buffer.Length > 7 && ContainsMarker(buffer) && BufferHelper.TryGetString(buffer, 5, buffer.Length - 8, out var value))
        {
            var match = DateExtractor.Match(value);
            if (match.Success && DateTime.TryParseExact(match.Groups[2].Value, DateFormats, CultureInfo.InvariantCulture, default, out var timestamp))
            {
                var body = NewLineSanitizer.Sanitize(value[match.Groups[0].Length..]).Trim();
                if (body.Length is not 0)
                {
                    packet = new(body, buffer[5] is 83, timestamp);
                    return true;
                }
            }
        }

        packet = default;
        return false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ContainsMarker(byte[] buffer) => buffer[0] is 0xFF && buffer[1] is 0xFF && buffer[2] is 0xFF && buffer[3] is 0xFF;
    }
}
