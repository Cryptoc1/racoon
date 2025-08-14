using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Racoon.Parsers.Abstractions;

namespace Racoon.Parsers.Standard;

public record Player( int ClientId, string Name, string SteamId, string Team ) : IParseable<Player>;

public sealed class PlayerParser( ) : RegexParser<Player>( Pattern )
{
    /// <summary> The default regex pattern for identifying player text. </summary>
    [StringSyntax( StringSyntaxAttribute.Regex )]
    public const string Pattern = @"""(?<Name>.+?(?:<.*>)*)<(?<ClientID>\d+?)><(?<SteamID>.+?)><(?<Team>.+?)?>""";

    /// <inheritdoc />
    protected override Player Convert( GroupCollection groups )
    {
        ArgumentNullException.ThrowIfNull( groups );

        return new(
            int.Parse( groups[ "ClientID" ].Value, CultureInfo.InvariantCulture ),
            groups[ "Name" ].Value,
            groups[ "SteamID" ].Value,
            groups[ "Team" ].Value );
    }
}
