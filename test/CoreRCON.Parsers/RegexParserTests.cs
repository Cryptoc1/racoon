using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.Tests;

public sealed class RegexParserTests
{
    [Fact]
    public void Parser_Sets_Pattern()
    {
        var parser = new Parser();

        Assert.Equal("TEST", parser.Pattern);
        Assert.NotNull(parser.PatternRegex);
    }

    [Fact]
    public void PatternRegex_Is_Compiled()
    {
        var parser = new Parser();
        Assert.True(parser.PatternRegex.Options.HasFlag(RegexOptions.Compiled));
    }

    [Fact]
    public void PatternRegex_Is_CultureInvariant()
    {
        var parser = new Parser();
        Assert.True(parser.PatternRegex.Options.HasFlag(RegexOptions.CultureInvariant));
    }

    [Fact]
    public void PatternRegex_Is_SingleLine()
    {
        var parser = new Parser();
        Assert.True(parser.PatternRegex.Options.HasFlag(RegexOptions.Singleline));
    }

    private sealed class P : IParseable<P>;
    private sealed class Parser : RegexParser<P>
    {
        public Parser() : base("TEST")
        {
        }

        protected override P Convert(GroupCollection groups)
        {
            throw new NotImplementedException();
        }
    }
}
