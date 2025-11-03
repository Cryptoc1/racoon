using System.ComponentModel;
using GitCredentialManager;
using Racoon.Tool.Internal;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Racoon.Tool.Commands;

internal sealed class ClearCredentialsCommand( ICredentialStore credentials ) : AsyncCommand<ClearCredentialsSettings>
{
    public override async Task<int> ExecuteAsync( CommandContext context, ClearCredentialsSettings settings, CancellationToken cancellation )
    {
        ArgumentNullException.ThrowIfNull( context );
        ArgumentNullException.ThrowIfNull( settings );

        if( !settings.Confirm && !await AnsiConsole.ConfirmAsync( "Are you sure you want to clear all credentials?", true, cancellation ) )
        {
            AnsiConsole.MarkupLine( $"[bold {RCONColor.Error}]Operation cancelled by user.[/]" );
            return -1;
        }

        var removed = AnsiConsole.Status().Spinner( Spinner.Known.Dots3 ).Start( "Clearing...", context =>
        {
            var count = 0;
            foreach( var account in credentials.GetRacoonAccounts() )
            {
                context.Status( $"Removing [bold]{account.Host}[/]..." );
                if( credentials.RemoveRacoon( account.Host ) )
                {
                    count++;
                }

                context.Status( $"Removed [bold]{account.Host}[/]!" );
            }

            return count;
        } );

        if( removed is 0 )
        {
            AnsiConsole.MarkupLine( $"[bold {RCONColor.Warning}]Nothing to do.[/]" );
            return 0;
        }

        AnsiConsole.MarkupLine( $"[bold {RCONColor.Success}]Credentials have been cleared[/]. (removed: {removed})" );
        return 0;
    }
}

internal sealed class ClearCredentialsSettings : ToolSettings
{
    [CommandOption( "-y|--confirm" )]
    [Description( "Do not prompt for confirmation" )]
    public bool Confirm { get; init; }
}