namespace Shalujianta.Sim.Codec;

/// <summary>Mirrors python CombatPhase (EXEC is reserved for in-Step resolution).</summary>
public enum CombatPhase : byte
{
    Player = 0,
    Enemy = 1,
    Exec = 2,
}

/// <summary>Minimal combat-view DTO (PR-008 replaces it with the full Observation codec).</summary>
public sealed class CombatObservation
{
    public ushort SchemaVersion { get; init; }
    public string CatalogVersion { get; init; } = "";
    public InfoMode Mode { get; init; }
    public ScreenType Screen { get; init; }
    public int Turn { get; init; }
    public CombatPhase Phase { get; init; }
    public PlayerObservation Player { get; init; } = new();
    public List<MonsterObservation> Monsters { get; init; } = [];
    public List<CardObservation> Hand { get; init; } = [];
    public int DrawCount { get; init; }
    public int DiscardCount { get; init; }
    public int ExhaustCount { get; init; }
    public int LimboCount { get; init; }
    public List<BagCount> DrawBagCounts { get; init; } = [];
    public List<int> Potions { get; init; } = [];
    public bool ShufflePending { get; init; }
}

public sealed class PlayerObservation
{
    public int Hp { get; init; }
    public int MaxHp { get; init; }
    public int Block { get; init; }
    public int Energy { get; init; }
    public int MaxEnergy { get; init; }
    public List<Shalujianta.Sim.Combat.PowerStack> Powers { get; init; } = [];
    public int CardsPlayedThisTurn { get; init; }
    public int ZeroCostPlaysThisTurn { get; init; }
    public int TimesDamagedThisCombat { get; init; }
}

public sealed class MonsterObservation
{
    public int Slot { get; init; }
    public int CatalogId { get; init; }
    public int Hp { get; init; }
    public int MaxHp { get; init; }
    public int Block { get; init; }
    public bool Alive { get; init; }
    public int IntentId { get; init; }
    public bool IntentKnown { get; init; }
    public int MoveBaseDamage { get; init; }
    public int MoveHits { get; init; }
    public List<Shalujianta.Sim.Combat.PowerStack> Powers { get; init; } = [];
}

public sealed class CardObservation
{
    public int CatalogId { get; init; }
    public int Uuid { get; init; }
    public int Upgrade { get; init; }
    public int CurrentCost { get; init; }
    public byte Flags { get; init; }
}

public readonly record struct BagCount(int CatalogId, int Count);
