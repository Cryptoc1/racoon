using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;
using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.CounterStrike;

public record Frag(bool Headshot, Player Killed, Player Killer, string Weapon) : IParseable<Frag>;

public sealed class FragParser : RegexParser<Frag>
{
    // TODO: parse position (square bracket content)
    public FragParser() : base(@$"(?<Killer>{PlayerParser.Shared.Pattern}) \[.*?\] killed (?<Killed>{PlayerParser.Shared.Pattern}) \[.*?\] with ""(?<Weapon>.+?)""\s?(?<Headshot>\(headshot\))?")
    {
    }

    protected override Frag Load(GroupCollection groups) => new(
        groups["Headshot"].Success,
        PlayerParser.Shared.Parse(groups["Killed"]),
        PlayerParser.Shared.Parse(groups["Killer"]),
        groups["Weapon"].Value
    );
}
