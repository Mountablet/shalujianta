using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Monsters.Underdocks;

/// <summary>
/// Calcified Cultist. Wiki: HP 38-41; Incantation (+2 Ritual) once, then Dark Strike 9.
/// Ritual grants its stacks as Strength at the end of each of its turns.
/// </summary>
public sealed class CalcifiedCultistAi : IMonsterAi
{
    public const int HpMin = 38;
    public const int HpMax = 41;

    public const int MoveIncantation = 0;
    public const int MoveDarkStrike = 1;

    public static readonly MonsterMove Incantation = new(
        MoveIncantation, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Ritual, 2));
    public static readonly MonsterMove DarkStrike = new(MoveDarkStrike, IntentId.Attack, 9, 1);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        return m.Misc == 0 ? Incantation : DarkStrike;
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) =>
        moveId == MoveIncantation ? Incantation : DarkStrike;
}

/// <summary>
/// Damp Cultist. Wiki: HP 51-53; Incantation (+5 Ritual) once, then Dark Strike 1.
/// </summary>
public sealed class DampCultistAi : IMonsterAi
{
    public const int HpMin = 51;
    public const int HpMax = 53;

    public const int MoveIncantation = 0;
    public const int MoveDarkStrike = 1;

    public static readonly MonsterMove Incantation = new(
        MoveIncantation, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Ritual, 5));
    public static readonly MonsterMove DarkStrike = new(MoveDarkStrike, IntentId.Attack, 1, 1);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        return m.Misc == 0 ? Incantation : DarkStrike;
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) =>
        moveId == MoveIncantation ? Incantation : DarkStrike;
}

/// <summary>
/// Living Fog. Wiki: HP 80; opens Advanced Gas (8 + 1 Smoggy), then alternates Bloat 5
/// (summons a Gas Bomb — summon not implemented in v0) and Super Gas Blast 8.
/// Smoggy: the player may play at most 1 Skill per turn while it has stacks.
/// </summary>
public sealed class LivingFogAi : IMonsterAi
{
    public const int HpMin = 80;
    public const int HpMax = 80;

    public const int MoveAdvancedGas = 0;
    public const int MoveBloat = 1;
    public const int MoveSuperGasBlast = 2;

    public static readonly MonsterMove AdvancedGas = new(
        MoveAdvancedGas, IntentId.Attack, 8, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Smoggy, 1));
    public static readonly MonsterMove Bloat = new(MoveBloat, IntentId.Attack, 5, 1);
    public static readonly MonsterMove SuperGasBlast = new(MoveSuperGasBlast, IntentId.Attack, 8, 1);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return AdvancedGas;
        }

        return m.MoveId == MoveBloat ? SuperGasBlast : Bloat;
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveBloat => Bloat,
        MoveSuperGasBlast => SuperGasBlast,
        _ => AdvancedGas,
    };
}

/// <summary>
/// Fossil Stalker. Wiki: HP 51-53, starts with 3 Suck (unblocked attack damage it
/// deals becomes Strength); opens Latch 12, then equal random among Latch / Tackle
/// (9 + 1 Frail) / Lash 3x2, repeats allowed.
/// </summary>
public sealed class FossilStalkerAi : IMonsterAi
{
    public const int HpMin = 51;
    public const int HpMax = 53;

    public const int MoveLatch = 0;
    public const int MoveTackle = 1;
    public const int MoveLash = 2;
    private const int MoveCount = 3;

    public static readonly MonsterMove Latch = new(MoveLatch, IntentId.Attack, 12, 1);
    public static readonly MonsterMove Tackle = new(
        MoveTackle, IntentId.Attack, 9, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Frail, 1));
    public static readonly MonsterMove Lash = new(MoveLash, IntentId.Attack, 3, 2);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return Latch;
        }

        return MoveFor(world, slot, world.Rng.NextInt(RngStream.MonsterAi, MoveCount));
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveTackle => Tackle,
        MoveLash => Lash,
        _ => Latch,
    };
}

/// <summary>
/// Gremlin Merc. Wiki: HP 47-49; fixed cycle Gimme 7x2 -> Double Smash 6x2 + 2 Weak ->
/// Hehe 8 + 2 Strength. Thievery (steals gold on attack) is logged only in v0 (no gold
/// economy inside combat). Death summons (Fat/Sneaky Gremlin) are not implemented.
/// </summary>
public sealed class GremlinMercAi : IMonsterAi
{
    public const int HpMin = 47;
    public const int HpMax = 49;

    public const int MoveGimme = 0;
    public const int MoveDoubleSmash = 1;
    public const int MoveHehe = 2;
    private const int MoveCount = 3;

    public static readonly MonsterMove Gimme = new(
        MoveGimme, IntentId.Attack, 7, 2, (w, s) => w.Log("thievery: gold stolen (not modeled in v0)"));
    public static readonly MonsterMove DoubleSmash = new(
        MoveDoubleSmash, IntentId.Attack, 6, 2, (w, s) => w.ApplyPlayerPower(PowerIds.Weak, 2));
    public static readonly MonsterMove Hehe = new(
        MoveHehe, IntentId.Attack, 8, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 2));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        int prev = m.Misc == 0 ? MoveHehe : m.MoveId;
        return MoveFor(world, slot, (prev + 1) % MoveCount);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveDoubleSmash => DoubleSmash,
        MoveHehe => Hehe,
        _ => Gimme,
    };
}

/// <summary>
/// Haunted Ship. Wiki: HP 63; always opens Haunt (5 Dazed into the discard pile +
/// 3 Weak), then alternates Swipe 13 / Stomp 4x3 starting with Swipe.
/// </summary>
public sealed class HauntedShipAi : IMonsterAi
{
    public const int HpMin = 63;
    public const int HpMax = 63;

    public const int MoveHaunt = 0;
    public const int MoveSwipe = 1;
    public const int MoveStomp = 2;

    public static readonly MonsterMove Haunt = new(
        MoveHaunt, IntentId.Debuff, 0, 1, (w, s) =>
        {
            w.AddCardsToDiscard(CardIds.Dazed, 5);
            w.ApplyPlayerPower(PowerIds.Weak, 3);
        });
    public static readonly MonsterMove Swipe = new(MoveSwipe, IntentId.Attack, 13, 1);
    public static readonly MonsterMove Stomp = new(MoveStomp, IntentId.Attack, 4, 3);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return Haunt;
        }

        return m.MoveId == MoveSwipe ? Stomp : Swipe;
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveSwipe => Swipe,
        MoveStomp => Stomp,
        _ => Haunt,
    };
}

/// <summary>
/// Punch Construct. Wiki: HP 55, innate Artifact 1; fixed cycle READY (10 block) ->
/// Fast Punch 5x2 + 1 Frail -> Strong Punch 14.
/// </summary>
public sealed class PunchConstructAi : IMonsterAi
{
    public const int HpMin = 55;
    public const int HpMax = 55;

    public const int MoveReady = 0;
    public const int MoveFastPunch = 1;
    public const int MoveStrongPunch = 2;
    private const int MoveCount = 3;

    public static readonly MonsterMove Ready = new(MoveReady, IntentId.Defend, 10, 1);
    public static readonly MonsterMove FastPunch = new(
        MoveFastPunch, IntentId.Attack, 5, 2, (w, s) => w.ApplyPlayerPower(PowerIds.Frail, 1));
    public static readonly MonsterMove StrongPunch = new(MoveStrongPunch, IntentId.Attack, 14, 1);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        int prev = m.Misc == 0 ? MoveStrongPunch : m.MoveId;
        return MoveFor(world, slot, (prev + 1) % MoveCount);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveFastPunch => FastPunch,
        MoveStrongPunch => StrongPunch,
        _ => Ready,
    };
}

/// <summary>
/// Sewer Clam. Wiki: HP 56, Plating 8 (gains its Plating as block at the end of its
/// own turn, loses 1 Plating at the start of its turn); opens Jet 10, then alternates
/// Pressurize (+4 Strength) / Jet.
/// </summary>
public sealed class SewerClamAi : IMonsterAi
{
    public const int HpMin = 56;
    public const int HpMax = 56;

    public const int MoveJet = 0;
    public const int MovePressurize = 1;

    public static readonly MonsterMove Jet = new(MoveJet, IntentId.Attack, 10, 1);
    public static readonly MonsterMove Pressurize = new(
        MovePressurize, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 4));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return Jet;
        }

        return m.MoveId == MoveJet ? Pressurize : Jet;
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) =>
        moveId == MovePressurize ? Pressurize : Jet;
}
