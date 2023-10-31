using CoreRCON.Parsers.CounterStrike;

namespace CoreRCON.Parsers.Tests.CounterStrike;

public sealed class TeamSideParserTests
{
    [Theory]
    [MemberData(nameof(Data))]
    public void Parser_Matches_And_Parses(string value, TeamSide side)
    {
        var parser = new TeamSideParser();
        if (!parser.IsMatch(value))
        {
            Assert.Fail("Input value was not matched by parser.");
        }

        Assert.Equal(side, parser.Parse(value));
    }

    public static TheoryData<string, TeamSide> Data = new()
    {
        { @"Team playing ""TEST"": TESTING", new("TEST", "TESTING")}
    };
}
