using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard;

public record NameChange(string NewName, Player Player) : IParseable<NameChange>;

public sealed class NameChangeParser : RegexParser<NameChange>
{
    public NameChangeParser() : base(@$"(?<Player>{PlayerParser.Shared.Pattern}) changed name to ""(?<Name>.+?)""$")
    {
    }

    protected override NameChange Load(GroupCollection groups) => new(
        groups["Name"].Value,
        PlayerParser.Shared.Parse(groups["Player"])
    );
}
