using Racoon.Tool.Commands;
using Spectre.Console;

namespace Racoon.Tool.Internal;

internal static class AnsiConsoleExtensions
{
    private static readonly FigletText Logo = new FigletText( "Racoon" ).Color( Color.MediumSpringGreen );

    public static void WriteLogo<TSettings>( this IAnsiConsole console, TSettings settings )
        where TSettings : ToolSettings
    {
        ArgumentNullException.ThrowIfNull( console );
        ArgumentNullException.ThrowIfNull( settings );

        if( !settings.NoLogo )
        {
            console.Write( Logo );
        }
    }
}