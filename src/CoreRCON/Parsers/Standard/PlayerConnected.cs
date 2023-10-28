using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard;

public record PlayerConnected(string Host, Player Player) : IParseable<PlayerConnected>;

public sealed class PlayerConnectedParser : RegexParser<PlayerConnected>
{
    public PlayerConnectedParser() : base(@$"(?<Player>{PlayerParser.Shared.Pattern}) connected, address ""(?<Host>.+?)""")
    {
    }

    protected override PlayerConnected Load(GroupCollection groups) => new(
        groups["Host"].Value,
        PlayerParser.Shared.Parse(groups["Player"])
    );
}
