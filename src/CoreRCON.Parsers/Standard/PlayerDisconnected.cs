using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.Standard;

public record PlayerDisconnected(Player Player, string Reason) : IParseable<PlayerDisconnected>;

public sealed class PlayerDisconnectedParser() : RegexParser<PlayerDisconnected>(@$"(?<Player>{PlayerParser.Shared.Pattern}) disconnected\s?(\(reason ""(?<Reason>.*)""\))?")
{
    protected override PlayerDisconnected Convert(GroupCollection groups) => new(
        PlayerParser.Shared.Parse(groups["Player"]),
        groups["Reason"].Value
    );
}
