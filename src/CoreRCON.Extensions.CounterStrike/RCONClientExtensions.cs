using CoreRCON.Parsers.Standard;

namespace CoreRCON.Extensions.CounterStrike;

public static class RCONClientExtensions
{
    public static Task<string> DSWorkshopChangeLevel(this RCONClient client, string map, CancellationToken cancellation = default) => client.SendCommandAsync($"ds_workshop_changelevel {map}", cancellation);

    public static async Task<string[]> DSWorkshopListMaps(this RCONClient client, CancellationToken cancellation = default)
    {
        var value = await client.SendCommandAsync("ds_workshop_listmaps", cancellation);
        return value.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
    }

    public static Task<string> BotAddCT(this RCONClient client, CancellationToken cancellation = default) => client.SendCommandAsync("bot_add_ct", cancellation);

    public static Task<string> BotAddT(this RCONClient client, CancellationToken cancellation = default) => client.SendCommandAsync("bot_add_t", cancellation);

    public static Task<string> Exec(this RCONClient client, string config, CancellationToken cancellation = default) => client.SendCommandAsync($"exec {config}", cancellation);

    public static Task HostWorkshopMap(this RCONClient client, ulong workshopId, CancellationToken cancellation = default) => client.SendCommandAsync($"host_workshop_map {workshopId}", cancellation);

    public static Task<string> Say(this RCONClient client, string text, CancellationToken cancellation = default) => client.SendCommandAsync($"say {text}", cancellation);

    public static Task<Status> Status(this RCONClient client, CancellationToken cancellation = default) => client.SendCommandAsync<Status>("status", cancellation);
}
