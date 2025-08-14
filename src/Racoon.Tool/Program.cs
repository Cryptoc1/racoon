using System.ComponentModel;
using System.Net;
using System.Text;
using Racoon;
using Racoon.Extensions;
using Spectre.Console;
using Spectre.Console.Cli;

using RCONStatus = Racoon.Parsers.Standard.Status;

var app = new CommandApp<ShellCommand>();
app.Configure( options =>
{
    options.SetApplicationName( "racoon" );
    options.PropagateExceptions();
} );

return await app.RunAsync( args );

internal sealed class ShellCommand : AsyncCommand<ShellParameters>
{
    public override async Task<int> ExecuteAsync( CommandContext context, ShellParameters parameters )
    {
        if( !parameters.NoLogo )
        {
            AnsiConsole.Write( new FigletText( "Racoon" ).Color( Color.MediumSpringGreen ) );
        }

        var host = await ResolveHost( parameters.Host );
        if( host is null )
        {
            AnsiConsole.Write( $"[bold red]<!>[/] Provided host could not be resolved." );
            return ShellExitCode.InvalidHost;
        }

        var password = string.IsNullOrWhiteSpace( parameters.Password )
            ? AnsiConsole.Prompt( new TextPrompt<string>( $"Password>" ).Secret() )
            : parameters.Password;

        using var console = new RCONClient( host, parameters.Port, password, new RCONClientOptions( parameters.Timeout, parameters.UseKoraktorMethod ) );
        if( !await TryConnect( console ) )
        {
            return ShellExitCode.FailedToConnect;
        }

        AnsiConsole.WriteLine();
        WritePromptHelp();
        AnsiConsole.WriteLine();

        using var cancellation = new CancellationTokenSource();
        console.Disconnected += ( _, _ ) => cancellation.Cancel();

        using var prompt = new RCONPrompt( await console.Status() );
        while( console.State is RCONClientState.Authenticated )
        {
            var command = await prompt.ShowAsync( AnsiConsole.Console, cancellation.Token );
            if( command[ 0 ] is not ':' )
            {
                try
                {
                    var result = await console.SendCommandAsync( command, cancellation.Token );
                    AnsiConsole.WriteLine( result );

                    prompt.Error = result.StartsWith( "unknown command", StringComparison.OrdinalIgnoreCase );
                }
                catch( RCONCommandException exception )
                {
                    AnsiConsole.WriteException( exception );
                    prompt.Error = true;
                }
            }

            if( command.Equals( ":clear", StringComparison.OrdinalIgnoreCase ) )
            {
                AnsiConsole.Clear();
            }

            if( command.Equals( ":help", StringComparison.OrdinalIgnoreCase ) )
            {
                WritePromptHelp();
            }

            if( command.Equals( ":q", StringComparison.OrdinalIgnoreCase ) )
            {
                return 0;
            }

            prompt.AddHistory( command );
        }

        return ShellExitCode.Disconnected;

        static void WritePromptHelp( )
        {
            AnsiConsole.MarkupLine( "Use [bold tan]:q[/] to exit." );
            AnsiConsole.MarkupLine( "Use [bold tan]:clear[/] to clear the screen." );
            AnsiConsole.MarkupLine( "Use [bold tan]arrow up[/]/[bold tan]arrow down[/] to traverse history." );
            AnsiConsole.MarkupLine( "Use [bold tan]:help[/] to display this help text." );
        }
    }

    private static async Task<IPAddress?> ResolveHost( string host )
    {
        if( IPAddress.TryParse( host, out var address ) )
        {
            return address;
        }

        var addresses = await Dns.GetHostAddressesAsync( host );
        if( addresses.Length is 1 )
        {
            return addresses[ 0 ];
        }
        else if( addresses.Length > 1 )
        {
            return AnsiConsole.Prompt(
                new SelectionPrompt<IPAddress>()
                    .Title( $"Host [green]{host}[/] resolved to multiple addresses>" )
                    .AddChoices( addresses ) );
        }

        return default;
    }

    private static async Task<bool> TryConnect( RCONClient console, int retry = default )
    {
        try
        {
            await AnsiConsole.Status().StartAsync( "Connecting...", async context =>
            {
                context.Spinner( Spinner.Known.Dots3 );
                await Task.Delay( retry is 0 ? 250 : 1250 );

                await console.ConnectAsync();
            } );
        }
        catch( RCONException )
        {
            return AnsiConsole.Confirm( $"[bold red]<!>[/] Failed to connect to the host, retry?" )
                && await TryConnect( console, ++retry );
        }

        return true;
    }
}

internal sealed class RCONPrompt( RCONStatus status, int capacity = 1024 ) : IDisposable, IPrompt<string>
{
    public bool Error { get; set; }

    private readonly List<string> history = new( capacity );

    public void Dispose( ) => history.Clear();

    public string Show( IAnsiConsole console ) => ShowAsync( console, CancellationToken.None ).GetAwaiter().GetResult();

    public async Task<string> ShowAsync( IAnsiConsole console, CancellationToken cancellation )
    {
        WritePrompt( console );
        var value = await console.RunExclusive( async ( ) =>
        {
            var position = history.Count;
            var text = new StringBuilder();
            while( true )
            {
                cancellation.ThrowIfCancellationRequested();

                var key = await console.Input.ReadKeyAsync( true, cancellation );
                if( !key.HasValue )
                {
                    continue;
                }

                if( key.Value.Key is ConsoleKey.Enter )
                {
                    if( text.Length is 0 )
                    {
                        continue;
                    }

                    console.WriteLine();
                    return text.ToString();
                }

                if( history.Count is not 0 && key.Value.Key is ConsoleKey.UpArrow )
                {
                    var value = history[ position = Math.Max( --position, 0 ) ];
                    console.Write( string.Concat(
                        Enumerable.Range( 0, text.Length )
                            .Select( _ => "\b \b" ) ) );

                    text = text.Clear().Append( value );
                    console.Write( value );

                    continue;
                }

                if( history.Count is not 0 && key.Value.Key is ConsoleKey.DownArrow )
                {
                    var value = (position = Math.Min( ++position, history.Count )) == history.Count
                        ? string.Empty
                        : history[ position ];

                    console.Write( string.Concat(
                        Enumerable.Range( 0, text.Length )
                            .Select( _ => "\b \b" ) ) );

                    text = text.Clear().Append( value );
                    console.Write( value );
                }

                if( key.Value.Key is ConsoleKey.Backspace )
                {
                    if( text.Length > 0 )
                    {
                        text = text.Remove( text.Length - 1, 1 );
                        console.Write( "\b \b" );
                    }

                    continue;
                }

                var character = key.Value.KeyChar;
                if( !char.IsControl( character ) )
                {
                    text = text.Append( character );
                    console.Write( character.ToString() );

                    continue;
                }
            }
        } );

        if( string.IsNullOrWhiteSpace( value ) )
        {
            return await ShowAsync( console, cancellation );
        }

        return value.Trim();
    }

    public void AddHistory( string command )
    {
        if( history.Count == history.Capacity )
        {
            history.RemoveAt( 0 );
        }

        history.Add( command );
    }

    private void WritePrompt( IAnsiConsole console ) => console.Write( new Markup( $"[bold {(Error ? Color.Orange1 : Color.PaleGreen1_1)}]{(Error ? '!' : '@')}[/] [bold]{status.Hostname}[/]> " ) );
}

internal static class ShellExitCode
{
    public const int Disconnected = -50;
    public const int FailedToConnect = -100;
    public const int InvalidHost = -75;
}

internal sealed class ShellParameters : CommandSettings
{
    [CommandArgument( 0, "<host>" )]
    [Description( "The IP address or hostname of the server to connect to" )]
    public string Host { get; init; } = default!;

    [CommandOption( "--no-logo" )]
    [Description( "Suppress the logo display (the $RACOON_NOLOGO environment variable is also supported)" )]
    public bool NoLogo { get; init; } = Environment.GetEnvironmentVariable( "RACOON_NOLOGO" )?.Equals( bool.TrueString, StringComparison.OrdinalIgnoreCase ) is true;

    [CommandArgument( 1, "[password]" )]
    [Description( "The RCON password for the server. If not provided, you will be prompted" )]
    public string Password { get; init; } = string.Empty;

    [CommandOption( "-p|--port" )]
    [DefaultValue( ( ushort )27015 )]
    [Description( "The remote port to connect to" )]
    public ushort Port { get; init; } = 27015;

    [CommandOption( "-t|--timeout" )]
    [DefaultValue( "00:00:30" )]
    [Description( "The timeout duration to use when connecting and sending commands" )]
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds( 30 );

    [CommandOption( "--use-koraktor" )]
    [DefaultValue( false )]
    [Description( "Whether to use the Koraktor Method for reading packets" )]
    public bool UseKoraktorMethod { get; init; } = false;
}
