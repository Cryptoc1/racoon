using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard;

public record PlayerDisconnected(Player Player, string Reason) : IParseable<PlayerDisconnected>;

public sealed class PlayerDisconnectedParser : RegexParser<PlayerDisconnected>
{
    public PlayerDisconnectedParser() : base(@$"(?<Player>{PlayerParser.Shared.Pattern}) disconnected\s?(\(reason ""(?<Reason>.*)""\))?")
    {
    }

    protected override PlayerDisconnected Load(GroupCollection groups) => new(
        PlayerParser.Shared.Parse(groups["Player"]),
        groups["Reason"].Value
    );
}
