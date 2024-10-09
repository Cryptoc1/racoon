﻿using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.Standard;

public record KillFeed(Player Killed, Player Killer, string Weapon) : IParseable<KillFeed>;

public sealed class KillFeedParser() : RegexParser<KillFeed>(@$"(?<Killer>{PlayerParser.Shared.Pattern}) killed (?<Killed>{PlayerParser.Shared.Pattern}) with ""(?<Weapon>.+?)""")
{
    protected override KillFeed Convert(GroupCollection groups)
    {
        return new(
            PlayerParser.Shared.Parse(groups["Killed"].Value),
            PlayerParser.Shared.Parse(groups["Killer"].Value),
            groups["Weapon"].Value);
    }
}
