using System.Text.RegularExpressions;
using Racoon.Parsers.Abstractions;

namespace Racoon.Parsers.Standard;

public record TeamChange( Player Player, string Team ) : IParseable<TeamChange>;

public sealed class TeamChangeParser( ParserPool parsers ) : RegexParser<TeamChange>( @$"(?<Player>{PlayerParser.Pattern}) joined team ""(?<Team>.+?)""" )
{
    private readonly IParser<Player> playerParser = parsers.Get<Player>();

    /// <inheritdoc />
    protected override TeamChange Convert( GroupCollection groups )
    {
        ArgumentNullException.ThrowIfNull( groups );

        return new(
            playerParser.Parse( groups[ "Player" ].Value ),
            groups[ "Team" ].Value );
    }
}
