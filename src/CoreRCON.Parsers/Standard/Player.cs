using System.Globalization;
using System.Text.RegularExpressions;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers.Standard;

public record Player(int ClientId, string Name, string SteamId, string Team) : IParseable<Player>;

public sealed class PlayerParser : RegexParser<Player>
{
    public static readonly PlayerParser Shared = new();

    public PlayerParser() : base(@"""(?<Name>.+?(?:<.*>)*)<(?<ClientID>\d+?)><(?<SteamID>.+?)><(?<Team>.+?)?>""")
    {
    }

    protected override Player Convert(GroupCollection groups) => new(
        int.Parse(groups["ClientID"].Value, CultureInfo.InvariantCulture),
        groups["Name"].Value,
        groups["SteamID"].Value,
        groups["Team"].Value
    );
}
