namespace Racoon.Extensions.CounterStrike;

/// <summary> Extensions to <see cref="RCONClient"/> for executing common Counter-Strike commands. </summary>
public static class RCONClientExtensions
{
    /// <summary> Change to server to a known workshop map. </summary>
    /// <seealso cref="DSWorkshopListMaps" />
    public static Task<string> DSWorkshopChangeLevel( this RCONClient client, string map, CancellationToken cancellation = default )
    {
        ArgumentNullException.ThrowIfNull( client );
        ArgumentException.ThrowIfNullOrWhiteSpace( map );
        return client.SendCommandAsync( $"ds_workshop_changelevel {map}", cancellation );
    }

    /// <summary> Get a list of workshop maps known by the server. </summary>
    /// <seealso cref="DSWorkshopChangeLevel" />
    public static async Task<string[]> DSWorkshopListMaps( this RCONClient client, CancellationToken cancellation = default )
    {
        ArgumentNullException.ThrowIfNull( client );

        var value = await client.SendCommandAsync( "ds_workshop_listmaps", cancellation );
        return value.Split( [ Environment.NewLine ], StringSplitOptions.RemoveEmptyEntries );
    }

    /// <summary> Add a bot to the 'Counter Terrorists' team. </summary>
    public static Task<string> BotAddCT( this RCONClient client, CancellationToken cancellation = default )
    {
        ArgumentNullException.ThrowIfNull( client );
        return client.SendCommandAsync( "bot_add_ct", cancellation );
    }

    /// <summary> Add a bot to the 'Terrorists' team. </summary>
    public static Task<string> BotAddT( this RCONClient client, CancellationToken cancellation = default )
    {
        ArgumentNullException.ThrowIfNull( client );
        return client.SendCommandAsync( "bot_add_t", cancellation );
    }

    /// <summary> Execute a named config. </summary>
    public static Task<string> Exec( this RCONClient client, string config, CancellationToken cancellation = default )
    {
        ArgumentNullException.ThrowIfNull( client );
        ArgumentException.ThrowIfNullOrWhiteSpace( config );
        return client.SendCommandAsync( $"exec {config}", cancellation );
    }

    /// <summary> Direct the server to host a workshop map by it's fileid. </summary>
    public static Task<string> HostWorkshopMap( this RCONClient client, ulong workshopId, CancellationToken cancellation = default )
    {
        ArgumentNullException.ThrowIfNull( client );
        ArgumentOutOfRangeException.ThrowIfNegative( workshopId );
        return client.SendCommandAsync( $"host_workshop_map {workshopId}", cancellation );
    }

    /// <summary> Announce a message to all clients connected to the server. </summary>
    public static Task<string> Say( this RCONClient client, string text, CancellationToken cancellation = default )
    {
        ArgumentNullException.ThrowIfNull( client );
        ArgumentException.ThrowIfNullOrEmpty( text );
        return client.SendCommandAsync( $"say {text}", cancellation );
    }
}
