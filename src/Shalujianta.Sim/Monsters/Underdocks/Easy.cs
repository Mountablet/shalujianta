using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Monsters.Underdocks;

/// <summary>
/// Corpse Slug. Wiki (2026-11-01): HP 25-27; fixed cycle Whip Slap 3x2 -> Glomp 8 ->
/// Goop (2 Frail); group members start staggered (slug i opens on move i).
/// Ravenous (eating dead allies) is not implemented in v0 — documented simplification.
/// </summary>
public sealed class CorpseSlugAi : IMonsterAi
{
    public const int HpMin = 25;
    public const int HpMax = 27;

    public const int MoveWhipSlap = 0;
    public const int MoveGlomp = 1;
    public const int MoveGoop = 2;
    private const int MoveCount = 3;

    public static readonly MonsterMove WhipSlap = new(MoveWhipSlap, IntentId.Attack, 3, 2);
    public static readonly MonsterMove Glomp = new(MoveGlomp, IntentId.Attack, 8, 1);
    public static readonly MonsterMove Goop = new(
        MoveGoop, IntentId.Debuff, 0, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Frail, 2));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        int prev = m.Misc == 0 ? ((slot % MoveCount) + MoveCount - 1) % MoveCount : m.MoveId;
        return MoveFor(world, slot, (prev + 1) % MoveCount); // staggered stagger then rotate
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveGlomp => Glomp,
        MoveGoop => Goop,
        _ => WhipSlap,
    };
}

/// <summary>
/// Seapunk. Wiki: HP 44-46; fixed cycle Sea Kick 11 -> Spinning Kick 2x4 ->
/// Bubble Burp (7 block + 1 Strength).
/// </summary>
public sealed class SeapunkAi : IMonsterAi
{
    public const int HpMin = 44;
    public const int HpMax = 46;

    public const int MoveSeaKick = 0;
    public const int MoveSpinningKick = 1;
    public const int MoveBubbleBurp = 2;
    private const int MoveCount = 3;

    public static readonly MonsterMove SeaKick = new(MoveSeaKick, IntentId.Attack, 11, 1);
    public static readonly MonsterMove SpinningKick = new(MoveSpinningKick, IntentId.Attack, 2, 4);
    public static readonly MonsterMove BubbleBurp = new(
        MoveBubbleBurp, IntentId.Defend, 0, 1, (w, s) =>
        {
            w.GainMonsterBlock(s, 7);
            w.ApplyMonsterPower(s, PowerIds.Strength, 1);
        });

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        int prev = m.Misc == 0 ? MoveBubbleBurp : m.MoveId;
        return MoveFor(world, slot, (prev + 1) % MoveCount);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveSpinningKick => SpinningKick,
        MoveBubbleBurp => BubbleBurp,
        _ => SeaKick,
    };
}

/// <summary>
/// Sludge Spinner. Wiki: HP 37-39; always opens Oil Spray (8 + 1 Weak), then random
/// no-repeat among Oil Spray / Slam 11 / Rage (6 + 3 Strength).
/// </summary>
public sealed class SludgeSpinnerAi : IMonsterAi
{
    public const int HpMin = 37;
    public const int HpMax = 39;

    public const int MoveOilSpray = 0;
    public const int MoveSlam = 1;
    public const int MoveRage = 2;
    private const int MoveCount = 3;

    public static readonly MonsterMove OilSpray = new(
        MoveOilSpray, IntentId.Attack, 8, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Weak, 1));
    public static readonly MonsterMove Slam = new(MoveSlam, IntentId.Attack, 11, 1);
    public static readonly MonsterMove Rage = new(
        MoveRage, IntentId.Attack, 6, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 3));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return OilSpray;
        }

        while (true)
        {
            int roll = world.Rng.NextInt(RngStream.MonsterAi, MoveCount);
            if (roll != m.MoveId)
            {
                return MoveFor(world, slot, roll);
            }
        }
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveSlam => Slam,
        MoveRage => Rage,
        _ => OilSpray,
    };
}

/// <summary>
/// Toadpole. Wiki: HP 21-25; fixed cycle Whirl 7 -> Spiken (+2 Thorns) -> Spike Spit
/// 3x3 (removes 2 Thorns); the front Toadpole of the easy pair opens with Spiken.
/// Thorns reflects attack damage back at the player.
/// </summary>
public sealed class ToadpoleAi : IMonsterAi
{
    public const int HpMin = 21;
    public const int HpMax = 25;

    public const int MoveWhirl = 0;
    public const int MoveSpiken = 1;
    public const int MoveSpikeSpit = 2;
    private const int MoveCount = 3;

    public static readonly MonsterMove Whirl = new(MoveWhirl, IntentId.Attack, 7, 1);
    public static readonly MonsterMove Spiken = new(
        MoveSpiken, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Thorns, 2));
    public static readonly MonsterMove SpikeSpit = new(
        MoveSpikeSpit, IntentId.Attack, 3, 3, (w, s) => w.ApplyMonsterPower(s, PowerIds.Thorns, -2));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            // Easy pair: front (slot 0) opens Spiken; otherwise the cycle opens Whirl.
            return MoveFor(world, slot, world.Monsters.Count == 2 && slot == 0 ? MoveSpiken : MoveWhirl);
        }

        return MoveFor(world, slot, (m.MoveId + 1) % MoveCount);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveSpiken => Spiken,
        MoveSpikeSpit => SpikeSpit,
        _ => Whirl,
    };
}
