using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;
using CoreRCON.Parsers.Standard;

namespace CoreRCON.Extensions.CounterStrike.Parsers;

public record Frag(bool IsHeadshot, Player Killed, Player Killer, string Weapon) : IParseable<Frag>;

public sealed class FragParser : RegexParser<Frag>
{
    // TODO: parse position (square bracket content)
    public FragParser() : base(@$"(?<Killer>{PlayerParser.Shared.Pattern}) \[.*?\] killed (?<Killed>{PlayerParser.Shared.Pattern}) \[.*?\] with ""(?<Weapon>.+?)""\s?(?<Headshot>\(headshot\))?")
    {
    }

    protected override Frag Convert(GroupCollection groups) => new(
        groups["Headshot"].Success,
        PlayerParser.Shared.Parse(groups["Killed"]),
        PlayerParser.Shared.Parse(groups["Killer"]),
        groups["Weapon"].Value
    );
}
