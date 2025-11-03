using GitCredentialManager;
using Racoon.Tool.Internal;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Racoon.Tool.Commands;

internal sealed class ListCredentialsCommand( ICredentialStore credentials ) : AsyncCommand<ToolSettings>
{
    public override Task<int> ExecuteAsync( CommandContext context, ToolSettings settings, CancellationToken cancellation )
    {
        ArgumentNullException.ThrowIfNull( context );
        ArgumentNullException.ThrowIfNull( settings );

        var accounts = AnsiConsole.Status().Spinner( Spinner.Known.Dots3 ).Start( "Loading...", context =>
        {
            var tree = new Tree( "" );
            foreach( var account in credentials.GetRacoonAccounts() )
            {
                context.Status( $"Loading [bold]{account}[/]..." );

                var node = tree.AddNode( account.Host );
                node.AddNode( $"Created: {account.CreatedAt.LocalDateTime}" );
                if( account.UpdatedAt.HasValue )
                {
                    node.AddNode( $"Updated: {account.UpdatedAt.Value.LocalDateTime}" );
                }

                context.Status( $"Loaded [bold]{account}[/]!" );
            }

            context.Status( $"[bold {RCONColor.Success}]Loaded![/]" );
            return tree;
        } );

        AnsiConsole.Write( accounts );
        return Task.FromResult( 0 );
    }
}