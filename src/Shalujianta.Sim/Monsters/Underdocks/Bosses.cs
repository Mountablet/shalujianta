using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Monsters.Underdocks;

/// <summary>
/// Waterfall Giant (boss). Wiki: HP 240; turn 1 Pressurize (+15 Steam Eruption), then
/// the cycle Stomp 15 + 1 Weak / Ram 10 / Siphon (heal 10) / Pressure Gun 20 /
/// Pressure Up 13 — nearly every move adds 3 Steam Eruption. On death the stored
/// Steam Eruption explodes onto the player (the invulnerable About-To-Blow turn is
/// not modeled in v0 — documented simplification).
/// </summary>
public sealed class WaterfallGiantAi : IMonsterAi
{
    public const int HpMin = 240;
    public const int HpMax = 240;

    public const int MovePressurize = 0;
    public const int MoveStomp = 1;
    public const int MoveRam = 2;
    public const int MoveSiphon = 3;
    public const int MovePressureGun = 4;
    public const int MovePressureUp = 5;

    public static readonly MonsterMove Pressurize = new(
        MovePressurize, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.SteamEruption, 15));
    public static readonly MonsterMove Stomp = new(
        MoveStomp, IntentId.Attack, 15, 1, (w, s) =>
        {
            w.ApplyPlayerPower(PowerIds.Weak, 1);
            w.ApplyMonsterPower(s, PowerIds.SteamEruption, 3);
        });
    public static readonly MonsterMove Ram = new(
        MoveRam, IntentId.Attack, 10, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.SteamEruption, 3));
    public static readonly MonsterMove Siphon = new(
        MoveSiphon, IntentId.Buff, 0, 1, (w, s) =>
        {
            w.HealMonster(s, 10);
            w.ApplyMonsterPower(s, PowerIds.SteamEruption, 3);
        });
    public static readonly MonsterMove PressureGun = new(
        MovePressureGun, IntentId.Attack, 20, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.SteamEruption, 3));
    public static readonly MonsterMove PressureUp = new(
        MovePressureUp, IntentId.Attack, 13, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.SteamEruption, 3));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return Pressurize;
        }

        // Cycle over Stomp, Ram, Siphon, Pressure Gun, Pressure Up.
        return MoveFor(world, slot, MoveStomp + ((m.Misc - 1) % 5));
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveStomp => Stomp,
        MoveSiphon => Siphon,
        MovePressureGun => PressureGun,
        MovePressureUp => PressureUp,
        MoveRam => Ram,
        _ => Pressurize,
    };
}

/// <summary>
/// Soul Fysh (boss). Wiki: HP 211; fixed rotation Beckon (2 Beckon statuses: one into
/// the draw pile, one into the discard) -> De-Gas 16 -> Gaze 7 + 1 Beckon -> Fade
/// (+2 Intangible) -> Scream 13 + 3 Vulnerable.
/// </summary>
public sealed class SoulFyshAi : IMonsterAi
{
    public const int HpMin = 211;
    public const int HpMax = 211;

    public const int MoveBeckon = 0;
    public const int MoveDeGas = 1;
    public const int MoveGaze = 2;
    public const int MoveFade = 3;
    public const int MoveScream = 4;
    private const int MoveCount = 5;

    public static readonly MonsterMove Beckon = new(
        MoveBeckon, IntentId.Debuff, 0, 1, (w, s) =>
        {
            w.AddCardsToDiscard(CardIds.Beckon, 1);
            w.AddCardsToDrawPile(CardIds.Beckon, 1);
        });
    public static readonly MonsterMove DeGas = new(MoveDeGas, IntentId.Attack, 16, 1);
    public static readonly MonsterMove Gaze = new(
        MoveGaze, IntentId.Attack, 7, 1, (w, s) => w.AddCardsToDiscard(CardIds.Beckon, 1));
    public static readonly MonsterMove Fade = new(
        MoveFade, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Intangible, 2));
    public static readonly MonsterMove Scream = new(
        MoveScream, IntentId.Attack, 13, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Vulnerable, 3));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        int prev = m.Misc == 0 ? MoveScream : m.MoveId;
        return MoveFor(world, slot, (prev + 1) % MoveCount);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveDeGas => DeGas,
        MoveGaze => Gaze,
        MoveFade => Fade,
        MoveScream => Scream,
        _ => Beckon,
    };
}

/// <summary>
/// Lagavulin Matriarch (boss). Wiki: HP 222, starts Asleep 3 + Plating 12; wakes on
/// damage or after 3 turns and loses all Plating; then the fixed cycle Slash 19 ->
/// Disembowel 9x2 -> Slash2 12 + 12 block -> Soul Siphon (player -2 Str / -2 Dex,
/// self +2 Str).
/// </summary>
public sealed class LagavulinMatriarchAi : IMonsterAi
{
    public const int HpMin = 222;
    public const int HpMax = 222;

    public const int MoveSleep = 0;
    public const int MoveSlash = 1;
    public const int MoveDisembowel = 2;
    public const int MoveSlash2 = 3;
    public const int MoveSoulSiphon = 4;

    public static readonly MonsterMove Sleep = new(MoveSleep, IntentId.Defend, 0, 1);
    public static readonly MonsterMove Slash = new(MoveSlash, IntentId.Attack, 19, 1);
    public static readonly MonsterMove Disembowel = new(MoveDisembowel, IntentId.Attack, 9, 2);
    public static readonly MonsterMove Slash2 = new(
        MoveSlash2, IntentId.Attack, 12, 1, (w, s) => w.GainMonsterBlock(s, 12));
    public static readonly MonsterMove SoulSiphon = new(
        MoveSoulSiphon, IntentId.Debuff, 0, 1, (w, s) =>
        {
            w.ApplyPlayerPower(PowerIds.Strength, -2);
            w.ApplyPlayerPower(PowerIds.Dexterity, -2);
            w.ApplyMonsterPower(s, PowerIds.Strength, 2);
        });

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (PowerOps.GetAmount(m.Powers, PowerIds.Asleep) > 0)
        {
            return Sleep;
        }

        // Post-wake cycle: Slash -> Disembowel -> Slash2 -> Soul Siphon. The first
        // awake roll starts the cycle at Slash.
        int prev = m.MoveId == MoveSleep ? MoveSoulSiphon : m.MoveId;
        int next = prev >= MoveSoulSiphon ? MoveSlash : prev + 1;
        return MoveFor(world, slot, next);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveDisembowel => Disembowel,
        MoveSlash2 => Slash2,
        MoveSoulSiphon => SoulSiphon,
        MoveSleep => Sleep,
        _ => Slash,
    };
}
