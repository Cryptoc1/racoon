using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.Standard;

public record NameChange(string NewName, Player Player) : IParseable<NameChange>;

public sealed class NameChangeParser() : RegexParser<NameChange>(@$"(?<Player>{PlayerParser.Shared.Pattern}) changed name to ""(?<Name>.+?)""$")
{
    protected override NameChange Convert(GroupCollection groups)
    {
        return new(
            groups["Name"].Value,
            PlayerParser.Shared.Parse(groups["Player"].Value));
    }
}
