using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Monsters.Overgrowth;

/// <summary>
/// Nibbit. Wiki (Slay_the_Spire_2:Nibbit, 2026-11-01): HP 42-46; strict rotation
/// Butt(12) -> Hesitant Slice(6+5blk) -> Hiss(+2 Str); solo opens Butt, pair front
/// opens Hesitant Slice and back opens Hiss. A0 values, dll check in PR-014.
/// </summary>
public sealed class NibbitAi : IMonsterAi
{
    public const int HpMin = 42;
    public const int HpMax = 46;

    public const int MoveButt = 0;
    public const int MoveHesitantSlice = 1;
    public const int MoveHiss = 2;
    private const int MoveCount = 3;

    public static readonly MonsterMove Butt = new(MoveButt, IntentId.Attack, 12, 1);
    public static readonly MonsterMove HesitantSlice = new(
        MoveHesitantSlice, IntentId.Attack, 6, 1, (w, s) => w.GainMonsterBlock(s, 5));
    public static readonly MonsterMove Hiss = new(
        MoveHiss, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 2));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            int opening = world.Monsters.Count == 1
                ? MoveButt
                : (slot == 0 ? MoveHesitantSlice : MoveHiss);
            return MoveFor(world, slot, opening);
        }

        // Continue the strict rotation from the move just executed.
        return MoveFor(world, slot, (m.MoveId + 1) % MoveCount);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveButt => Butt,
        MoveHesitantSlice => HesitantSlice,
        MoveHiss => Hiss,
        _ => Butt,
    };
}

/// <summary>
/// Leaf/Twig Slimes. Wiki (Slay_the_Spire_2:Slimes, 2026-11-01):
/// Leaf S 11-15: Tackle 3 / Goop (1 Slimed -> discard), random no-repeat.
/// Leaf M 32-35: opens Sticky (2 Slimed), then alternates Clump Shot 8 / Sticky.
/// Twig S 7-11: Tackle 4 every turn.
/// Twig M 26-28: opens Sticky (1 Slimed), then 67% Chomp 11 / 33% Sticky, no Sticky twice.
/// </summary>
public sealed class LeafSlimeAi : IMonsterAi
{
    public const int HpMinM = 32;
    public const int HpMaxM = 35;
    public const int HpMinS = 11;
    public const int HpMaxS = 15;

    public const int MoveTackle = 0;
    public const int MoveGoop = 1;
    public const int MoveClumpShot = 2;
    public const int MoveStickyShot = 3;

    private readonly bool _medium;

    public LeafSlimeAi(bool medium) => _medium = medium;

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (_medium)
        {
            if (m.Misc == 0)
            {
                return MoveFor(world, slot, MoveStickyShot); // always opens Sticky Shot
            }

            // Alternates Clump Shot / Sticky Shot afterwards.
            return MoveFor(world, slot, m.MoveId == MoveClumpShot ? MoveStickyShot : MoveClumpShot);
        }

        if (m.Misc == 0)
        {
            return MoveFor(world, slot, world.Rng.NextInt(RngStream.MonsterAi, 2));
        }

        // Random between two moves with no-repeat is strict alternation.
        return MoveFor(world, slot, m.MoveId == MoveTackle ? MoveGoop : MoveTackle);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveTackle => new MonsterMove(MoveTackle, IntentId.Attack, _medium ? 0 : 3, 1),
        MoveGoop => new MonsterMove(MoveGoop, IntentId.Debuff, 0, 1,
            (w, s) => w.AddCardsToDiscard(CardIds.Slimed, 1)),
        MoveClumpShot => new MonsterMove(MoveClumpShot, IntentId.Attack, 8, 1),
        MoveStickyShot => new MonsterMove(MoveStickyShot, IntentId.Debuff, 0, 1,
            (w, s) => w.AddCardsToDiscard(CardIds.Slimed, 2)),
        _ => new MonsterMove(MoveTackle, IntentId.Attack, 3, 1),
    };
}

public sealed class TwigSlimeAi : IMonsterAi
{
    public const int HpMinM = 26;
    public const int HpMaxM = 28;
    public const int HpMinS = 7;
    public const int HpMaxS = 11;

    public const int MoveTackle = 0;
    public const int MoveChomp = 1;
    public const int MoveStickyShot = 2;

    private readonly bool _medium;

    public TwigSlimeAi(bool medium) => _medium = medium;

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (!_medium)
        {
            return MoveFor(world, slot, MoveTackle); // Twig S: Tackle every turn
        }

        if (m.Misc == 0)
        {
            return MoveFor(world, slot, MoveStickyShot); // always opens Sticky Shot
        }

        if (m.MoveId == MoveStickyShot)
        {
            return MoveFor(world, slot, MoveChomp); // Sticky never twice in a row
        }

        // 67% Chomp / 33% Sticky.
        int roll = world.Rng.NextInt(RngStream.MonsterAi, 3);
        return MoveFor(world, slot, roll == 2 ? MoveStickyShot : MoveChomp);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveChomp => new MonsterMove(MoveChomp, IntentId.Attack, 11, 1),
        MoveStickyShot => new MonsterMove(MoveStickyShot, IntentId.Debuff, 0, 1,
            (w, s) => w.AddCardsToDiscard(CardIds.Slimed, 1)),
        _ => new MonsterMove(MoveTackle, IntentId.Attack, 4, 1),
    };
}

/// <summary>
/// Shrinker Beetle. Wiki: HP 38-40; always opens Shrinker (Shrink: the debuffed
/// unit's attacks deal 30% less, removed when the applier dies), then alternates
/// Chomp 7 / Stomp 13.
/// </summary>
public sealed class ShrinkerBeetleAi : IMonsterAi
{
    public const int HpMin = 38;
    public const int HpMax = 40;

    public const int MoveShrinker = 0;
    public const int MoveChomp = 1;
    public const int MoveStomp = 2;

    public static readonly MonsterMove Shrinker = new(
        MoveShrinker, IntentId.Debuff, 0, 1, (w, s) => w.ApplyPlayerPower(PowerIds.Shrink, 1));
    public static readonly MonsterMove Chomp = new(MoveChomp, IntentId.Attack, 7, 1);
    public static readonly MonsterMove Stomp = new(MoveStomp, IntentId.Attack, 13, 1);

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            return Shrinker;
        }

        return m.MoveId == MoveChomp ? Stomp : Chomp;
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveChomp => Chomp,
        MoveStomp => Stomp,
        _ => Shrinker,
    };
}

/// <summary>
/// Fuzzy Wurm Crawler. Wiki: HP 55-57; fixed cycle Acid Goop 4 -> Inhale +7 Str ->
/// Acid Goop, i.e. strict alternation.
/// </summary>
public sealed class FuzzyWurmCrawlerAi : IMonsterAi
{
    public const int HpMin = 55;
    public const int HpMax = 57;

    public const int MoveAcidGoop = 0;
    public const int MoveInhale = 1;

    public static readonly MonsterMove AcidGoop = new(MoveAcidGoop, IntentId.Attack, 4, 1);
    public static readonly MonsterMove Inhale = new(
        MoveInhale, IntentId.Buff, 0, 1, (w, s) => w.ApplyMonsterPower(s, PowerIds.Strength, 7));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        return m.Misc == 0 || m.MoveId == MoveInhale
            ? AcidGoop
            : Inhale;
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) =>
        moveId == MoveInhale ? Inhale : AcidGoop;
}
