using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Monsters.Underdocks;

/// <summary>
/// Skulking Colony (elite). Wiki: HP 75, Hardened Shell 20 (cannot lose more than
/// 20 HP per turn); fixed cycle Zoom 14 -> Zoom -> Inertia 9 + 2 Strength ->
/// Piercing Stabs 7x2.
/// </summary>
public sealed class SkulkingColonyAi : IMonsterAi
{
    public const int HpMin = 75;
    public const int HpMax = 75;

    public const int MoveZoom = 0;
    public const int MoveInertia = 1;
    public const int MovePiercingStabs = 2;

    public static readonly MonsterMove Zoom = new(MoveZoom, IntentId.Attack, 14, 1);
    public static readonly MonsterMove Inertia = new(
        MoveInertia, IntentId.Attack, 9, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 2));
    public static readonly MonsterMove PiercingStabs = new(MovePiercingStabs, IntentId.Attack, 7, 2);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        // Misc % 4: 0,1 = Zoom; 2 = Inertia; 3 = Piercing Stabs.
        return MoveFor(world, slot, (m.Misc % 4) switch { 2 => MoveInertia, 3 => MovePiercingStabs, _ => MoveZoom });
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveInertia => Inertia,
        MovePiercingStabs => PiercingStabs,
        _ => Zoom,
    };
}

/// <summary>
/// Phantasmal Gardener (elite, 4 appear together). Wiki: HP 26-31, Skittish 6 (first
/// time hit each turn: gains 6 block); all four cycle Bite 5 -> Lash 7 -> Flail 1x3 ->
/// Enlarge (+2 Strength), staggered: leftmost opens Flail, then Bite, Lash, Enlarge.
/// </summary>
public sealed class PhantasmalGardenerAi : IMonsterAi
{
    public const int HpMin = 26;
    public const int HpMax = 31;

    public const int MoveBite = 0;
    public const int MoveLash = 1;
    public const int MoveFlail = 2;
    public const int MoveEnlarge = 3;
    private const int MoveCount = 4;

    public static readonly MonsterMove Bite = new(MoveBite, IntentId.Attack, 5, 1);
    public static readonly MonsterMove Lash = new(MoveLash, IntentId.Attack, 7, 1);
    public static readonly MonsterMove Flail = new(MoveFlail, IntentId.Attack, 1, 3);
    public static readonly MonsterMove Enlarge = new(
        MoveEnlarge, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 2));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            // Openings by slot: 0=Flail, 1=Bite, 2=Lash, 3=Enlarge.
            int[] openings = { MoveFlail, MoveBite, MoveLash, MoveEnlarge };
            return MoveFor(world, slot, openings[slot % MoveCount]);
        }

        return MoveFor(world, slot, (m.MoveId + 1) % MoveCount);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveLash => Lash,
        MoveFlail => Flail,
        MoveEnlarge => Enlarge,
        _ => Bite,
    };
}

/// <summary>
/// Terror Eel (elite). Wiki: HP 140; alternates Crash 16 / Thrash 3x3 + 6 Vigor,
/// starting Crash. When HP first reaches its Shriek threshold (70) it becomes
/// Stunned for a turn, then uses Terror (99 Vulnerable), then resumes the cycle.
/// </summary>
public sealed class TerrorEelAi : IMonsterAi
{
    public const int HpMin = 140;
    public const int HpMax = 140;
    public const int ShriekThreshold = 70;

    public const int MoveCrash = 0;
    public const int MoveThrash = 1;
    public const int MoveStunned = 2;
    public const int MoveTerror = 3;

    public const int AiShriekStunned = 1;
    public const int AiShriekTerrorDone = 2;

    public static readonly MonsterMove Crash = new(MoveCrash, IntentId.Attack, 16, 1);
    public static readonly MonsterMove Thrash = new(
        MoveThrash, IntentId.Attack, 3, 3, (w, s) => w.ApplyMonsterPower(s, PowerIds.Vigor, 6));
    public static readonly MonsterMove Stunned = new(MoveStunned, IntentId.Defend, 0, 1);
    public static readonly MonsterMove Terror = new(
        MoveTerror, IntentId.Debuff, 0, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Vulnerable, 99));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.AiState == 0 && m.Hp <= ShriekThreshold && m.Misc > 0)
        {
            m.AiState = AiShriekStunned;
            world.Monsters[slot] = m;
            return Stunned;
        }

        if (m.AiState == AiShriekStunned)
        {
            m.AiState = AiShriekTerrorDone;
            world.Monsters[slot] = m;
            return Terror;
        }

        return m.Misc == 0 || m.MoveId == MoveThrash ? Crash : Thrash;
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveThrash => Thrash,
        MoveTerror => Terror,
        MoveStunned => Stunned,
        _ => Crash,
    };
}
