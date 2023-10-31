using System.Reflection;
using CoreRCON.Parsers.CounterStrike;
using CoreRCON.Parsers.Standard;
using Xunit.Sdk;

namespace CoreRCON.Parsers.Tests.CounterStrike;

public sealed class MapLoadingParserTests
{
    [Theory]
    [MemberData(nameof(Data))]
    public void Parser_Matches_And_Parses(string value, MapLoading loading)
    {
        var parser = new MapLoadingParser();
        if (!parser.IsMatch(value))
        {
            Assert.Fail("Input value was not matched by parser.");
        }

        Assert.NotNull(parser.Parse(value));
    }

    public static TheoryData<string, MapLoading> Data = new() {
        { @"Loading map ""de_dust2""", new("de_dust2") },
        { @"Loading map ""workshop\1234""", new(@"workshop\1234") },
    };
}
