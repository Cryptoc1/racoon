using System.ComponentModel;
using GitCredentialManager;
using Racoon.Tool.Internal;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Racoon.Tool.Commands;

internal sealed class RemoveCredentialCommand( ICredentialStore credentials ) : AsyncCommand<RemoveCredentialSettings>
{
    public override async Task<int> ExecuteAsync( CommandContext context, RemoveCredentialSettings settings, CancellationToken cancellation )
    {
        ArgumentNullException.ThrowIfNull( context );
        ArgumentNullException.ThrowIfNull( settings );

        if( !settings.Confirm && !await AnsiConsole.ConfirmAsync( $"Are you sure you want to remove the credential for '{settings.Host}'?", true, cancellation ) )
        {
            AnsiConsole.MarkupLine( $"[bold {RCONColor.Error}]Operation cancelled by user.[/]" );
            return -1;
        }

        if( !credentials.RemoveRacoon( settings.Host ) )
        {
            AnsiConsole.MarkupLine( $"[bold {RCONColor.Warning}]Credential does not exist.[/]" );
            return -1;
        }

        AnsiConsole.MarkupLine( $"[bold {RCONColor.Success}]Credential has been removed.[/]" );
        return 0;
    }
}

internal sealed class RemoveCredentialSettings : ToolSettings
{
    [CommandArgument( 0, "<host>" )]
    [Description( "The IP address or hostname of the server to connect to" )]
    public string Host { get; init; } = default!;

    [CommandOption( "-y|--confirm" )]
    [Description( "Do not prompt for confirmation" )]
    public bool Confirm { get; init; }
}