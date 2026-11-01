using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Monsters.Overgrowth;

/// <summary>
/// Inklet. Wiki: HP 11-17, starts with 1 Slippery (next HP loss is only 1);
/// Jab 3 / Whirlwind 2x3 / Piercing Gaze 10. The outer two open Jab, the middle
/// (slot 1 of 3) opens Whirlwind; afterwards each alternates between Jab and an
/// even coin flip between Piercing Gaze or Whirlwind.
/// </summary>
public sealed class InkletAi : IMonsterAi
{
    public const int HpMin = 11;
    public const int HpMax = 17;

    public const int MoveJab = 0;
    public const int MoveWhirlwind = 1;
    public const int MovePiercingGaze = 2;

    public static readonly MonsterMove Jab = new(MoveJab, IntentId.Attack, 3, 1);
    public static readonly MonsterMove Whirlwind = new(MoveWhirlwind, IntentId.Attack, 2, 3);
    public static readonly MonsterMove PiercingGaze = new(MovePiercingGaze, IntentId.Attack, 10, 1);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return MoveFor(world, slot, slot == 1 ? MoveWhirlwind : MoveJab);
        }

        if (m.MoveId == MoveJab)
        {
            // Even coin flip between Piercing Gaze and Whirlwind.
            return MoveFor(world, slot, world.Rng.NextInt(RngStream.MonsterAi, 2) == 0 ? MovePiercingGaze : MoveWhirlwind);
        }

        return Jab; // alternates back to Jab
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveWhirlwind => Whirlwind,
        MovePiercingGaze => PiercingGaze,
        _ => Jab,
    };
}

/// <summary>
/// Mawler. Wiki: HP 72; always opens Claw 4x2, then random with equal chance among
/// Rip and Tear 14 / Roar 3 Vulnerable (once per fight) / Claw; no move twice in a row.
/// </summary>
public sealed class MawlerAi : IMonsterAi
{
    public const int HpMin = 72;
    public const int HpMax = 72;

    public const int MoveRipAndTear = 0;
    public const int MoveRoar = 1;
    public const int MoveClaw = 2;
    private const int MoveCount = 3;

    public const int AiRoarUsed = 1;

    public static readonly MonsterMove RipAndTear = new(MoveRipAndTear, IntentId.Attack, 14, 1);
    public static readonly MonsterMove Roar = new(
        MoveRoar, IntentId.Debuff, 0, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Vulnerable, 3));
    public static readonly MonsterMove Claw = new(MoveClaw, IntentId.Attack, 4, 2);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return Claw;
        }

        while (true)
        {
            int roll = world.Rng.NextInt(RngStream.MonsterAi, MoveCount);
            if (roll == m.MoveId)
            {
                continue; // no repeats
            }

            if (roll == MoveRoar && (m.AiState & AiRoarUsed) != 0)
            {
                continue; // Roar once per fight
            }

            if (roll == MoveRoar)
            {
                m.AiState |= AiRoarUsed;
                world.Monsters[slot] = m;
            }

            return MoveFor(world, slot, roll);
        }
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveRoar => Roar,
        MoveClaw => Claw,
        _ => RipAndTear,
    };
}

/// <summary>
/// Flyconid. Wiki: HP 47-49; turn 1 picks Frail Spores (8 dmg + 2 Frail, 2/3) or
/// Smash (11, 1/3); later Weakening Spores 2 Vuln (3/6), Frail Spores (2/6), Smash (1/6);
/// never the same move twice in a row.
/// </summary>
public sealed class FlyconidAi : IMonsterAi
{
    public const int HpMin = 47;
    public const int HpMax = 49;

    public const int MoveWeakeningSpores = 0;
    public const int MoveFrailSpores = 1;
    public const int MoveSmash = 2;

    public static readonly MonsterMove WeakeningSpores = new(
        MoveWeakeningSpores, IntentId.Debuff, 0, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Vulnerable, 2));
    public static readonly MonsterMove FrailSpores = new(
        MoveFrailSpores, IntentId.Attack, 8, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Frail, 2));
    public static readonly MonsterMove Smash = new(MoveSmash, IntentId.Attack, 11, 1);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            // 2/3 Frail Spores, 1/3 Smash (never Weakening Spores turn 1).
            int roll = world.Rng.NextInt(RngStream.MonsterAi, 3);
            return MoveFor(world, slot, roll == 2 ? MoveSmash : MoveFrailSpores);
        }

        while (true)
        {
            // 3/6 / 2/6 / 1/6.
            int roll = world.Rng.NextInt(RngStream.MonsterAi, 6);
            int move = roll switch
            {
                < 3 => MoveWeakeningSpores,
                < 5 => MoveFrailSpores,
                _ => MoveSmash,
            };
            if (move != m.MoveId)
            {
                return MoveFor(world, slot, move);
            }
        }
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveFrailSpores => FrailSpores,
        MoveSmash => Smash,
        _ => WeakeningSpores,
    };
}

/// <summary>
/// Snapping Jaxfruit. Wiki: HP 31-33; Energy Orb (3 damage + 2 Strength) every turn.
/// </summary>
public sealed class SnappingJaxfruitAi : IMonsterAi
{
    public const int HpMin = 31;
    public const int HpMax = 33;

    public const int MoveEnergyOrb = 0;

    public static readonly MonsterMove EnergyOrb = new(
        MoveEnergyOrb, IntentId.Attack, 3, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 2));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot) => EnergyOrb;

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => EnergyOrb;
}

/// <summary>
/// Vine Shambler. Wiki: HP 61; fixed cycle Swipe 6x2 -> Grasping Vines 8 + 1 Tangled
/// -> Chomp 16 -> repeat. Tangled: entangled cards cost +1 energy (v0: a decaying
/// player debuff that surcharges every card while it has stacks).
/// </summary>
public sealed class VineShamblerAi : IMonsterAi
{
    public const int HpMin = 61;
    public const int HpMax = 61;

    public const int MoveSwipe = 0;
    public const int MoveGraspingVines = 1;
    public const int MoveChomp = 2;
    private const int MoveCount = 3;

    public static readonly MonsterMove Swipe = new(MoveSwipe, IntentId.Attack, 6, 2);
    public static readonly MonsterMove GraspingVines = new(
        MoveGraspingVines, IntentId.Attack, 8, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Tangled, 1));
    public static readonly MonsterMove Chomp = new(MoveChomp, IntentId.Attack, 16, 1);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        int prev = m.Misc == 0 ? MoveChomp : m.MoveId; // cycle opens with Swipe
        return MoveFor(world, slot, (prev + 1) % MoveCount);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveGraspingVines => GraspingVines,
        MoveChomp => Chomp,
        _ => Swipe,
    };
}

/// <summary>
/// Fogmog. Wiki: HP 74; always opens Illusory Spores (summons an Eye With Teeth -
/// summoning is not implemented in v0, so the turn resolves as a logged no-op like
/// Call for Backup), then Thwack 8 + 1 Str, then alternates Thwack / Headbutt 14.
/// </summary>
public sealed class FogmogAi : IMonsterAi
{
    public const int HpMin = 74;
    public const int HpMax = 74;

    public const int MoveIllusorySpores = 0;
    public const int MoveThwack = 1;
    public const int MoveHeadbutt = 2;
    private const int MoveCount = 3;

    public static readonly MonsterMove IllusorySpores = new(MoveIllusorySpores, IntentId.Unknown, 0, 1);
    public static readonly MonsterMove Thwack = new(
        MoveThwack, IntentId.Attack, 8, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 1));
    public static readonly MonsterMove Headbutt = new(MoveHeadbutt, IntentId.Attack, 14, 1);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return IllusorySpores; // stub: summon not implemented (logged no-op)
        }

        // Strict alternation Thwack/Headbutt (the wiki's no-repeat and always-follow
        // rules agree once only two moves remain).
        return m.MoveId == MoveThwack ? Headbutt : Thwack;
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveThwack => Thwack,
        MoveHeadbutt => Headbutt,
        _ => IllusorySpores,
    };
}

/// <summary>
/// Cubex Construct. Wiki: HP 65, starts with Artifact 1; opens Charge Up (+2 Str),
/// then two Repeater Blasts (7 + 2 Str each), then Expel Blast 5x2, then repeats
/// Repeater, Repeater, Expel for the rest of the fight.
/// </summary>
public sealed class CubexConstructAi : IMonsterAi
{
    public const int HpMin = 65;
    public const int HpMax = 65;

    public const int MoveChargeUp = 0;
    public const int MoveRepeaterBlast = 1;
    public const int MoveExpelBlast = 2;

    public const int AiPhaseStep = 0; // counts moves since the opener, modulo the cycle

    public static readonly MonsterMove ChargeUp = new(
        MoveChargeUp, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 2));
    public static readonly MonsterMove RepeaterBlast = new(
        MoveRepeaterBlast, IntentId.Attack, 7, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 2));
    public static readonly MonsterMove ExpelBlast = new(MoveExpelBlast, IntentId.Attack, 5, 2);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        // 0 = opener Charge Up; after that the 1->2->3 cycle (Repeater, Repeater,
        // Expel) repeats WITHOUT Charge Up, per the wiki.
        var m = world.Monsters[slot];
        int step = m.AiState;
        int move = step == 0 ? MoveChargeUp : step == 3 ? MoveExpelBlast : MoveRepeaterBlast;
        m.AiState = step == 3 ? 1 : step + 1;
        world.Monsters[slot] = m;
        return MoveFor(world, slot, move);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveRepeaterBlast => RepeaterBlast,
        MoveExpelBlast => ExpelBlast,
        _ => ChargeUp,
    };
}

/// <summary>
/// Slithering Strangler. Wiki: HP 53-55; always opens Constrict (3 stacks: take X
/// damage at the end of your turn while the applier is alive), then alternates
/// Constrict with a 50/50 Thwack (7 + 5 block) / Lash (12).
/// </summary>
public sealed class SlitheringStranglerAi : IMonsterAi
{
    public const int HpMin = 53;
    public const int HpMax = 55;

    public const int MoveConstrict = 0;
    public const int MoveThwack = 1;
    public const int MoveLash = 2;

    public static readonly MonsterMove Constrict = new(
        MoveConstrict, IntentId.Debuff, 0, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Constrict, 3));
    public static readonly MonsterMove Thwack = new(
        MoveThwack, IntentId.Attack, 7, 1, (w, s) => w.GainMonsterBlock(s, 5));
    public static readonly MonsterMove Lash = new(MoveLash, IntentId.Attack, 12, 1);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0 || m.MoveId != MoveConstrict)
        {
            return Constrict; // opener, and after every attack
        }

        return MoveFor(world, slot, world.Rng.NextInt(RngStream.MonsterAi, 2) == 0 ? MoveThwack : MoveLash);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveThwack => Thwack,
        MoveLash => Lash,
        _ => Constrict,
    };
}
