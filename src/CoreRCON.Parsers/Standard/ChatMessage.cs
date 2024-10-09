﻿using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.Standard;

public record ChatMessage(MessageChannel Channel, string Message, Player Player) : IParseable<ChatMessage>;

public enum MessageChannel
{
    Team,
    All
}

public sealed class ChatMessageParser() : RegexParser<ChatMessage>(@$"(?<Sender>{PlayerParser.Shared.Pattern}) (?<Channel>say_team|say) ""(?<Message>.+?)""")
{
    protected override ChatMessage Convert(GroupCollection groups)
    {
        return new(
            groups["Channel"].Value == "say" ? MessageChannel.All : MessageChannel.Team,
            groups["Message"].Value,
            PlayerParser.Shared.Parse(groups["Sender"].Value));
    }
}
