namespace Shalujianta.Sim.Combat;

/// <summary>One enemy row at combat setup.</summary>
public sealed class MonsterSpawn
{
    public required int CatalogId { get; init; }
    public required int Hp { get; init; }

    /// <summary>-1 means same as Hp.</summary>
    public int MaxHp { get; init; } = -1;

    public required Func<IMonsterAi> AiFactory { get; init; }
}

/// <summary>
/// Combat setup spec (sim-side mirror of design §5.1 CombatStart; the Python
/// generator arrives in PR-009). Held by reference for Clone/Restore — it is
/// configuration, not state, and is never serialized.
/// </summary>
public sealed class CombatStart
{
    public int CharacterId { get; init; }
    public int Floor { get; init; } = 1;
    public int EncounterId { get; init; }
    public int PlayerHp { get; init; } = -1;
    public int PlayerMaxHp { get; init; } = 80;
    public int MaxEnergy { get; init; } = CombatRules.DefaultMaxEnergy;
    public List<CardInstance> Deck { get; init; } = [];
    public List<MonsterSpawn> Monsters { get; init; } = [];
    public List<int> RelicIds { get; init; } = [];
}
