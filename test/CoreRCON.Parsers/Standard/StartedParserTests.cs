using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Tests.Standard;

public sealed class StartedParserTests
{
    [Fact(DisplayName = "Parser: matches and parses")]
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

    [Fact(DisplayName = "ParserPool: gets parser")]
    public void ParserPool_Gets_Parser()
    {
        var parser = new ParserPool().Get<Started>();

        Assert.NotNull(parser);
        Assert.IsType<StartedParser>(parser);
    }
}
