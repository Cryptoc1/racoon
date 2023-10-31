using System.Net;
using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Tests.Standard;

public sealed class PlayerConnectedParserTests
{
    [Theory]
    [MemberData(nameof(Data))]
    public void Parser_Matches_And_Parses(string value, PlayerConnected connected)
    {
        var parser = new PlayerConnectedParser();
        if (!parser.IsMatch(value))
        {
            Assert.Fail("Input value was not matched by parser.");
        }

        Assert.Equal(connected, parser.Parse(value));
    }

    public static TheoryData<string, PlayerConnected> Data = new()
    {
        {
            @"""TEST<0><[U:0:123456789]><TERRORIST>"" connected, address ""127.0.0.1:27015""",
            new(new(IPAddress.Loopback, 27015), new(0, "TEST", "[U:0:123456789]", "TERRORIST"))
        },
    };
}
