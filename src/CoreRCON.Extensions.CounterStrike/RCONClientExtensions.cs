using CoreRCON.Parsers.Standard;

namespace CoreRCON.Extensions.CounterStrike;

public static class RCONClientExtensions
{
    public static Task<string> DSWorkshopChangeLevel(this RCONClient console, string map) => console.SendCommandAsync($"ds_workshop_changelevel {map}");

    public static async Task<string[]> DSWorkshopListMaps(this RCONClient console)
    {
        var value = await console.SendCommandAsync("ds_workshop_listmaps");
        return value.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
    }

    public static Task<string> BotAddCT(this RCONClient console) => console.SendCommandAsync("bot_add_ct");

    public static Task<string> BotAddT(this RCONClient console) => console.SendCommandAsync("bot_add_t");

    public static Task<string> Exec(this RCONClient console, string config) => console.SendCommandAsync($"exec {config}");

    public static Task HostWorkshopMap(this RCONClient console, ulong workshopId) => console.SendCommandAsync($"host_workshop_map {workshopId}");

    public static Task<string> Say(this RCONClient console, string text) => console.SendCommandAsync($"say {text}");

    public static Task<Status> Status(this RCONClient console) => console.SendCommandAsync<Status>("status");
}
