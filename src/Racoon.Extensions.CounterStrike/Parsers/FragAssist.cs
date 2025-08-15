using System.Text.RegularExpressions;
using Racoon.Parsers;
using Racoon.Parsers.Abstractions;
using Racoon.Parsers.Standard;

namespace Racoon.Extensions.CounterStrike.Parsers;

public record FragAssist( Player Assister, Player Killed ) : IParsed<FragAssist>;

public sealed class FragAssistParser( ParserPool parsers ) : RegexParser<FragAssist>( @$"(?<Assister>{PlayerParser.Pattern}) assisted killing (?<Killed>{PlayerParser.Pattern})?" )
{
    private readonly IParser<Player> playerParser = parsers.Get<Player>();

    /// <inheritdoc />
    protected override FragAssist Convert( GroupCollection groups )
    {
        ArgumentNullException.ThrowIfNull( groups );

        return new(
            playerParser.Parse( groups[ "Assister" ].Value ),
            playerParser.Parse( groups[ "Killed" ].Value ) );
    }
}
