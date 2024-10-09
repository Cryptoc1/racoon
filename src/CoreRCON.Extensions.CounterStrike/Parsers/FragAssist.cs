﻿using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;
using CoreRCON.Parsers.Standard;

namespace CoreRCON.Extensions.CounterStrike.Parsers;

public record FragAssist(Player Assister, Player Killed) : IParseable<FragAssist>;

public sealed class FragAssistParser() : RegexParser<FragAssist>(@$"(?<Assister>{PlayerParser.Shared.Pattern}) assisted killing (?<Killed>{PlayerParser.Shared.Pattern})?")
{
    protected override FragAssist Convert(GroupCollection groups)
    {
        return new(
            PlayerParser.Shared.Parse(groups["Assister"].Value),
            PlayerParser.Shared.Parse(groups["Killed"].Value));
    }
}
