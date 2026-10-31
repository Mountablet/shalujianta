namespace Shalujianta.Sim.Combat;

[Flags]
public enum CardFlags : byte
{
    None = 0,
    Exhausts = 1,
    Ethereal = 2,
    Retain = 4,
    Innate = 8,
    Unplayable = 16,
    XCost = 32,
    Status = 64,
    Curse = 128,
}

/// <summary>
/// Value card instance (§4.2). CatalogId is the full 32-bit entity id
/// (namespace &lt;&lt; 24 | local_id); hot-path table lookups use local = id &amp; 0x00FFFFFF.
/// Struct-in-list: no per-card heap allocation.
/// </summary>
public readonly record struct CardInstance
{
    public required int CatalogId { get; init; }
    public required int Uuid { get; init; }
    public int Upgrade { get; init; }
    public CardFlags Flags { get; init; }

    /// <summary>Resolved cost: -2 unplayable, -1 X, otherwise 0..9.</summary>
    public int CurrentCost { get; init; }

    /// <summary>Free per-instance scratch (enchantment id, generated-from, etc.).</summary>
    public int Misc { get; init; }

    public bool HasFlag(CardFlags flag) => (Flags & flag) != 0;
}
