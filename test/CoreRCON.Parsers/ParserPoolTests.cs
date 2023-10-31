using System.Data;
using System.Reflection;
using CoreRCON.Parsers.Abstractions;
using CoreRCON.Parsers.CounterStrike;
using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Tests;

public sealed class ParserPoolTests
{
    [Fact]
    public void FindImplementations_Finds_ParserInAssembly()
    {
        var testAssembly = typeof(TestParseable).Assembly;

        // find custom parseable parser
        var implementation = ParserPool.FindImplementations<TestParseable>(testAssembly).First();
        Assert.Equal(typeof(TestParser), implementation);

        // find custom parser for built-in parseable
        implementation = ParserPool.FindImplementations<Status>(testAssembly).First();
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

    [Fact]
    public void Shared_Gets_BuiltInParsers()
    {
        var pool = ParserPool.Shared;

        // standard
        Pool_Gets_Parser<ChatMessage>(pool);
        Pool_Gets_Parser<KillFeed>(pool);
        Pool_Gets_Parser<NameChange>(pool);
        Pool_Gets_Parser<Player>(pool);
        Pool_Gets_Parser<PlayerConnected>(pool);
        Pool_Gets_Parser<PlayerDisconnected>(pool);
        Pool_Gets_Parser<Status>(pool);
        Pool_Gets_Parser<TeamChange>(pool);

        // counter-strike
        Pool_Gets_Parser<DamageEvent>(pool);
        Pool_Gets_Parser<Frag>(pool);
        Pool_Gets_Parser<FragAssist>(pool);
        Pool_Gets_Parser<GameOverScore>(pool);
        Pool_Gets_Parser<MapLoading>(pool);
        Pool_Gets_Parser<RoundEndScore>(pool);
        Pool_Gets_Parser<Started>(pool);
        Pool_Gets_Parser<TeamSide>(pool);

        void Pool_Gets_Parser<T>(ParserPool pool) where T : class, IParseable<T>
        {
            var parser = pool.Get<T>();
            Assert.IsAssignableFrom<IParser<T>>(parser);
        }
    }
}

public sealed class TestParseable : IParseable<TestParseable>;
public sealed class TestParser : IParser<TestParseable>
{
    public bool IsMatch(string input) => throw new NotImplementedException();
    public TestParseable Parse(string input) => throw new NotImplementedException();
}

public sealed class TestStatusParser : IParser<Status>
{
    public bool IsMatch(string input) => throw new NotImplementedException();
    public Status Parse(string input) => throw new NotImplementedException();
}
