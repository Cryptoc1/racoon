using System.Net;
using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Tests.Standard;

public sealed class PlayerDisconnectedParserTests
{
    [Theory]
    [MemberData(nameof(Data))]
    public void Parser_Matches_And_Parses(string value, PlayerDisconnected disconnected)
    {
        var parser = new PlayerDisconnectedParser();
        if (!parser.IsMatch(value))
        {
            Assert.Fail("Input value was not matched by parser.");
        }

        Assert.Equal(disconnected, parser.Parse(value));
    }

    public static TheoryData<string, PlayerDisconnected> Data = new()
    {
        {
            @"""TEST<0><[U:0:123456789]><TERRORIST>"" disconnected (reason ""Unit Test"")",
            new(new(0, "TEST", "[U:0:123456789]", "TERRORIST"), "Unit Test")
        },
    };
}
