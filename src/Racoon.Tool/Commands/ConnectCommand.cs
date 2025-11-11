using System.ComponentModel;
using System.Net;
using GitCredentialManager;
using Racoon.Extensions;
using Racoon.Tool.Internal;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Racoon.Tool.Commands;

internal sealed class ConnectCommand( IAnsiConsole stdout, ICredentialStore credentials ) : AsyncCommand<ConnectSettings>
{
    public override async Task<int> ExecuteAsync( CommandContext context, ConnectSettings settings, CancellationToken cancellation )
    {
        ArgumentNullException.ThrowIfNull( context );
        ArgumentNullException.ThrowIfNull( settings );

        var host = await ResolveHost( stdout, settings.Host );
        if( host is null )
        {
            stdout.Write( $"[bold {RCONColor.Error}]<!>[/] Provided host could not be resolved." );
            return ShellExitCode.InvalidHost;
        }

        var password = ResolvePassword(
            stdout,
            credentials,
            settings );

        using var console = new RCONClient(
            host,
            settings.Port,
            password,
            new( settings.Timeout, settings.UseKoraktorMethod ) );

        using var aborted = CancellationTokenSource.CreateLinkedTokenSource( cancellation );
        console.Disconnected += ( _, _ ) => aborted.Cancel();

        if( !await TryConnect( stdout, console, settings.Retry ) )
        {
            password = default;
            return ShellExitCode.FailedToConnect;
        }

        if( settings.SaveCredentials )
        {
            credentials.UpdateRacoonPassword( settings.Host, password );
        }

        password = default;
        WritePromptHelp( stdout, true );

        using var prompt = new RCONPrompt( await console.Status( aborted.Token ) );
        while( !aborted.IsCancellationRequested && console.State is RCONClientState.Authenticated )
        {
            var command = await prompt.ShowAsync( stdout, aborted.Token );
            if( command[ 0 ] is not ':' )
            {
                try
                {
                    var result = await console.SendCommandAsync(
                        command,
                        aborted.Token );

                    prompt.Error = result.StartsWith(
                        "unknown command",
                        StringComparison.OrdinalIgnoreCase );

                    stdout.WriteLine( result );
                }
                catch( RCONCommandException exception )
                {
                    prompt.Error = true;

#pragma warning disable IL3050
                    stdout.WriteException( exception );
#pragma warning restore IL3050
                }
            }

            if( command.Equals( ":clear", StringComparison.OrdinalIgnoreCase ) )
            {
                stdout.Clear();
            }

            if( command.Equals( ":help", StringComparison.OrdinalIgnoreCase ) )
            {
                WritePromptHelp( stdout );
            }

            if( command.Equals( ":q", StringComparison.OrdinalIgnoreCase ) )
            {
                return 0;
            }

            if( command.Equals( ":reset", StringComparison.OrdinalIgnoreCase ) )
            {
                prompt.Reset();

                stdout.MarkupLine( $"[bold {RCONColor.Success}]Prompt has been reset.[/]" );
                continue;
            }

            prompt.AddHistory( command );
        }

        return ShellExitCode.Disconnected;

        static void WritePromptHelp( IAnsiConsole stdout, bool padding = false )
        {
            if( padding )
            {
                stdout.WriteLine();
            }

            stdout.MarkupLine( $"Use [bold {RCONColor.Hint}]:q[/] to exit." );
            stdout.MarkupLine( $"Use [bold {RCONColor.Hint}]:clear[/] to clear the screen." );
            stdout.MarkupLine( $"Use [bold {RCONColor.Hint}]:reset[/] to clear prompt history." );
            stdout.MarkupLine( $"Use [bold {RCONColor.Hint}]arrow up[/]/[bold tan]arrow down[/] to traverse history." );
            stdout.MarkupLine( $"Use [bold {RCONColor.Hint}]:help[/] to display this help text." );

            if( padding )
            {
                stdout.WriteLine();
            }
        }
    }

    private static string ResolvePassword( IAnsiConsole stdout, ICredentialStore credentials, ConnectSettings settings )
    {
        ArgumentNullException.ThrowIfNull( credentials );
        ArgumentNullException.ThrowIfNull( settings );
        ArgumentException.ThrowIfNullOrWhiteSpace( settings.Host );

        if( !string.IsNullOrWhiteSpace( settings.Password ) )
        {
            return settings.Password;
        }

        // NOTE: if the user requested to save creds, we should always prompt for the password
        if( !settings.SaveCredentials && credentials.TryGetRacoonPassword( settings.Host, out var password ) )
        {
            return password;
        }

        return stdout.Prompt( new TextPrompt<string>( $"Password>" ).Secret() );
    }

    private static async Task<IPAddress?> ResolveHost( IAnsiConsole stdout, string host )
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
            return stdout.Prompt(
                new SelectionPrompt<IPAddress>()
                    .Title( $"Host [green]{host}[/] resolved to multiple addresses>" )
                    .AddChoices( addresses ) );
        }

        return default;
    }

    private static async Task<bool> TryConnect( IAnsiConsole stdout, RCONClient console, int retries, int retry = default )
    {
        try
        {
            await stdout.Status().Spinner( Spinner.Known.Dots3 ).StartAsync( "Connecting...", async context =>
            {
                await Task.Delay( retry is 0 ? 250 : 1250 );
                await console.ConnectAsync();
            } );
        }
        catch( RCONException )
        {
            if( retry < retries )
            {
                return stdout.Confirm( $"[bold {RCONColor.Error}]<!>[/] Failed to connect to the host, retry?" )
                    && await TryConnect( stdout, console, retries, ++retry );
            }

            throw;
        }

        return true;
    }
}

internal static class ShellExitCode
{
    public const int Disconnected = 76;
    public const int FailedToConnect = 69;
    public const int InvalidHost = 68;
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

    [CommandOption( "-r|--retry" )]
    [DefaultValue( 3 )]
    [Description( "The # of retries to make when attempting to connect." )]
    public int Retry { get; init; } = 3;

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
