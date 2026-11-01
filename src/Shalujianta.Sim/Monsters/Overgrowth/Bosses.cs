using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Monsters.Overgrowth;

/// <summary>
/// Bygone Effigy (elite). Wiki: HP 127; turn 1 Sleep (a plain no-op move, not the
/// Asleep power), turn 2 Wake (+10 Strength), then Slashes 13 every turn. Slow: the
/// player's attacks deal +10% per card played this turn (spawns with Slow 1; damage
/// uses stacks-1 so the first card still rounds to base damage).
/// </summary>
public sealed class BygoneEffigyAi : IMonsterAi
{
    public const int HpMin = 127;
    public const int HpMax = 127;

    public const int MoveSleep = 0;
    public const int MoveWake = 1;
    public const int MoveSlashes = 2;

    public static readonly MonsterMove Sleep = new(MoveSleep, IntentId.Defend, 0, 1);
    public static readonly MonsterMove Wake = new(
        MoveWake, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 10));
    public static readonly MonsterMove Slashes = new(MoveSlashes, IntentId.Attack, 13, 1);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        return m.Misc switch
        {
            0 => Sleep,
            1 => Wake,
            _ => Slashes,
        };
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveWake => Wake,
        MoveSlashes => Slashes,
        _ => Sleep,
    };
}

/// <summary>
/// Byrdonis (elite). Wiki: HP 81-84, Territorial 1 (gains its stacks as Strength at
/// the end of its own turns); alternates Swoop 17 / Peck 3x3.
/// </summary>
public sealed class ByrdonisAi : IMonsterAi
{
    public const int HpMin = 81;
    public const int HpMax = 84;

    public const int MoveSwoop = 0;
    public const int MovePeck = 1;

    public static readonly MonsterMove Swoop = new(MoveSwoop, IntentId.Attack, 17, 1);
    public static readonly MonsterMove Peck = new(MovePeck, IntentId.Attack, 3, 3);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return Swoop;
        }

        return m.MoveId == MoveSwoop ? Peck : Swoop;
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) =>
        moveId == MovePeck ? Peck : Swoop;
}

/// <summary>
/// Phrog Parasite (elite). Wiki: HP 61-64; alternates Infect (3 Infection into the
/// discard pile) / Lash 4x4. Infested (4 Wrigglers on death) is not implemented in
/// v0 — the fight simply ends on its death (documented simplification).
/// </summary>
public sealed class PhrogParasiteAi : IMonsterAi
{
    public const int HpMin = 61;
    public const int HpMax = 64;

    public const int MoveInfect = 0;
    public const int MoveLash = 1;

    public static readonly MonsterMove Infect = new(
        MoveInfect, IntentId.Debuff, 0, 1, (w, s) => w.AddCardsToDiscard(CardIds.Infection, 3));
    public static readonly MonsterMove Lash = new(MoveLash, IntentId.Attack, 4, 4);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return Infect;
        }

        return m.MoveId == MoveInfect ? Lash : Infect;
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) =>
        moveId == MoveLash ? Lash : Infect;
}

/// <summary>
/// Ceremonial Beast (boss). Wiki: HP 252; turn 1 Stamp (sets the 150 HP Plow
/// threshold), then Plow 18 + 2 Strength until HP first reaches the threshold, where
/// it is Stunned for a turn and loses all Strength; then the cycle Beast Cry (1
/// Ringing) -> Stomp 15 -> Crush 17 + 3 Strength.
/// </summary>
public sealed class CeremonialBeastAi : IMonsterAi
{
    public const int HpMin = 252;
    public const int HpMax = 252;
    public const int PlowThreshold = 150;

    public const int MoveStamp = 0;
    public const int MovePlow = 1;
    public const int MoveStunned = 2;
    public const int MoveBeastCry = 3;
    public const int MoveStomp = 4;
    public const int MoveCrush = 5;

    public const int AiPhaseTwo = 1;
    public const int AiStunned = 2;

    public static readonly MonsterMove Stamp = new(
        MoveStamp, IntentId.Buff, 0, 1, (w, s) => w.Log("plow_threshold_set: 150"));
    public static readonly MonsterMove Plow = new(
        MovePlow, IntentId.Attack, 18, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 2));
    public static readonly MonsterMove Stunned = new(MoveStunned, IntentId.Defend, 0, 1);
    public static readonly MonsterMove BeastCry = new(
        MoveBeastCry, IntentId.Debuff, 0, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Ringing, 1));
    public static readonly MonsterMove Stomp = new(MoveStomp, IntentId.Attack, 15, 1);
    public static readonly MonsterMove Crush = new(
        MoveCrush, IntentId.Attack, 17, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 3));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return Stamp;
        }

        if ((m.AiState & AiPhaseTwo) == 0)
        {
            if (m.Hp <= PlowThreshold)
            {
                if ((m.AiState & AiStunned) == 0)
                {
                    m.AiState |= AiStunned;
                    world.Monsters[slot] = m;
                    return Stunned;
                }

                // Waking from the stun: wipe Phase-1 Strength, enter phase two.
                w_remove_strength(world, slot);
                m = world.Monsters[slot];
                m.AiState |= AiPhaseTwo;
                world.Monsters[slot] = m;
                return BeastCry;
            }

            return Plow;
        }

        // Phase two cycle: Beast Cry -> Stomp -> Crush.
        return MoveFor(world, slot, m.MoveId switch
        {
            MoveBeastCry => MoveStomp,
            MoveStomp => MoveCrush,
            _ => MoveBeastCry,
        });
    }

    private static void w_remove_strength(CombatWorld world, int slot) =>
        world.RemoveMonsterPower(slot, PowerIds.Strength);

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveStunned => Stunned,
        MoveBeastCry => BeastCry,
        MoveStomp => Stomp,
        MoveCrush => Crush,
        MovePlow => Plow,
        _ => Stamp,
    };
}

/// <summary>
/// The Kin (boss): Kin Priest (HP 190) + two Kin Followers (HP 58-59). Minion rules
/// (followers leaving when the priest dies) are not modeled in v0. Fixed cycles;
/// the followers open offset (Quick Slash / Power Dance).
/// </summary>
public sealed class KinPriestAi : IMonsterAi
{
    public const int HpMin = 190;
    public const int HpMax = 190;

    public const int MoveOrbOfFrailty = 0;
    public const int MoveOrbOfWeakness = 1;
    public const int MoveSoulBeam = 2;
    public const int MoveDarkRitual = 3;
    private const int MoveCount = 4;

    public static readonly MonsterMove OrbOfFrailty = new(
        MoveOrbOfFrailty, IntentId.Attack, 8, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Frail, 1));
    public static readonly MonsterMove OrbOfWeakness = new(
        MoveOrbOfWeakness, IntentId.Attack, 8, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Weak, 1));
    public static readonly MonsterMove SoulBeam = new(MoveSoulBeam, IntentId.Attack, 3, 3);
    public static readonly MonsterMove DarkRitual = new(
        MoveDarkRitual, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 2));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        int prev = m.Misc == 0 ? MoveDarkRitual : m.MoveId;
        return MoveFor(world, slot, (prev + 1) % MoveCount);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveOrbOfWeakness => OrbOfWeakness,
        MoveSoulBeam => SoulBeam,
        MoveDarkRitual => DarkRitual,
        _ => OrbOfFrailty,
    };
}

public sealed class KinFollowerAi : IMonsterAi
{
    public const int HpMin = 58;
    public const int HpMax = 59;

    public const int MoveQuickSlash = 0;
    public const int MoveBoomerang = 1;
    public const int MovePowerDance = 2;
    private const int MoveCount = 3;

    public static readonly MonsterMove QuickSlash = new(MoveQuickSlash, IntentId.Attack, 5, 1);
    public static readonly MonsterMove Boomerang = new(MoveBoomerang, IntentId.Attack, 2, 2);
    public static readonly MonsterMove PowerDance = new(
        MovePowerDance, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 2));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            // Offset openings: slot 1 opens Quick Slash, slot 2 opens Power Dance.
            return MoveFor(world, slot, slot == 1 ? MoveQuickSlash : MovePowerDance);
        }

        return MoveFor(world, slot, (m.MoveId + 1) % MoveCount);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveBoomerang => Boomerang,
        MovePowerDance => PowerDance,
        _ => QuickSlash,
    };
}

/// <summary>
/// Vantom (boss). Wiki: HP 173, Slippery 9; fixed cycle Ink Blot 7 -> Inky Lance 6x2
/// -> Dismember 27 + 3 Wound -> Prepare (+2 Strength).
/// </summary>
public sealed class VantomAi : IMonsterAi
{
    public const int HpMin = 173;
    public const int HpMax = 173;

    public const int MoveInkBlot = 0;
    public const int MoveInkyLance = 1;
    public const int MoveDismember = 2;
    public const int MovePrepare = 3;
    private const int MoveCount = 4;

    public static readonly MonsterMove InkBlot = new(MoveInkBlot, IntentId.Attack, 7, 1);
    public static readonly MonsterMove InkyLance = new(MoveInkyLance, IntentId.Attack, 6, 2);
    public static readonly MonsterMove Dismember = new(
        MoveDismember, IntentId.Attack, 27, 1, (w, s) => w.AddCardsToDiscard(CardIds.Wound, 3));
    public static readonly MonsterMove Prepare = new(
        MovePrepare, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 2));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        int prev = m.Misc == 0 ? MovePrepare : m.MoveId;
        return MoveFor(world, slot, (prev + 1) % MoveCount);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveInkyLance => InkyLance,
        MoveDismember => Dismember,
        MovePrepare => Prepare,
        _ => InkBlot,
    };
}
