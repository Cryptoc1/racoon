using System.Text.RegularExpressions;
using Racoon.Parsers.Abstractions;

namespace Racoon.Parsers.Standard;

public sealed record Started : IParsed<Started>;

public sealed class StartedParser( ) : RegexParser<Started>( @"^Started:  """"$" )
{
    /// <inheritdoc />
    protected override Started Convert( GroupCollection groups ) => new();
}
