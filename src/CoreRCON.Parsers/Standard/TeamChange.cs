using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.Standard;

public record TeamChange(Player Player, string Team) : IParseable<TeamChange>;

public sealed class TeamChangeParser : RegexParser<TeamChange>
{
    public TeamChangeParser() : base(@$"(?<Player>{PlayerParser.Shared.Pattern}) joined team ""(?<Team>.+?)""")
    {
    }

    protected override TeamChange Load(GroupCollection groups) => new(
        PlayerParser.Shared.Parse(groups["Player"]),
        groups["Team"].Value
    );
}
