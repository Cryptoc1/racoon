using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Csgo;

public record TeamSide(string CurrentSide, string Team) : IParseable<TeamSide>;

public sealed class TeamSideParser : RegexParser<TeamSide>
{
    public TeamSideParser() : base(@"Team playing ""(?<side>.+?)"": (?<team>.*)")
    {
    }

    protected override TeamSide Load(GroupCollection groups) => new(groups["side"].Value, groups["team"].Value);
}
