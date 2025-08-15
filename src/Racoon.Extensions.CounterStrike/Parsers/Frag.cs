using System.Text.RegularExpressions;
using Racoon.Parsers;
using Racoon.Parsers.Abstractions;
using Racoon.Parsers.Standard;

namespace Racoon.Extensions.CounterStrike.Parsers;

public record Frag( bool IsHeadshot, Player Killed, Player Killer, string Weapon ) : IParsed<Frag>;

// TODO: parse position (square bracket content)
public sealed class FragParser( ParserPool parsers ) : RegexParser<Frag>( @$"(?<Killer>{PlayerParser.Pattern}) \[.*?\] killed (?<Killed>{PlayerParser.Pattern}) \[.*?\] with ""(?<Weapon>.+?)""\s?(?<Headshot>\(headshot\))?" )
{
    private readonly IParser<Player> playerParser = parsers.Get<Player>();

    /// <inheritdoc />
    protected override Frag Convert( GroupCollection groups )
    {
        ArgumentNullException.ThrowIfNull( groups );

        return new(
            groups[ "Headshot" ].Success,
            playerParser.Parse( groups[ "Killed" ].Value ),
            playerParser.Parse( groups[ "Killer" ].Value ),
            groups[ "Weapon" ].Value );
    }
}
