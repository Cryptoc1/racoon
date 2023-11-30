using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;
using CoreRCON.Parsers.Internal;

namespace CoreRCON.Parsers.Standard;

public record Status(
    string? Hostname,
    HostEndpoints? Endpoints,
    bool IsHibernating,
    string? OperatingSystem,
    string? Map,
    PlayerCounts? Players,
    string? Source,
    string? SteamID,
    string? Type,
    string? Version
) : IParseable<Status>;

public record HostEndpoints(IPEndPoint Local, IPEndPoint Public);
public record PlayerCounts(byte Bots, byte Humans, byte Max)
{
    public static readonly PlayerCounts Empty = new(0, 0, 0);
}

public sealed class StatusParser : IParser<Status>
{
    private readonly Regex DelimeterRegex = new(@"^(?<Key>.+?)\s+:\s+(?<Value>.+)", RegexOptions.Compiled | RegexOptions.Singleline);
    private readonly Regex EndpointRegex = new(@"^(?<Local>.*:\d{0,5})\s+\(public(\s*ip[:]\s*)?(?<Public>.*:\d{0,5})\)", RegexOptions.Compiled | RegexOptions.Singleline);
    private readonly Regex PlayersRegex = new(@"^(?<Humans>\d+) humans, (?<Bots>\d+) bots \((?<Max>\d+) max\).*", RegexOptions.Compiled | RegexOptions.Singleline);
    private readonly Regex PlayersLegacyRegex = new(@"^(?<Humans>\d+) \((?<Max>\d+) max\).*", RegexOptions.Compiled | RegexOptions.Singleline);

    public bool IsMatch(string value) => value.Contains("----- Status -----");

    public Status Parse(string value)
    {
        var groups = value.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => DelimeterRegex.Match(value))
            .Where(match => match.Success)
            .ToDictionary(match => match.Groups["Key"].Value, match => match.Groups["Value"].Value, StringComparer.OrdinalIgnoreCase);

        groups.TryGetValue("source", out var source);
        groups.TryGetValue("map", out var map);

        var (os, type) = OsAndType(groups);
        var (steamId, version) = SteamIdAndVersion(groups);

        return new(
            groups["hostname"],
            Endpoints(groups),
            groups.TryGetValue("players", out var players) && players.Contains("(hibernating)") && !players.Contains("(not hibernating)"),
            os,
            map,
            PlayerCounts(groups),
            source,
            steamId,
            type,
            version);

        HostEndpoints? Endpoints(Dictionary<string, string> groups)
        {
            if (groups.TryGetValue("udp/ip", out var value) || groups.TryGetValue("udp / ip", out value))
            {
                var match = EndpointRegex.Match(value);
                if (match.Success
                    && IPEndPointHelper.TryParse(match.Groups["Local"].Value, out var localhost)
                    && IPEndPointHelper.TryParse(match.Groups["Public"].Value, out var publichost))
                {
                    return new(localhost, publichost);
                }
            }

            return null;
        }

        PlayerCounts? PlayerCounts(Dictionary<string, string> groups)
        {
            if (groups.TryGetValue("players", out var value))
            {
                var match = PlayersRegex.Match(value);
                if (match.Success)
                {
                    return new(byte.Parse(match.Groups["Bots"].Value, CultureInfo.InvariantCulture),
                        byte.Parse(match.Groups["Humans"].Value, CultureInfo.InvariantCulture),
                        byte.Parse(match.Groups["Max"].Value, CultureInfo.InvariantCulture));
                }
                else if ((match = PlayersLegacyRegex.Match(value)).Success)
                {
                    return new(0,
                        byte.Parse(match.Groups["Humans"].Value, CultureInfo.InvariantCulture),
                        byte.Parse(match.Groups["Max"].Value, CultureInfo.InvariantCulture));
                }
            }

            return null;
        }

        static (string? type, string? os) OsAndType(Dictionary<string, string> groups)
        {
            if (groups.TryGetValue("os/type", out var osAndType))
            {
                var segments = osAndType.Split(' ');
                return (segments[0], segments[1]);
            }

            if (groups.TryGetValue("type", out var type))
            {
                return (null, type);
            };

            return (null, null);
        }

        static (string?, string? version) SteamIdAndVersion(Dictionary<string, string> groups)
        {
            if (groups.TryGetValue("version", out var version))
            {
                var match = Regex.Match(version, ".*(\\[.*\\]).*");
                if (match.Success)
                {
                    return (match.Groups[1].Value, version);
                }
            }

            groups.TryGetValue("steamid", out var steamId);
            return (steamId, version);
        }
    }
}
