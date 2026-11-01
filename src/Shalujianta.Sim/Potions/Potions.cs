using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Potions;

/// <summary>One static Apply per implemented potion (v0: FirePotion, BlockPotion).</summary>
public delegate void PotionApply(CombatWorld world, TargetRef target);

/// <summary>Unimplemented potions carry Apply == null and are rejected before mutation.</summary>
public readonly record struct PotionDef(int CatalogId, bool RequiresTarget, PotionApply? Apply);

public static class PotionRegistry
{
    private static readonly Dictionary<int, PotionDef> Defs = [];

    public static void Register(PotionDef def) => Defs[def.CatalogId] = def;

    public static bool TryGet(int catalogId, out PotionDef def) => Defs.TryGetValue(catalogId, out def!);
}

/// <summary>
/// Fire Potion. Wiki value (STS2:Fire_Potion, 2026-10-31): deal 20 damage, thrown
/// at an enemy of choice (targeted). Pending dll verification in PR-014.
/// </summary>
public static class FirePotion
{
    public const int Damage = 20;

    public static void Apply(CombatWorld world, TargetRef target) =>
        world.DamageMonster(target.Index, Damage, world.PlayerPowers);

    public static void Register() =>
        PotionRegistry.Register(new PotionDef(PotionIds.FirePotion, RequiresTarget: true, Apply));
}

/// <summary>
/// Block Potion. Wiki value (STS2:Block_Potion, 2026-10-31): gain 12 Block, no target.
/// Pending dll verification in PR-014.
/// </summary>
public static class BlockPotion
{
    public const int Block = 12;

    public static void Apply(CombatWorld world, TargetRef target) => world.GainPlayerBlock(Block);

    public static void Register() =>
        PotionRegistry.Register(new PotionDef(PotionIds.BlockPotion, RequiresTarget: false, Apply));
}

/// <summary>Registers every v0 potion. Call once per process (SliceV0.Register covers it).</summary>
public static class V0Potions
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        FirePotion.Register();
        BlockPotion.Register();
    }
}
