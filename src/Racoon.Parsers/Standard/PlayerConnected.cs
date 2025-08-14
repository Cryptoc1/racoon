using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Racoon.Parsers.Abstractions;

namespace Racoon.Parsers.Standard;

public record PlayerConnected( IPEndPoint Host, Player Player ) : IParseable<PlayerConnected>;

public sealed class PlayerConnectedParser( ParserPool parsers ) : RegexParser<PlayerConnected>( @$"(?<Player>{PlayerParser.Pattern}) connected, address ""(?<Host>.+?):(?<Port>\d+)""" )
{
    private readonly IParser<Player> playerParser = parsers.Get<Player>();

    /// <inheritdoc />
    protected override PlayerConnected Convert( GroupCollection groups )
    {
        ArgumentNullException.ThrowIfNull( groups );

        return new(
            new( IPAddress.Parse( groups[ "Host" ].Value ), int.Parse( groups[ "Port" ].Value, CultureInfo.InvariantCulture ) ),
            playerParser.Parse( groups[ "Player" ].Value ) );
    }
}
