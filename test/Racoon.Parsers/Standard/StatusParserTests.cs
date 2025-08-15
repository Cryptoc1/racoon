using System.Net;
using Racoon.Parsers.Standard;

namespace Racoon.Parsers.Tests.Standard;

public sealed class StatusParserTests
{
    [Theory( DisplayName = "Parser: matches and parses" )]
    [MemberData( nameof( Data ) )]
    public void Parser_Matches_And_Parses( string value, Status status )
    {
        var parser = new StatusParser();
        if( !parser.IsMatch( value ) )
        {
            Assert.Fail( "Input value was not matched by parser." );
        }

        Assert.Equal( status, parser.Parse( value ) );
    }

    [Fact( DisplayName = "ParserPool: gets parser" )]
    public void ParserPool_Gets_Parser( )
    {
        var parser = ParserPool.CreateDefault().Get<Status>();

        Assert.NotNull( parser );
        Assert.IsType<StatusParser>( parser );
    }

    public static readonly TheoryData<string, Status> Data = new()
    {
        {
@"----- Status -----
@ Current  :  game
source   : console
hostname : TEST
spawn    : 0
version  : 1.39.6.9/13969 9842 secure  public
steamid  : [A:0:0123456789:12345] (12345678909876)
udp/ip   : 10.0.0.4:27015 (public 123.45.67.89:27015)
os/type  : Linux dedicated
sourcetv[0] : 10.0.0.4:27020 (public 123.45.67.89:27020) delay 105.0s
players  : 1 humans, 2 bots (0 max) (not hibernating) (unreserved)",
        new(
            "TEST",
            new(new(IPAddress.Parse("10.0.0.4"), 27015), new(IPAddress.Parse("123.45.67.89"), 27015)),
            false,
            "Linux",
            default,
            new(2, 1, 0),
            "console",
            "[A:0:0123456789:12345] (12345678909876)",
            "dedicated",
            "1.39.6.9/13969 9842 secure  public")
        },
        {
@"----- Status -----
@ Current  :  game
source   : console
hostname : TEST
spawn    : 0
version  : 1.39.6.9/13969 9842 secure  public
steamid  : [A:0:0123456789:12345] (12345678909876)
udp/ip   : 10.0.0.4:27015 (public 123.45.67.89:27015)
os/type  : Linux dedicated
sourcetv[0] : 10.0.0.4:27020 (public 123.45.67.89:27020) delay 105.0s
players  : 0 humans, 0 bots (0 max) (hibernating) (unreserved)",
        new(
            "TEST",
            new(new(IPAddress.Parse("10.0.0.4"), 27015), new(IPAddress.Parse("123.45.67.89"), 27015)),
            true,
            "Linux",
            default,
            new(0, 0, 0),
            "console",
            "[A:0:0123456789:12345] (12345678909876)",
            "dedicated",
            "1.39.6.9/13969 9842 secure  public")
        },
    };
}
