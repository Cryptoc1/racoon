using System.Globalization;
using System.Text.RegularExpressions;
using CoreRCON.Parsers.Standard;

namespace CoreRCON.Parsers.Csgo;

public record DamageEvent(int ArmorDamage, Player Attacked, int Damage, string HitLocation, int PostHealth, int PostArmor, Player Target) : IParseable<DamageEvent>;

public sealed class DamageEventParser : RegexParser<DamageEvent>
{
    //Todo parse position (square bracket content)
    public DamageEventParser() : base(
        @$"(?<Attacker>{PlayerParser.Shared.Pattern}) \[.*?\] attacked (?<Target>{PlayerParser.Shared.Pattern}) \[.*?\] with ""(?<Weapon>.+?)"" " +
        @"\(damage ""(?<Damage>\d+)""\) " +
        @"\(damage_armor ""(?<ArmorDamage>\d+)""\) " +
        @"\(health ""(?<Health>\d+)""\) " +
        @"\(armor ""(?<Armor>\d+)""\) " +
        @"\(hitgroup ""(?<Hitgroup>.*?)""\)"
    )
    {
    }

    protected override DamageEvent Load(GroupCollection groups) => new(
        int.Parse(groups["ArmorDamage"].Value, CultureInfo.InvariantCulture),
        PlayerParser.Shared.Parse(groups["Attacker"]),
        int.Parse(groups["Damage"].Value, CultureInfo.InvariantCulture),
        groups["Hitgroup"].Value,
        int.Parse(groups["Health"].Value, CultureInfo.InvariantCulture),
        int.Parse(groups["Armor"].Value, CultureInfo.InvariantCulture),
        PlayerParser.Shared.Parse(groups["Target"])
    );
}
