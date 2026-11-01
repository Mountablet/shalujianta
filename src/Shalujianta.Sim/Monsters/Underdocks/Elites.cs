using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Monsters.Underdocks;

/// <summary>
/// Axe Raider. Wiki: HP 20-22; fixed cycle Swing (5 + 5 block) -> Swing -> Big Swing 12.
/// </summary>
public sealed class AxeRaiderAi : IMonsterAi
{
    public const int HpMin = 20;
    public const int HpMax = 22;

    public const int MoveSwing = 0;
    public const int MoveBigSwing = 1;

    public static readonly MonsterMove Swing = new(
        MoveSwing, IntentId.Attack, 5, 1, (w, s) => w.GainMonsterBlock(s, 5));
    public static readonly MonsterMove BigSwing = new(MoveBigSwing, IntentId.Attack, 12, 1);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        // Misc counts rolls: 0=Swing, 1=Swing, 2=Big Swing, then repeat.
        return MoveFor(world, slot, m.Misc % 3 == 2 ? MoveBigSwing : MoveSwing);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveBigSwing => BigSwing,
        _ => Swing,
    };
}

/// <summary>
/// Assassin Raider. Wiki: HP 18-23; Killshot 10 every turn.
/// </summary>
public sealed class AssassinRaiderAi : IMonsterAi
{
    public const int HpMin = 18;
    public const int HpMax = 23;

    public static readonly MonsterMove Killshot = new(0, IntentId.Attack, 10, 1);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot) => Killshot;

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => Killshot;
}

/// <summary>
/// Brute Raider. Wiki: HP 30-33; alternates Beat 7 / Clap (+3 Strength), starting Beat.
/// </summary>
public sealed class BruteRaiderAi : IMonsterAi
{
    public const int HpMin = 30;
    public const int HpMax = 33;

    public const int MoveBeat = 0;
    public const int MoveClap = 1;

    public static readonly MonsterMove Beat = new(MoveBeat, IntentId.Attack, 7, 1);
    public static readonly MonsterMove Clap = new(
        MoveClap, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 3));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return Beat;
        }

        return m.MoveId == MoveBeat ? Clap : Beat;
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) =>
        moveId == MoveClap ? Clap : Beat;
}

/// <summary>
/// Crossbow Raider. Wiki: HP 18-21; alternates Reload (3 block) / Fire! 14, starting Reload.
/// </summary>
public sealed class CrossbowRaiderAi : IMonsterAi
{
    public const int HpMin = 18;
    public const int HpMax = 21;

    public const int MoveReload = 0;
    public const int MoveFire = 1;

    public static readonly MonsterMove Reload = new(MoveReload, IntentId.Defend, 3, 1);
    public static readonly MonsterMove Fire = new(MoveFire, IntentId.Attack, 14, 1);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return Reload;
        }

        return m.MoveId == MoveReload ? Fire : Reload;
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) =>
        moveId == MoveFire ? Fire : Reload;
}

/// <summary>
/// Tracker Raider. Wiki: HP 21-25; opens Track (2 Frail), then Unleash the Hounds
/// (1 damage x8) every turn.
/// </summary>
public sealed class TrackerRaiderAi : IMonsterAi
{
    public const int HpMin = 21;
    public const int HpMax = 25;

    public const int MoveTrack = 0;
    public const int MoveUnleash = 1;

    public static readonly MonsterMove Track = new(
        MoveTrack, IntentId.Debuff, 0, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Frail, 2));
    public static readonly MonsterMove Unleash = new(MoveUnleash, IntentId.Attack, 1, 8);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        return m.Misc == 0 ? Track : Unleash;
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) =>
        moveId == MoveTrack ? Track : Unleash;
}
