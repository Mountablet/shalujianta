using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Core;
using Shalujianta.Sim.Combat;

namespace Shalujianta.Sim.Cards.Ironclad;

/// <summary>
/// Strike (Ironclad). Wiki values (STS2:Strike_(Ironclad), 2026-10-31): 1 cost,
/// deal 6 damage / Strike+ 9. Pending dll verification in PR-014.
/// </summary>
public static class Strike
{
    public const int Damage = 6;
    public const int UpgradedDamage = 9;

    public static void Apply(CombatWorld world, CardInstance card, TargetRef target) =>
        world.DamageMonster(target.Index, card.Upgrade > 0 ? UpgradedDamage : Damage, world.PlayerPowers);

    public static void Register() =>
        CardRegistry.Register(new CardDef(
            CardIds.StrikeR, CardType.Attack, 1, CardTarget.Enemy, CardFlags.None, Apply));
}
