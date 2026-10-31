using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Combat;

public enum CardType : byte
{
    Attack,
    Skill,
    Power,
    Status,
    Curse,
}

public enum CardTarget : byte
{
    /// <summary>No target: AOE and self effects fold into slot 0 (§4.9).</summary>
    None,
    Enemy,
    Player,
    AllEnemies,
}

/// <summary>
/// One static Apply per implemented card (§4.11). Apply must be deterministic and
/// allocation-free; zone movement after Apply is handled by CombatWorld.
/// </summary>
public delegate void CardApply(CombatWorld world, CardInstance card, TargetRef target);

/// <summary>Unimplemented cards carry Apply == null and are rejected before mutation.</summary>
public readonly record struct CardDef(
    int CatalogId,
    CardType Type,
    int BaseCost,
    CardTarget Target,
    CardFlags Flags,
    CardApply? Apply);

/// <summary>
/// CatalogId -> CardDef. Registered once at startup (tests, then PR-005 real cards).
/// Read-only after setup; per-world state never lives here.
/// </summary>
public static class CardRegistry
{
    private static readonly Dictionary<int, CardDef> Defs = [];

    public static void Register(CardDef def) => Defs[def.CatalogId] = def;

    public static bool TryGet(int catalogId, out CardDef def) => Defs.TryGetValue(catalogId, out def);

    public static IReadOnlyDictionary<int, CardDef> All => Defs;
}

/// <summary>Builds card instances from registered defs with sequential uuids.</summary>
public sealed class DeckBuilder
{
    private readonly List<CardInstance> _cards = [];
    private int _uuid = 1;

    public DeckBuilder Add(int catalogId, int count = 1, int upgrade = 0)
    {
        if (!CardRegistry.TryGet(catalogId, out var def))
        {
            throw new KeyNotFoundException($"card {catalogId} is not registered");
        }

        for (int i = 0; i < count; i++)
        {
            _cards.Add(new CardInstance
            {
                CatalogId = catalogId,
                Uuid = _uuid++,
                Upgrade = upgrade,
                Flags = def.Flags,
                CurrentCost = def.BaseCost,
            });
        }

        return this;
    }

    public List<CardInstance> Build() => [.. _cards];
}
