using System.ComponentModel;
using System.Net;
using GitCredentialManager;
using Racoon.Extensions;
using Racoon.Tool.Internal;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Racoon.Tool.Commands;

internal sealed class ConnectCommand( ICredentialStore credentials ) : AsyncCommand<ConnectSettings>
{
    public override async Task<int> ExecuteAsync( CommandContext context, ConnectSettings settings, CancellationToken cancellation )
    {
        ArgumentNullException.ThrowIfNull( context );
        ArgumentNullException.ThrowIfNull( settings );
        AnsiConsole.Console.WriteLogo( settings );

        var host = await ResolveHost( settings.Host );
        if( host is null )
        {
            AnsiConsole.Write( $"[bold {RCONColor.Error}]<!>[/] Provided host could not be resolved." );
            return ShellExitCode.InvalidHost;
        }

        var password = ResolvePassword(
            credentials,
            settings.Host,
            settings.Password );

        using var console = new RCONClient(
            host,
            settings.Port,
            password,
            new( settings.Timeout, settings.UseKoraktorMethod ) );

        using var aborted = CancellationTokenSource.CreateLinkedTokenSource( cancellation );
        console.Disconnected += ( _, _ ) => aborted.Cancel();

        if( !await TryConnect( console ) )
        {
            password = default;
            return ShellExitCode.FailedToConnect;
        }

        if( settings.SaveCredentials )
        {
            credentials.UpdateRacoonPassword( settings.Host, password );
        }

        password = default;
        WritePromptHelp( true );

        using var prompt = new RCONPrompt( await console.Status( aborted.Token ) );
        while( !aborted.IsCancellationRequested && console.State is RCONClientState.Authenticated )
        {
            var command = await prompt.ShowAsync( AnsiConsole.Console, aborted.Token );
            if( command[ 0 ] is not ':' )
            {
                try
                {
                    var result = await console.SendCommandAsync( command, aborted.Token );
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

        static void WritePromptHelp( bool padding = false )
        {
            if( padding )
            {
                AnsiConsole.WriteLine();
            }

            AnsiConsole.MarkupLine( $"Use [bold {RCONColor.Hint}]:q[/] to exit." );
            AnsiConsole.MarkupLine( $"Use [bold {RCONColor.Hint}]:clear[/] to clear the screen." );
            AnsiConsole.MarkupLine( $"Use [bold {RCONColor.Hint}]arrow up[/]/[bold tan]arrow down[/] to traverse history." );
            AnsiConsole.MarkupLine( $"Use [bold {RCONColor.Hint}]:help[/] to display this help text." );

            if( padding )
            {
                AnsiConsole.WriteLine();
            }
        }
    }

    private static string ResolvePassword( ICredentialStore credentials, string host, string? password )
    {
        ArgumentNullException.ThrowIfNull( credentials );
        ArgumentException.ThrowIfNullOrWhiteSpace( host );

        if( !string.IsNullOrWhiteSpace( password ) )
        {
            return password;
        }

        if( credentials.TryGetRacoonPassword( host, out password ) )
        {
            return password;
        }

        return AnsiConsole.Prompt( new TextPrompt<string>( $"Password>" ).Secret() );
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
            await AnsiConsole.Status().Spinner( Spinner.Known.Dots3 ).StartAsync( "Connecting...", async context =>
            {
                await Task.Delay( retry is 0 ? 250 : 1250 );
                await console.ConnectAsync();
            } );
        }
        catch( RCONException )
        {
            return AnsiConsole.Confirm( $"[bold {RCONColor.Error}]<!>[/] Failed to connect to the host, retry?" )
                && await TryConnect( console, ++retry );
        }

        return true;
    }
}

internal static class ShellExitCode
{
    public const int Disconnected = -50;
    public const int FailedToConnect = -100;
    public const int InvalidHost = -75;
}

internal sealed class ConnectSettings : ToolSettings
{
    [CommandArgument( 0, "<host>" )]
    [Description( "The IP address or hostname of the server to connect to" )]
    public string Host { get; init; } = default!;

    [CommandArgument( 1, "[password]" )]
    [Description( "The RCON password for the server. If not provided, you will be prompted" )]
    public string Password { get; init; } = string.Empty;

    [CommandOption( "-p|--port" )]
    [DefaultValue( ( ushort )27015 )]
    [Description( "The remote port to connect to" )]
    public ushort Port { get; init; } = 27015;

    [CommandOption( "-s|--save" )]
    [DefaultValue( false )]
    [Description( "Save the credentials to the System's Keyring." )]
    public bool SaveCredentials { get; init; }

    [CommandOption( "-t|--timeout" )]
    [DefaultValue( "00:00:30" )]
    [Description( "The timeout duration to use when connecting and sending commands" )]
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds( 30 );

    [CommandOption( "--use-koraktor" )]
    [DefaultValue( false )]
    [Description( "Whether to use the Koraktor Method for reading packets" )]
    public bool UseKoraktorMethod { get; init; } = false;
}
