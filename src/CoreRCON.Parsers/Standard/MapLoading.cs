using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.Standard;

public record MapLoading(string Map) : IParseable<MapLoading>;

public sealed class MapLoadingParser() : RegexParser<MapLoading>(@"^Loading map ""(?<Map>.+?)""")
{
    protected override MapLoading Convert(GroupCollection groups)
    {
        return new(groups["Map"].Value);
    }
}
