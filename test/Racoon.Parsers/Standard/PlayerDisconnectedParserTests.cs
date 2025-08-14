using Racoon.Parsers.Standard;

namespace Racoon.Parsers.Tests.Standard;

public sealed class PlayerDisconnectedParserTests
{
    [Theory( DisplayName = "Parser: matches and parses" )]
    [MemberData( nameof( Data ) )]
    public void Parser_Matches_And_Parses( string value, PlayerDisconnected disconnected )
    {
        var parser = new PlayerDisconnectedParser( ParserPool.Shared );
        if( !parser.IsMatch( value ) )
        {
            Assert.Fail( "Input value was not matched by parser." );
        }

        Assert.Equal( disconnected, parser.Parse( value ) );
    }

    [Fact( DisplayName = "ParserPool: gets parser" )]
    public void ParserPool_Gets_Parser( )
    {
        var parser = new ParserPool().Get<PlayerDisconnected>();

        Assert.NotNull( parser );
        Assert.IsType<PlayerDisconnectedParser>( parser );
    }

    public static readonly TheoryData<string, PlayerDisconnected> Data = new()
    {
        {
            @"""TEST<0><[U:0:123456789]><TERRORIST>"" disconnected (reason ""Unit Test"")",
            new(new(0, "TEST", "[U:0:123456789]", "TERRORIST"), "Unit Test")
        },
    };
}
