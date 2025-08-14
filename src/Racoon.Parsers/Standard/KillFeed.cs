using System.Text.RegularExpressions;
using Racoon.Parsers.Abstractions;

namespace Racoon.Parsers.Standard;

public record KillFeed( Player Killed, Player Killer, string Weapon ) : IParseable<KillFeed>;

public sealed class KillFeedParser( ParserPool parsers ) : RegexParser<KillFeed>( @$"(?<Killer>{PlayerParser.Pattern}) killed (?<Killed>{PlayerParser.Pattern}) with ""(?<Weapon>.+?)""" )
{
    private readonly IParser<Player> playerParser = parsers.Get<Player>();

    /// <inheritdoc />
    protected override KillFeed Convert( GroupCollection groups )
    {
        ArgumentNullException.ThrowIfNull( groups );

        return new(
            playerParser.Parse( groups[ "Killed" ].Value ),
            playerParser.Parse( groups[ "Killer" ].Value ),
            groups[ "Weapon" ].Value );
    }
}
