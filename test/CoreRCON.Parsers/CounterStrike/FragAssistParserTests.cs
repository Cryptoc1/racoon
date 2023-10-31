using CoreRCON.Parsers.CounterStrike;

namespace CoreRCON.Parsers.Tests.CounterStrike;

public sealed class FragAssistParserTests
{
    [Theory]
    [MemberData(nameof(Data))]
    public void Parser_Matches_And_Parses(string value, FragAssist assist)
    {
        var parser = new FragAssistParser();
        if (!parser.IsMatch(value))
        {
            Assert.Fail("Input value was not matched by parser.");
        }

        Assert.Equal(assist, parser.Parse(value));
    }

    public static TheoryData<string, FragAssist> Data = new()
    {
        {
            @"""TEST<0><[U:0:123456789]><TERRORIST>"" assisted killing ""TEST1<1><[U:0:123456789]><TERRORIST>""",
            new(new(0, "TEST", "[U:0:123456789]", "TERRORIST"), new(1, "TEST1", "[U:0:123456789]", "TERRORIST"))
        }
    };
}
