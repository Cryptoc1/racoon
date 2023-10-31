using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Tests.Standard;

public sealed class KillFeedParserTests
{
    [Theory]
    [MemberData(nameof(Data))]
    public void Parser_Matches_And_Parses(string value, KillFeed kill)
    {
        var parser = new KillFeedParser();
        if (!parser.IsMatch(value))
        {
            Assert.Fail("Input value was not matched by parser.");
        }

        Assert.Equal(kill, parser.Parse(value));
    }

    public static TheoryData<string, KillFeed> Data = new()
    {
        {
            @"""TEST<0><[U:0:123456789]><TERRORIST>"" killed ""TEST1<1><[U:0:123456789]><TERRORIST>"" with ""ak47""",
            new(new(1, "TEST1", "[U:0:123456789]", "TERRORIST"), new(0, "TEST", "[U:0:123456789]", "TERRORIST"), "ak47")
        },
    };
}
