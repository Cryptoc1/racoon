using Racoon.Parsers.Standard;

namespace Racoon.Extensions;

/// <summary> Extensions to <see cref="RCONClient"/> for common RCON commands. </summary>
public static class RCONClientExtensions
{
    /// <summary> Get the server's current status. </summary>
    public static Task<Status> Status( this RCONClient client, CancellationToken cancellation = default )
    {
        ArgumentNullException.ThrowIfNull( client );
        return client.SendCommandAsync<Status>( "status", cancellation );
    }
}