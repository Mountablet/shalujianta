using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Cards.Ironclad;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Monsters;
using Shalujianta.Sim.Relics;

namespace Shalujianta.Sim.Content;

/// <summary>
/// Frozen SliceV0 content (design 4.1.1): Ironclad starter deck (Strike x5,
/// Defend x4, Bash), Burning Blood, Two-Tailed Rat encounters. Register() must be
/// called once per process before any world is built.
/// </summary>
public static class SliceV0
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        Strike.Register();
        Defend.Register();
        Bash.Register();
        RelicRegistry.Register(RelicIds.BurningBlood, new BurningBlood());
    }

    public static List<CardInstance> IroncladStarterDeck() => new DeckBuilder()
        .Add(CardIds.StrikeR, 5)
        .Add(CardIds.DefendR, 4)
        .Add(CardIds.Bash, 1)
        .Build();

    public enum EncounterKind
    {
        SingleRat,       // C0 kernel encounter
        TwoTailedRats,   // Act1_TwoTailedRats evaluation encounter
    }

    /// <summary>Starter combat: full HP, 3 energy, starter deck, Burning Blood, chosen encounter.</summary>
    public static CombatStart StarterCombat(EncounterKind kind, int encounterId = 0)
    {
        Register();
        return new CombatStart
        {
            PlayerMaxHp = 80,
            MaxEnergy = 3,
            EncounterId = kind switch
            {
                EncounterKind.TwoTailedRats => EncounterIds.Act1TwoTailedRats,
                _ => encounterId,
            },
            Deck = IroncladStarterDeck(),
            RelicIds = [RelicIds.BurningBlood],
            Monsters = kind switch
            {
                EncounterKind.TwoTailedRats => Encounters.Act1TwoTailedRats(),
                EncounterKind.SingleRat => Encounters.SingleTwoTailedRat(),
                _ => Encounters.SingleTwoTailedRat(),
            },
        };
    }
}
