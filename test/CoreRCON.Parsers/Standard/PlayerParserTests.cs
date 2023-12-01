using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Tests.Standard;

public sealed class PlayerParserTests
{
    [Theory(DisplayName = "Parser: matches and parses")]
    [MemberData(nameof(Data))]
    public void Parser_Matches_And_Parses(string value, Player player)
    {
        var parser = new PlayerParser();
        if (!parser.IsMatch(value))
        {
            Assert.Fail("Input value was not matched by parser.");
        }

        Assert.Equal(player, parser.Parse(value));
    }

    [Fact(DisplayName = "ParserPool: gets parser")]
    public void ParserPool_Gets_Parser()
    {
        var parser = new ParserPool().Get<Player>();

        Assert.NotNull(parser);
        Assert.IsType(typeof(PlayerParser), parser);
    }

    public static TheoryData<string, Player> Data = new() {
        { @"""TEST<0><[U:0:123456789]><TERRORIST>""", new(0, "TEST", "[U:0:123456789]", "TERRORIST") },
        { @"""TEST<1><[U:0:123456789]><TERRORIST>""", new(1, "TEST", "[U:0:123456789]", "TERRORIST") },
    };
}
