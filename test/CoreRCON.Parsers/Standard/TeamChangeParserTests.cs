using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Tests.Standard;

public sealed class TeamChangeParserTets
{
    [Theory(DisplayName = "Parser: matches and parses")]
    [MemberData(nameof(Data))]
    public void Parser_Matches_And_Parses(string value, TeamChange change)
    {
        var parser = new TeamChangeParser();
        if (!parser.IsMatch(value))
        {
            Assert.Fail("Input value was not matched by parser.");
        }

        Assert.Equal(change, parser.Parse(value));
    }

    [Fact(DisplayName = "ParserPool: gets parser")]
    public void ParserPool_Gets_Parser()
    {
        var parser = new ParserPool().Get<TeamChange>();

        Assert.NotNull(parser);
        Assert.IsType<TeamChangeParser>(parser);
    }

    public static readonly TheoryData<string, TeamChange> Data = new()
    {
        { @"""TEST<0><[U:0:123456789]><TERRORIST>"" joined team ""Terrorists""", new(new(0, "TEST", "[U:0:123456789]", "TERRORIST"), "Terrorists") },
    };
}
