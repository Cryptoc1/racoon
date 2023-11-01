using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.CounterStrike;

public record TeamSide(string CurrentSide, string Team) : IParseable<TeamSide>;

public sealed class TeamSideParser : RegexParser<TeamSide>
{
    public TeamSideParser() : base(@"Team playing ""(?<side>.+?)"": (?<team>.*)")
    {
    }

    protected override TeamSide Convert(GroupCollection groups) => new(groups["side"].Value, groups["team"].Value);
}
