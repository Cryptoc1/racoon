using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.Standard;

public sealed record Started : IParseable<Started>;

public sealed class StartedParser() : RegexParser<Started>(@"^Started:  """"$")
{
    protected override Started Convert(GroupCollection groups)
    {
        return new();
    }
}
