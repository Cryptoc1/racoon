using Racoon.Parsers.Abstractions;

namespace Racoon.Extensions.CounterStrike.Parsers;

/// <summary> Extensions for adding Counter-Strike parsers. </summary>
public static class ParserPoolExtensions
{
    /// <summary> Add the Counter-Strike parsers. </summary>
    /// <param name="builder"> The parser pool builder. </param>
    public static IParserPoolBuilder UseCounterStrike( this IParserPoolBuilder builder )
    {
        ArgumentNullException.ThrowIfNull( builder );

        return builder.AddParser<DamageEvent, DamageEventParser>()
            .AddParser<Frag, FragParser>()
            .AddParser<FragAssist, FragAssistParser>()
            .AddParser<GameOverScore, GameOverScoreParser>()
            .AddParser<RoundEndScore, RoundEndScoreParser>()
            .AddParser<TeamSide, TeamSideParser>();
    }
}