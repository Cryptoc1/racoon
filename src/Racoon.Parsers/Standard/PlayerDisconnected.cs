using System.Text.RegularExpressions;
using Racoon.Parsers.Abstractions;

namespace Racoon.Parsers.Standard;

public record PlayerDisconnected( Player Player, string Reason ) : IParsed<PlayerDisconnected>;

public sealed class PlayerDisconnectedParser( ParserPool parsers ) : RegexParser<PlayerDisconnected>( @$"(?<Player>{PlayerParser.Pattern}) disconnected\s?(\(reason ""(?<Reason>.*)""\))?" )
{
    private readonly IParser<Player> playerParser = parsers.Get<Player>();

    /// <inheritdoc />
    protected override PlayerDisconnected Convert( GroupCollection groups )
    {
        ArgumentNullException.ThrowIfNull( groups );

        return new(
            playerParser.Parse( groups[ "Player" ].Value ),
            groups[ "Reason" ].Value );
    }
}
