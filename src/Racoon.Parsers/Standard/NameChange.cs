using System.Text.RegularExpressions;
using Racoon.Parsers.Abstractions;

namespace Racoon.Parsers.Standard;

public record NameChange( string NewName, Player Player ) : IParsed<NameChange>;

public sealed class NameChangeParser( ParserPool parsers ) : RegexParser<NameChange>( @$"(?<Player>{PlayerParser.Pattern}) changed name to ""(?<Name>.+?)""$" )
{
    private readonly IParser<Player> playerParser = parsers.Get<Player>();

    /// <inheritdoc />
    protected override NameChange Convert( GroupCollection groups )
    {
        ArgumentNullException.ThrowIfNull( groups );

        return new(
            groups[ "Name" ].Value,
            playerParser.Parse( groups[ "Player" ].Value ) );
    }
}
