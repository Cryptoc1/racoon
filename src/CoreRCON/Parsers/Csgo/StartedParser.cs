using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Csgo;

public sealed record Started : IParseable<Started>;

internal sealed class StartedParser : RegexParser<Started>
{
    public StartedParser() : base(@"^Started:  """"$")
    {
    }

    protected override Started Load(GroupCollection groups) => new();
}
