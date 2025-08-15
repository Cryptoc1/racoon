using Racoon.Extensions.CounterStrike.Parsers;
using Racoon.Parsers;

namespace Racoon.Extensions.CounterStrike.Tests.Parsers;

public sealed class FragAssistParserTests
{
    [Theory( DisplayName = "Parser: matches and parses" )]
    [MemberData( nameof( Data ) )]
    public void Parser_Matches_And_Parses( string value, FragAssist assist )
    {
        var parser = new FragAssistParser( ParserPool.CreateDefault() );
        if( !parser.IsMatch( value ) )
        {
            Assert.Fail( "Input value was not matched by parser." );
        }

        Assert.Equal( assist, parser.Parse( value ) );
    }

    [Fact( DisplayName = "ParserPool: gets parser" )]
    public void ParserPool_Gets_Parser( )
    {
        var parser = ParserPool.CreateDefault( builder => builder.UseCounterStrike() ).Get<FragAssist>();

        Assert.NotNull( parser );
        Assert.IsType<FragAssistParser>( parser );
    }

    public static readonly TheoryData<string, FragAssist> Data = new()
    {
        {
            @"""TEST<0><[U:0:123456789]><TERRORIST>"" assisted killing ""TEST1<1><[U:0:123456789]><TERRORIST>""",
            new(new(0, "TEST", "[U:0:123456789]", "TERRORIST"), new(1, "TEST1", "[U:0:123456789]", "TERRORIST"))
        }
    };
}
