using System.Globalization;
using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Csgo;

public record GameOverScore(int CTScore, int TScore) : IParseable<GameOverScore>;

public sealed class GameOverScoreParser : RegexParser<GameOverScore>
{
    public GameOverScoreParser() : base(@"Game Over: .*? .*? .*? score (?<ct_score>\d+):(?<t_score>\d+) (after \d+ min)?")
    {
    }

    protected override GameOverScore Load(GroupCollection groups) => new(
        int.Parse(groups["ct_score"].Value, CultureInfo.InvariantCulture),
        int.Parse(groups["t_score"].Value, CultureInfo.InvariantCulture)
    );
}
