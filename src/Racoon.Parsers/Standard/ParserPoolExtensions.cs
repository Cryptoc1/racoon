using Racoon.Parsers.Abstractions;

namespace Racoon.Parsers.Standard;

/// <summary> Extensions for adding standard parsers. </summary>
public static class ParserPoolExtensions
{
    /// <summary> Add the standard parsers. </summary>
    /// <param name="builder"> The parser pool builder. </param>
    public static IParserPoolBuilder UseStandard( this IParserPoolBuilder builder )
    {
        ArgumentNullException.ThrowIfNull( builder );

        return builder.AddParser<ChatMessage, ChatMessageParser>()
            .AddParser<KillFeed, KillFeedParser>()
            .AddParser<MapLoading, MapLoadingParser>()
            .AddParser<NameChange, NameChangeParser>()
            .AddParser<Player, PlayerParser>()
            .AddParser<PlayerConnected, PlayerConnectedParser>()
            .AddParser<PlayerDisconnected, PlayerDisconnectedParser>()
            .AddParser<Started, StartedParser>()
            .AddParser<Status, StatusParser>()
            .AddParser<TeamChange, TeamChangeParser>();
    }
}