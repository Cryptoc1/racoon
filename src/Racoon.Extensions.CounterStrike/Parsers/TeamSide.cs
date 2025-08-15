using System.Text.RegularExpressions;
using Racoon.Parsers.Abstractions;

namespace Racoon.Extensions.CounterStrike.Parsers;

public record TeamSide( string CurrentSide, string Team ) : IParsed<TeamSide>;

public sealed class TeamSideParser( ) : RegexParser<TeamSide>( @"Team playing ""(?<side>.+?)"": (?<team>.*)" )
{
    /// <inheritdoc />
    protected override TeamSide Convert( GroupCollection groups )
    {
        ArgumentNullException.ThrowIfNull( groups );

        return new( groups[ "side" ].Value, groups[ "team" ].Value );
    }
}
