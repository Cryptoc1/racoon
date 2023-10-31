using CoreRCON.Parsers.Standard;
using Microsoft.VisualBasic;

namespace CoreRCON.Parsers.Tests.Standard;

public sealed class NameChangeParserTests
{
    [Theory]
    [MemberData(nameof(Data))]
    public void Parser_Matches_And_Parses(string value, NameChange change)
    {
        var parser = new NameChangeParser();
        if (!parser.IsMatch(value))
        {
            Assert.Fail("Input value was not matched by parser.");
        }

        Assert.Equal(change, parser.Parse(value));
    }

    public static TheoryData<string, NameChange> Data = new()
    {
        { @"""TEST<0><[U:0:123456789]><TERRORIST>"" changed name to ""TESTING""", new("TESTING", new(0, "TEST", "[U:0:123456789]", "TERRORIST")) }
    };
}
