using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Core;
using Shalujianta.Sim.Combat;

namespace Shalujianta.Sim.Cards.Ironclad;

/// <summary>
/// Defend (Ironclad). Wiki values (STS2:Defend_(Ironclad), 2026-10-31): 1 cost,
/// gain 5 block / Defend+ 8. Pending dll verification in PR-014.
/// </summary>
public static class Defend
{
    public const int Block = 5;
    public const int UpgradedBlock = 8;

    public static void Apply(CombatWorld world, CardInstance card, TargetRef target) =>
        world.GainPlayerBlock(card.Upgrade > 0 ? UpgradedBlock : Block);

    public static void Register() =>
        CardRegistry.Register(new CardDef(
            CardIds.DefendR, CardType.Skill, 1, CardTarget.None, CardFlags.None, Apply));
}
