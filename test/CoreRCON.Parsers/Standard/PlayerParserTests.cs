using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Tests.Standard;

public sealed class PlayerParserTests
{
    [Theory]
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

    public static TheoryData<string, Player> Data = new() {
        { @"""TEST<0><[U:0:123456789]><TERRORIST>""", new(0, "TEST", "[U:0:123456789]", "TERRORIST") },
        { @"""TEST<1><[U:0:123456789]><TERRORIST>""", new(1, "TEST", "[U:0:123456789]", "TERRORIST") },
    };
}
