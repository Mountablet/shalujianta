using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Core;
using Shalujianta.Sim.Combat;

namespace Shalujianta.Sim.Cards.Ironclad;

/// <summary>
/// Bash. Wiki values (STS2:Bash, 2026-10-31): 2 cost, deal 8 damage and apply
/// 2 Vulnerable / Bash+ 10 damage and 3 Vulnerable. Pending dll verification in PR-014.
/// </summary>
public static class Bash
{
    public const int Damage = 8;
    public const int Vulnerable = 2;
    public const int UpgradedDamage = 10;
    public const int UpgradedVulnerable = 3;

    public static void Apply(CombatWorld world, CardInstance card, TargetRef target)
    {
        world.DamageMonster(target.Index, card.Upgrade > 0 ? UpgradedDamage : Damage, world.PlayerPowers);
        if (world.Monsters[target.Index].Alive)
        {
            world.ApplyMonsterPower(target.Index, PowerIds.Vulnerable, card.Upgrade > 0 ? UpgradedVulnerable : Vulnerable);
        }
    }

    public static void Register() =>
        CardRegistry.Register(new CardDef(
            CardIds.Bash, CardType.Attack, 2, CardTarget.Enemy, CardFlags.None, Apply));
}
