﻿using System.Globalization;
using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Extensions.CounterStrike.Parsers;

public record GameOverScore(int CTScore, int TScore) : IParseable<GameOverScore>;

public sealed class GameOverScoreParser() : RegexParser<GameOverScore>(@"Game Over: .*? .*? .*? score (?<ct_score>\d+):(?<t_score>\d+) (after \d+ min)?")
{
    protected override GameOverScore Convert(GroupCollection groups)
    {
        return new(
            int.Parse(groups["ct_score"].Value, CultureInfo.InvariantCulture),
            int.Parse(groups["t_score"].Value, CultureInfo.InvariantCulture));
    }
}
