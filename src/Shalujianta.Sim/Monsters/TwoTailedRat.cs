using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Monsters;

/// <summary>
/// Two-Tailed Rat (Act 1 hallway). Wiki values (STS2:Two-Tailed_Rat, 2026-10-31):
/// HP 17-21; Scratch 8 dmg; Disease Bite 6 dmg; Screech applies 1 Frail; the same
/// move is never chosen twice in a row; Call for Backup (summon) exists in the live
/// game but is excluded from the v0 AI (design 4.1.1). Ascension 8+ numbers are not
/// implemented (v0 is A0). Pending dll verification in PR-014.
/// </summary>
public sealed class TwoTailedRatAi : IMonsterAi
{
    public const int HpMin = 17;
    public const int HpMax = 21;

    public const int MoveScratch = 0;
    public const int MoveDiseaseBite = 1;
    public const int MoveScreech = 2;
    private const int MoveCount = 3;

    public static readonly MonsterMove Scratch = new(MoveScratch, IntentId.Attack, 8, 1);
    public static readonly MonsterMove DiseaseBite = new(MoveDiseaseBite, IntentId.Attack, 6, 1);
    public static readonly MonsterMove Screech = new(
        MoveScreech, IntentId.Debuff, 0, 1, (world, slot) => world.ApplyPlayerPower(PowerIds.Frail, 1));

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        var m = world.Monsters[slot];
        if (m.Misc == 0)
        {
            // Opening intent: one uniform sample on the MonsterAi stream.
            return MoveFor(world, slot, world.Rng.NextInt(RngStream.MonsterAi, MoveCount));
        }

        // Never repeat the move that was just executed: uniform over the other two.
        int prev = m.MoveId;
        int roll = world.Rng.NextInt(RngStream.MonsterAi, MoveCount - 1);
        int next = roll == 0 ? (prev + 1) % MoveCount : (prev + 2) % MoveCount;
        return MoveFor(world, slot, next);
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) => moveId switch
    {
        MoveScratch => Scratch,
        MoveDiseaseBite => DiseaseBite,
        MoveScreech => Screech,
        _ => Screech,
    };
}

/// <summary>Encounter spawn tables for the SliceV0 hallway pool.</summary>
public static class Encounters
{
    /// <summary>C0 kernel encounter: a single rat (not an evaluation suite entry).</summary>
    public static List<MonsterSpawn> SingleTwoTailedRat() => [Rat()];

    /// <summary>
    /// SliceV0 evaluation encounter (owner-frozen at two rats; the live Underdocks
    /// pool fields three — see catalog/patches/slice_v0_moves.json).
    /// </summary>
    public static List<MonsterSpawn> Act1TwoTailedRats() => [Rat(), Rat()];

    private static MonsterSpawn Rat() => new()
    {
        CatalogId = MonsterIds.TwoTailedRat,
        HpMin = TwoTailedRatAi.HpMin,
        HpMax = TwoTailedRatAi.HpMax,
        AiFactory = () => new TwoTailedRatAi(),
    };
}
