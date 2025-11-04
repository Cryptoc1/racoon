using Racoon.Tool.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Racoon.Tool.Interceptors;

internal sealed class NoLogoInterceptor( IAnsiConsole stdout ) : ICommandInterceptor
{
    private static readonly bool NoLogo = DefaultNoLogo();
    private static readonly FigletText Logo = new FigletText( "Racoon" ).Color( Color.MediumSpringGreen );

    private static bool DefaultNoLogo( )
    {
        var value = Environment.GetEnvironmentVariable( "RACOON_NOLOGO" )?.Trim( '"', '\'' );
        return value is "1" || value?.Equals( bool.TrueString, StringComparison.OrdinalIgnoreCase ) is true;
    }

    public void Intercept( CommandContext context, CommandSettings settings )
    {
        ArgumentNullException.ThrowIfNull( context );
        ArgumentNullException.ThrowIfNull( settings );

        if( NoLogo || settings is ToolSettings { NoLogo: true } )
        {
            return;
        }

        stdout.Write( Logo );
    }
}