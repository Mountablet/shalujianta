namespace Shalujianta.Sim.Core;

public enum RngBackendKind : byte
{
    SystemRandomLegacy = 0,
    Pcg32 = 1,
}

public enum RngStream : byte
{
    UpFront = 0,
    Shuffle = 1,
    UnknownMapPoint = 2,
    CombatCardGeneration = 3,
    CombatPotionGeneration = 4,
    CombatCardSelection = 5,
    CombatEnergyCosts = 6,
    CombatTargets = 7,
    MonsterAi = 8,
    Niche = 9,
    CombatOrbGeneration = 10,
    TreasureRoomRelics = 11,
    Neow = 12,
    EventBase = 13,
    Custom0 = 14,
    Custom1 = 15,
}

public sealed class RngStreamSpec
{
    public required RngStream Stream { get; init; }
    public required string HashString { get; init; }
    public int ExtraOffset { get; init; }
}

public static class RngStreamTable
{
    public static IReadOnlyList<RngStreamSpec> All { get; } =
    [
        new() { Stream = RngStream.UpFront, HashString = "up_front" },
        new() { Stream = RngStream.Shuffle, HashString = "shuffle" },
        new() { Stream = RngStream.UnknownMapPoint, HashString = "unknown_map_point" },
        new() { Stream = RngStream.CombatCardGeneration, HashString = "combat_card_generation" },
        new() { Stream = RngStream.CombatPotionGeneration, HashString = "combat_potion_generation" },
        new() { Stream = RngStream.CombatCardSelection, HashString = "combat_card_selection" },
        new() { Stream = RngStream.CombatEnergyCosts, HashString = "combat_energy_costs" },
        new() { Stream = RngStream.CombatTargets, HashString = "combat_targets" },
        new() { Stream = RngStream.MonsterAi, HashString = "monster_ai" },
        new() { Stream = RngStream.Niche, HashString = "niche" },
        new() { Stream = RngStream.CombatOrbGeneration, HashString = "combat_orbs" },
        new() { Stream = RngStream.TreasureRoomRelics, HashString = "treasure_room_relics" },
        new() { Stream = RngStream.Neow, HashString = "NEOW", ExtraOffset = 1 },
        new() { Stream = RngStream.EventBase, HashString = "event_base" },
        new() { Stream = RngStream.Custom0, HashString = "custom_0" },
        new() { Stream = RngStream.Custom1, HashString = "custom_1" },
    ];

    public static int DerivedSeed(int runSeedI32, RngStreamSpec spec)
    {
        uint hash = unchecked((uint)DeterministicHash.GetDeterministicHashCode(spec.HashString));
        return unchecked((int)((uint)runSeedI32 + hash + (uint)spec.ExtraOffset));
    }
}
