using CoreRCON.Parsers.Standard;

namespace CoreRCON.Extensions.CounterStrike;

public static class RCONClientExtensions
{
    public static Task<string> DSWorkshopChangeLevel(this RCONClient client, string map, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(map);
        return client.SendCommandAsync($"ds_workshop_changelevel {map}", cancellation);
    }

    public static async Task<string[]> DSWorkshopListMaps(this RCONClient client, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        var value = await client.SendCommandAsync("ds_workshop_listmaps", cancellation);
        return value.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
    }

    public static Task<string> BotAddCT(this RCONClient client, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.SendCommandAsync("bot_add_ct", cancellation);
    }

    public static Task<string> BotAddT(this RCONClient client, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.SendCommandAsync("bot_add_t", cancellation);
    }

    public static Task<string> Exec(this RCONClient client, string config, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(config);
        return client.SendCommandAsync($"exec {config}", cancellation);
    }

    public static Task<string> HostWorkshopMap(this RCONClient client, ulong workshopId, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentOutOfRangeException.ThrowIfNegative(workshopId);
        return client.SendCommandAsync($"host_workshop_map {workshopId}", cancellation);
    }

    public static Task<string> Say(this RCONClient client, string text, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrEmpty(text);
        return client.SendCommandAsync($"say {text}", cancellation);
    }

    public static Task<Status> Status(this RCONClient client, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.SendCommandAsync<Status>("status", cancellation);
    }
}
