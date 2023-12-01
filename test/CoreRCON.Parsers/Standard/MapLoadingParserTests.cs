using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Tests.Standard;

public sealed class MapLoadingParserTests
{
    [Theory(DisplayName = "Parser: matches and parses")]
    [MemberData(nameof(Data))]
    public void Parser_Matches_And_Parses(string value, MapLoading loading)
    {
        var parser = new MapLoadingParser();
        if (!parser.IsMatch(value))
        {
            Assert.Fail("Input value was not matched by parser.");
        }

        Assert.Equal(loading, parser.Parse(value));
    }

    [Fact(DisplayName = "ParserPool: gets parser")]
    public void ParserPool_Gets_Parser()
    {
        var parser = new ParserPool().Get<MapLoading>();

        Assert.NotNull(parser);
        Assert.IsType(typeof(MapLoadingParser), parser);
    }

    public static TheoryData<string, MapLoading> Data = new() {
        { @"Loading map ""de_dust2""", new("de_dust2") },
        { @"Loading map ""workshop\1234""", new(@"workshop\1234") },
    };
}
