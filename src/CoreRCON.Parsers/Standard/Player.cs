using System.Globalization;
using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.Standard;

public record Player(int ClientId, string Name, string SteamId, string Team) : IParseable<Player>;

public sealed class PlayerParser() : RegexParser<Player>(@"""(?<Name>.+?(?:<.*>)*)<(?<ClientID>\d+?)><(?<SteamID>.+?)><(?<Team>.+?)?>""")
{
    public static readonly PlayerParser Shared = new();

    protected override Player Convert(GroupCollection groups)
    {
        return new(
            int.Parse(groups["ClientID"].Value, CultureInfo.InvariantCulture),
            groups["Name"].Value,
            groups["SteamID"].Value,
            groups["Team"].Value);
    }
}
