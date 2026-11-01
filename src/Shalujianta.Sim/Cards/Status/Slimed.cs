using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Core;
using Shalujianta.Sim.Combat;

namespace Shalujianta.Sim.Cards.Status;

/// <summary>
/// Slimed (STS2 status, wiki 2026-11-01): 1-cost Colorless Status, "Draw 1 card.
/// Exhaust." — playable, unlike STS1. Enemies shuffle it into the discard pile.
/// </summary>
public static class Slimed
{
    public static void Apply(CombatWorld world, CardInstance card, TargetRef target) =>
        world.DrawPlayerCards(1);

    public static void Register() =>
        CardRegistry.Register(new CardDef(
            CardIds.Slimed, CardType.Status, 1, CardTarget.None, CardFlags.Exhausts, Apply));
}
