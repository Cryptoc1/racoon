using System.Text.RegularExpressions;
using Racoon.Parsers.Abstractions;

namespace Racoon.Parsers.Standard;

public record MapLoading( string Map ) : IParsed<MapLoading>;

public sealed class MapLoadingParser( ) : RegexParser<MapLoading>( @"^Loading map ""(?<Map>.+?)""" )
{
    /// <inheritdoc />
    protected override MapLoading Convert( GroupCollection groups )
    {
        ArgumentNullException.ThrowIfNull( groups );
        return new( groups[ "Map" ].Value );
    }
}
