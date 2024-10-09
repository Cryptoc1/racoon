using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.Standard;

public record TeamChange(Player Player, string Team) : IParseable<TeamChange>;

public sealed class TeamChangeParser() : RegexParser<TeamChange>(@$"(?<Player>{PlayerParser.Shared.Pattern}) joined team ""(?<Team>.+?)""")
{
    protected override TeamChange Convert(GroupCollection groups)
    {
        return new(
            PlayerParser.Shared.Parse(groups["Player"].Value),
            groups["Team"].Value);
    }
}
