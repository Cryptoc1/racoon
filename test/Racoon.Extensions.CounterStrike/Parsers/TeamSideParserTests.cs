using Racoon.Extensions.CounterStrike.Parsers;
using Racoon.Parsers;

namespace Racoon.Extensions.CounterStrike.Tests.Parsers;

public sealed class TeamSideParserTests
{
    [Theory( DisplayName = "Parser: matches and parses" )]
    [MemberData( nameof( Data ) )]
    public void Parser_Matches_And_Parses( string value, TeamSide side )
    {
        var parser = new TeamSideParser();
        if( !parser.IsMatch( value ) )
        {
            Assert.Fail( "Input value was not matched by parser." );
        }

        Assert.Equal( side, parser.Parse( value ) );
    }

    [Fact( DisplayName = "ParserPool: gets parser" )]
    public void ParserPool_Gets_Parser( )
    {
        var parser = new ParserPool().Get<TeamSide>();

        Assert.NotNull( parser );
        Assert.IsType<TeamSideParser>( parser );
    }

    public static readonly TheoryData<string, TeamSide> Data = new()
    {
        { @"Team playing ""TEST"": TESTING", new("TEST", "TESTING")}
    };
}
