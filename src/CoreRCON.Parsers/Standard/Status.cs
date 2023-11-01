using System.Globalization;
using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.Standard;

public record Status(
    byte Bots,
    ulong CommunityID,
    string Hostname,
    byte Humans,
    string? LocalHost,
    string Map,
    byte MaxPlayers,
    string? PublicHost,
    string? SteamID,
    string Version,
    bool Hibernating,
    string? Type
) : IParseable<Status>
{
    [Obsolete("No longer part of status message")]
    public string? Account { get; }

    [Obsolete("No longer part of status message")]
    public string[]? Tags { get; }
}

public sealed class StatusParser : IParser<Status>
{
    public bool IsMatch(string value) => value.Contains("hostname: ") || value.Contains("hibernating");

    public Status Parse(string value)
    {
        var groups = value.Split('\n')
            .Select(value => value.Split(':'))
            .Where(value => value.Length > 1 && !string.IsNullOrEmpty(value[0].Trim()) && !string.IsNullOrEmpty(value[1].Trim()))
            .ToDictionary(
                value => value[0].Trim(),
                value => string.Join(":", value.ToList().Skip(1)).Trim()
            );

        groups.TryGetValue("hostname", out var hostname);

        string? steamId = null;
        if (groups.TryGetValue("version", out var version))
        {
            var match = Regex.Match(version, ".*(\\[.*\\]).*");
            if (match.Success)
            {
                steamId = match.Groups[1].Value;
            }
        }

        groups.TryGetValue("map", out var map);
        groups.TryGetValue("type", out var type);

        byte humans = 0, bots = 0, maxPlayers = 0;
        bool hibernating = false;

        if (groups.TryGetValue("players", out var players))
        {
            var match = Regex.Match(players, "(\\d+) humans, (\\d+) bots\\((\\d+)/\\d+ max\\) (\\(not hibernating\\))?.*");
            if (match.Success)
            {
                bots = byte.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                humans = byte.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                maxPlayers = byte.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                if (match.Groups[4].Success)
                {
                    hibernating = !match.Groups[4].Value.Contains("not hibernating");
                }
            }

            // NOTE: legacy formatting
            else if ((match = Regex.Match(players, "(\\d+) \\((\\d+) max\\).*")).Success)
            {
                bots = 0;
                humans = byte.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                maxPlayers = byte.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            }
        }
        else
        {
            hibernating = value.Contains("hibernating") && !value.Contains("not hibernating");
        }

        string? localIp = null, publicIp = null;
        if (groups.TryGetValue("udp / ip", out var address))
        {
            var match = Regex.Match(address, "\\((.*:.*)\\)\\s+\\(public ip: (.*)\\).*");
            if (match.Success)
            {
                localIp = match.Groups[1].Value;
                publicIp = match.Groups[2].Value;
            }

            // NOTE: legacy format
            else if ((match = Regex.Match(address, "((\\d|\\.)+:(\\d|\\.)+)\\(public ip: (.*)\\).*")).Success)
            {
                localIp = match.Groups[1].Value;
                publicIp = match.Groups[4].Value;
            }
        }

        return new(bots, default, hostname, humans, localIp, map, maxPlayers, publicIp, steamId, version, hibernating, default);
    }
}
