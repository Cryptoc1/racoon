using CoreRCON.Parsers.CounterStrike;

namespace CoreRCON.Parsers.Tests.CounterStrike;

public sealed class StartedParserTests
{
    [Fact]
    public void Parser_Matches_And_Parses()
    {
        var value = @"Started:  """"";

        var parser = new StartedParser();
        if (!parser.IsMatch(value))
        {
            Assert.Fail("Input value was not matched by parser.");
        }

        Assert.Equal(new Started(), parser.Parse(value));
    }
}
