using CoreRCON.Parsers.Abstractions;
using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Tests;

public sealed class ParserPoolTests
{
    [Fact]
    public void FindImplementations_Finds_ParserInAssembly()
    {
        var testAssembly = typeof(TestParseable).Assembly;

        // find custom parseable parser
        var implementation = ParserPool.FindImplementations<TestParseable>(testAssembly).Single();
        Assert.Equal(typeof(TestParser), implementation);

        // find custom parser for standard parseable
        implementation = ParserPool.FindImplementations<Status>(testAssembly).Single();
        Assert.NotEqual(typeof(StatusParser), implementation);
        Assert.Equal(typeof(TestStatusParser), implementation);
    }

    [Fact]
    public void Get_Gets_FromParseableAssembly()
    {
        var parser = new ParserPool().Get<Status>();

        Assert.IsNotType<TestStatusParser>(parser);
        Assert.IsType<StatusParser>(parser);
    }
}

public sealed class TestParseable : IParseable<TestParseable>;
public sealed class TestParser : IParser<TestParseable>
{
    public bool IsMatch(string value)
    {
        throw new NotImplementedException();
    }

    public TestParseable Parse(string value)
    {
        throw new NotImplementedException();
    }
}

public sealed class TestStatusParser : IParser<Status>
{
    public bool IsMatch(string value)
    {
        throw new NotImplementedException();
    }

    public Status Parse(string value)
    {
        throw new NotImplementedException();
    }
}
