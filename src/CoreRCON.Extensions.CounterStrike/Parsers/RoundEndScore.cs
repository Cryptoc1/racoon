using System.Globalization;
using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Extensions.CounterStrike.Parsers;

public record RoundEndScore(int CTScore, int TScore, string WinningTeam) : IParseable<RoundEndScore>;

public sealed class RoundEndScoreParser() : RegexParser<RoundEndScore>(@"Team ""(?<winning_team>.+?)"" triggered ""SFUI_Notice_.+?_Win"" \(CT ""(?<ct_score>\d+)""\) \(T ""(?<t_score>\d+)""\)")
{
    protected override RoundEndScore Convert(GroupCollection groups)
    {
        return new(
            int.Parse(groups["ct_score"].Value, CultureInfo.InvariantCulture),
            int.Parse(groups["t_score"].Value, CultureInfo.InvariantCulture),
            groups["winning_team"].Value);
    }
}
