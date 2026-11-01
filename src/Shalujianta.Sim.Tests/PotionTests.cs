using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Codec;
using Shalujianta.Sim.Content;
using Shalujianta.Sim.Core;
using Shalujianta.Sim.Potions;
using Xunit;

namespace Shalujianta.Sim.Tests;

/// <summary>PR-006: potion slots, FirePotion (targeted) and BlockPotion (untargeted).</summary>
public sealed class PotionTests
{
    private static CombatWorld NewRatWorld(int seed = 5, params int[] startingPotions)
    {
        SliceV0.Register();
        var cfg = new SimConfig { CatalogVersion = "synthetic-v0", Mode = InfoMode.Sl, GameVersionString = "potion-tests" };
        var start = SliceV0.StarterCombat(SliceV0.EncounterKind.SingleRat);
        start.StartingPotions.AddRange(startingPotions);
        var world = new CombatWorld(cfg, start, seed);
        world.Start();
        return world;
    }

    [Fact]
    public void FirePotionDealsTwentyToTheTargetedEnemy()
    {
        var w = NewRatWorld();
        Assert.True(w.AddPotion(PotionIds.FirePotion));
        int hpBefore = w.Monsters[0].Hp;
        var result = w.Step(new Action(Verb.UsePotion, -1, -1, 0, new TargetRef(TargetKind.Monster, 0)));

        // A low hp roll (17-19) means the potion itself wins the fight.
        Assert.True(result.Status is StepStatus.Ok or StepStatus.TerminalWin, result.Status.ToString());
        Assert.Equal(Math.Max(0, hpBefore - 20), w.Monsters[0].Hp);
        Assert.Equal(0, w.PotionSlots[0]);
        Assert.Equal(3, w.Energy); // potions cost no energy
    }

    [Fact]
    public void BlockPotionGrantsTwelveBlock()
    {
        var w = NewRatWorld();
        Assert.True(w.AddPotion(PotionIds.BlockPotion));
        var result = w.Step(new Action(Verb.UsePotion, -1, -1, 0, new TargetRef(TargetKind.None)));

        Assert.Equal(StepStatus.Ok, result.Status);
        Assert.Equal(12, w.PlayerBlock);
        Assert.Equal(0, w.PotionSlots[0]);
    }

    [Fact]
    public void DiscardClearsSlotAndEmptySlotsAreIllegal()
    {
        var w = NewRatWorld();
        Assert.True(w.AddPotion(PotionIds.BlockPotion));

        Assert.Equal(StepStatus.Illegal, w.Step(new Action(Verb.UsePotion, -1, -1, 1, new TargetRef(TargetKind.None))).Status);
        Assert.Equal(StepStatus.Illegal, w.Step(new Action(Verb.DiscardPotion, -1, -1, 2)).Status);

        Assert.Equal(StepStatus.Ok, w.Step(new Action(Verb.DiscardPotion, -1, -1, 0)).Status);
        Assert.Equal(0, w.PotionSlots[0]);
        Assert.Equal(StepStatus.Illegal, w.Step(new Action(Verb.DiscardPotion, -1, -1, 0)).Status);
    }

    [Fact]
    public void PotionMaskDistinguishesTargetedAndSelfPotions()
    {
        var w = NewRatWorld();
        w.AddPotion(PotionIds.FirePotion);
        w.AddPotion(PotionIds.BlockPotion);

        var mask = w.LegalActions();
        Assert.True(mask.PotionUsable[0]);
        Assert.True(mask.PotionUsable[1]);
        Assert.True(mask.PotionTargets[0][1]);  // fire -> living monster slot 0
        Assert.False(mask.PotionTargets[0][0]);
        Assert.True(mask.PotionTargets[1][0]);  // block -> no target
        Assert.False(mask.PotionTargets[1][1]);
        Assert.False(mask.PotionTargets[0][8]); // companion slot permanently masked (PR-006 gate)
        Assert.False(mask.PotionTargets[1][8]);
        Assert.True(mask.PotionDiscardable[0]);
        Assert.True(mask.PotionDiscardable[1]);
        Assert.True(mask.Flat[ActionFlatten.UsePotionSlot(0, 1)]);
        Assert.True(mask.Flat[ActionFlatten.DiscardPotionBase + 1]);
    }

    [Fact]
    public void PotionsSurviveCheckpointRoundTrip()
    {
        var w = NewRatWorld();
        w.AddPotion(PotionIds.FirePotion);
        w.AddPotion(PotionIds.BlockPotion);
        var ck = w.Checkpoint();

        var cfg = new SimConfig { CatalogVersion = "synthetic-v0", Mode = InfoMode.Sl, GameVersionString = "potion-tests" };
        var restored = new CombatWorld(cfg, SliceV0.StarterCombat(SliceV0.EncounterKind.SingleRat), runSeedI32: 999);
        restored.Restore(ck);

        Assert.Equal(w.PotionSlots, restored.PotionSlots);
        Assert.Equal(StepStatus.Ok,
            restored.Step(new Action(Verb.UsePotion, -1, -1, 1, new TargetRef(TargetKind.None))).Status);
        Assert.Equal(12, restored.PlayerBlock);
    }

    [Fact]
    public void AddPotionRefusesWhenFull()
    {
        var w = NewRatWorld();
        Assert.True(w.AddPotion(PotionIds.FirePotion));
        Assert.True(w.AddPotion(PotionIds.BlockPotion));
        Assert.True(w.AddPotion(PotionIds.BlockPotion));
        Assert.False(w.AddPotion(PotionIds.FirePotion)); // 3 slots, all full
    }
}
