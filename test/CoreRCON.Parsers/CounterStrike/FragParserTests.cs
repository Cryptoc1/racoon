using CoreRCON.Parsers.CounterStrike;

namespace CoreRCON.Parsers.Tests.CounterStrike;

public sealed class FragParserTests
{
    [Theory]
    [MemberData(nameof(Data))]
    public void Parser_Matches_And_Parses(string value, Frag frag)
    {
        var parser = new FragParser();
        if (!parser.IsMatch(value))
        {
            Assert.Fail("Input value was not matched by parser.");
        }

        Assert.Equal(frag, parser.Parse(value));
    }

    public static TheoryData<string, Frag> Data = new()
    {
        {
            @"""TEST<0><[U:0:123456789]><TERRORIST>"" [0] killed ""TEST1<1><[U:0:123456789]><TERRORIST>"" [0] with ""ak47""",
            new(false, new(1, "TEST1", "[U:0:123456789]", "TERRORIST"), new(0, "TEST", "[U:0:123456789]", "TERRORIST"), "ak47")
        },
        {
            @"""TEST<0><[U:0:123456789]><TERRORIST>"" [0] killed ""TEST1<1><[U:0:123456789]><TERRORIST>"" [0] with ""ak47"" (headshot)",
            new(true, new(1, "TEST1", "[U:0:123456789]", "TERRORIST"), new(0, "TEST", "[U:0:123456789]", "TERRORIST"), "ak47")
        },
    };
}
