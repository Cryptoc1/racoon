using Spectre.Console;

namespace Racoon.Tool.Internal;

internal static class RCONColor
{
    public static Color Error => Color.OrangeRed1;
    public static Color Hint => Color.Tan;
    public static Color Success => Color.PaleGreen1_1;
    public static Color Warning => Color.Yellow;
}