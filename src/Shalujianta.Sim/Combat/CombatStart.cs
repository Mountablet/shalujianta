namespace Shalujianta.Sim.Combat;

/// <summary>One enemy row at combat setup. HP is either exact (Hp) or rolled on the MonsterAi stream (HpMin/HpMax).</summary>
public sealed class MonsterSpawn
{
    public required int CatalogId { get; init; }
    public int Hp { get; init; } = -1;

    /// <summary>Inclusive roll range; ignored when Hp is set.</summary>
    public int HpMin { get; init; } = -1;
    public int HpMax { get; init; } = -1;

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

    /// <summary>Starting HP; -1 means full. Mutable so generators can set the carried HP.</summary>
    public int PlayerHp { get; set; } = -1;
    public int PlayerMaxHp { get; init; } = 80;
    public int MaxEnergy { get; init; } = CombatRules.DefaultMaxEnergy;

    /// <summary>Potion slot count (SliceV0: 3, starting empty).</summary>
    public int PotionSlots { get; init; } = 3;

    /// <summary>Catalog ids filling the first slots at combat start; 0 rows are skipped.</summary>
    public List<int> StartingPotions { get; init; } = [];
    public List<CardInstance> Deck { get; init; } = [];
    public List<MonsterSpawn> Monsters { get; init; } = [];
    public List<int> RelicIds { get; init; } = [];
}
