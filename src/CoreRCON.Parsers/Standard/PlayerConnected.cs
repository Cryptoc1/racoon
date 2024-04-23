using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.Standard;

public record PlayerConnected(IPEndPoint Host, Player Player) : IParseable<PlayerConnected>;

public sealed class PlayerConnectedParser() : RegexParser<PlayerConnected>(@$"(?<Player>{PlayerParser.Shared.Pattern}) connected, address ""(?<Host>.+?):(?<Port>\d+)""")
{
    protected override PlayerConnected Convert(GroupCollection groups) => new(
        new(IPAddress.Parse(groups["Host"].Value), int.Parse(groups["Port"].Value, CultureInfo.InvariantCulture)),
        PlayerParser.Shared.Parse(groups["Player"])
    );
}
