using System.Text.RegularExpressions;

namespace CoreRCON.Internal;

internal static partial class NewLineSanitizer
{
    public static string Sanitize(string value)
    {
        return Pattern().Replace(value, Environment.NewLine);
    }

    [GeneratedRegex(@"\r\n|\n\r|\n|\r", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
