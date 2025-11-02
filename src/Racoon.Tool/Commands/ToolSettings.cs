using System.ComponentModel;
using Spectre.Console.Cli;

namespace Racoon.Tool.Commands;

internal class ToolSettings : CommandSettings
{
    [CommandOption( "--no-logo" )]
    [Description( "Suppress the logo display (the $RACOON_NOLOGO environment variable is also supported)" )]
    public bool NoLogo { get; init; } = Environment.GetEnvironmentVariable( "RACOON_NOLOGO" )?.Equals( bool.TrueString, StringComparison.OrdinalIgnoreCase ) is true;
}