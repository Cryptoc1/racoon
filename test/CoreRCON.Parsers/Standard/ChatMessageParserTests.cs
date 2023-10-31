using System.Reflection;
using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Tests.Standard;

public sealed class ChatMessageParserTests
{
    [Theory]
    [MemberData(nameof(TestData))]
    public void Parser_Matches_And_Parses(string value, ChatMessage message)
    {
        var parser = new ChatMessageParser();
        if (!parser.IsMatch(value))
        {
            Assert.Fail("Input value was not matched by parser.");
        }

        Assert.Equal(message, parser.Parse(value));
    }

    public static TheoryData<string, ChatMessage> TestData = new() {
        { @"""TEST<0><[U:0:123456789]><TERRORIST>"" say ""test""", new(MessageChannel.All, "test", new(0, "TEST", "[U:0:123456789]", "TERRORIST")) },
        { @"""TEST<0><[U:0:123456789]><TERRORIST>"" say_team ""test""", new(MessageChannel.Team, "test", new(0, "TEST", "[U:0:123456789]", "TERRORIST")) },
    };
}
