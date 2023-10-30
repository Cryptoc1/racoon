using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Csgo;

public sealed record MapLoading(string Map) : IParseable<MapLoading>;

public sealed class MapLoadingParser : RegexParser<MapLoading>
{
    public MapLoadingParser() : base(@"^Loading map ""(?<Map>.+?)""")
    {
    }

    protected override MapLoading Load(GroupCollection groups) => new(groups["Map"].Value);
}
