using System.Text.RegularExpressions;

namespace Racoon.Internal;

internal static partial class NewLineSanitizer
{
    [GeneratedRegex( @"\r\n|\n\r|\n|\r", RegexOptions.Multiline | RegexOptions.CultureInvariant )]
    private static partial Regex Pattern { get; }

    public static string Sanitize( string value ) => Pattern.Replace( value, Environment.NewLine );
}
