using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard;

public record ChatMessage(MessageChannel Channel, string Message, Player Player) : IParseable<ChatMessage>;

public enum MessageChannel
{
    Team,
    All
}

public sealed class ChatMessageParser : RegexParser<ChatMessage>
{
    public ChatMessageParser() : base(@$"(?<Sender>{PlayerParser.Shared.Pattern}) (?<Channel>say_team|say) ""(?<Message>.+?)""")
    {
    }

    protected override ChatMessage Load(GroupCollection groups) => new(
        groups["Channel"].Value == "say" ? MessageChannel.All : MessageChannel.Team,
        groups["Message"].Value,
        PlayerParser.Shared.Parse(groups["Sender"])
    );
}
