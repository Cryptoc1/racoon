using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.CounterStrike;

public record MapLoading(string Map) : IParseable<MapLoading>;

public sealed class MapLoadingParser : RegexParser<MapLoading>
{
    public MapLoadingParser() : base(@"^Loading map ""(?<Map>.+?)""")
    {
    }

    protected override MapLoading Convert(GroupCollection groups) => new(groups["Map"].Value);
}
