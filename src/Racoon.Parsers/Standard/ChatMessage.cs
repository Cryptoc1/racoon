using System.Text.RegularExpressions;
using Racoon.Parsers.Abstractions;

namespace Racoon.Parsers.Standard;

public record ChatMessage( MessageChannel Channel, string Message, Player Player ) : IParseable<ChatMessage>;

public enum MessageChannel
{
    Team,
    All
}

public sealed class ChatMessageParser( ParserPool parsers ) : RegexParser<ChatMessage>( @$"(?<Sender>{PlayerParser.Pattern}) (?<Channel>say_team|say) ""(?<Message>.+?)""" )
{
    private readonly IParser<Player> playerParser = parsers.Get<Player>();

    /// <inheritdoc />
    protected override ChatMessage Convert( GroupCollection groups )
    {
        ArgumentNullException.ThrowIfNull( groups );

        return new(
            groups[ "Channel" ].Value == "say" ? MessageChannel.All : MessageChannel.Team,
            groups[ "Message" ].Value,
            playerParser.Parse( groups[ "Sender" ].Value ) );
    }
}
