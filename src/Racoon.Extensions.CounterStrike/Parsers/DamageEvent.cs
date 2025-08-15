using System.Globalization;
using System.Text.RegularExpressions;
using Racoon.Parsers;
using Racoon.Parsers.Abstractions;
using Racoon.Parsers.Standard;

namespace Racoon.Extensions.CounterStrike.Parsers;

public record DamageEvent( int ArmorDamage, Player Attacked, int Damage, string HitLocation, int PostHealth, int PostArmor, Player Target ) : IParsed<DamageEvent>;

// TODO: parse position (square bracket content)
public sealed class DamageEventParser( ParserPool parsers ) : RegexParser<DamageEvent>(
    @$"(?<Attacker>{PlayerParser.Pattern}) \[.*?\] attacked (?<Target>{PlayerParser.Pattern}) \[.*?\] with ""(?<Weapon>.+?)"" " +
    @"\(damage ""(?<Damage>\d+)""\) " +
    @"\(damage_armor ""(?<ArmorDamage>\d+)""\) " +
    @"\(health ""(?<Health>\d+)""\) " +
    @"\(armor ""(?<Armor>\d+)""\) " +
    @"\(hitgroup ""(?<Hitgroup>.*?)""\)" )
{
    private readonly IParser<Player> playerParser = parsers.Get<Player>();

    /// <inheritdoc />
    protected override DamageEvent Convert( GroupCollection groups )
    {
        ArgumentNullException.ThrowIfNull( groups );

        return new(
            int.Parse( groups[ "ArmorDamage" ].Value, CultureInfo.InvariantCulture ),
            playerParser.Parse( groups[ "Attacker" ].Value ),
            int.Parse( groups[ "Damage" ].Value, CultureInfo.InvariantCulture ),
            groups[ "Hitgroup" ].Value,
            int.Parse( groups[ "Health" ].Value, CultureInfo.InvariantCulture ),
            int.Parse( groups[ "Armor" ].Value, CultureInfo.InvariantCulture ),
            playerParser.Parse( groups[ "Target" ].Value ) );
    }
}
