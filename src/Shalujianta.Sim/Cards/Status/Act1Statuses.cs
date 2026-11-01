using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;

namespace Shalujianta.Sim.Cards.Status;

/// <summary>
/// Wound. STS2 status; v0 treats it as an unplayable dead card (wiki rule page not
/// verified — PR-014 parity re-checks).
/// </summary>
public static class Wound
{
    public static void Register() =>
        CardRegistry.Register(new CardDef(
            CardIds.Wound, CardType.Status, -1, CardTarget.None, CardFlags.Unplayable, null));
}

/// <summary>
/// Dazed. Wiki (Slay_the_Spire_2:Dazed, 2026-11-01): unplayable colorless status with
/// Ethereal — exhausted from the hand at the end of the turn. Shuffled into the
/// discard pile by Haunted Ship / Eye With Teeth / Chomper.
/// </summary>
public static class Dazed
{
    public static void Register() =>
        CardRegistry.Register(new CardDef(
            CardIds.Dazed, CardType.Status, -1, CardTarget.None,
            CardFlags.Unplayable | CardFlags.Ethereal, null));
}

/// <summary>
/// Infection. Wiki: unplayable status — while in hand at the end of your turn, take
/// 3 damage (handled by CombatWorld's StatusEndOfTurnDamage table). Phrog Parasite
/// shuffles it into the discard pile.
/// </summary>
public static class Infection
{
    public static void Register() =>
        CardRegistry.Register(new CardDef(
            CardIds.Infection, CardType.Status, -1, CardTarget.None, CardFlags.Unplayable, null));
}

/// <summary>
/// Beckon. Wiki: unplayable status — while in hand at the end of your turn, lose
/// 6 HP (StatusEndOfTurnDamage table). Soul Fysh adds it to the draw and discard piles.
/// </summary>
public static class Beckon
{
    public static void Register() =>
        CardRegistry.Register(new CardDef(
            CardIds.Beckon, CardType.Status, -1, CardTarget.None, CardFlags.Unplayable, null));
}
