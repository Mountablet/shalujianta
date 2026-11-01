using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Codec;
using Shalujianta.Sim.Core;
using Xunit;

namespace Shalujianta.Sim.Tests;

/// <summary>PR-008 C# side: CloneForSearch / INoSlSearchWorld semantics (KD-16).</summary>
public sealed class SearchWorldTests
{
    [Fact]
    public void CloneForSearchPreservesInfoSetAndPublicState()
    {
        var h = CombatTestHarness.New(
            deck: CombatTestHarness.Deck((TestCards.Strike, 3), (TestCards.Defend, 2), (TestCards.AllHit, 2), (TestCards.VulnHit, 3)));
        CombatTestHarness.Play(h.World, 0); // move a card to discard
        h.World.AddPotion(PotionIds.BlockPotion);
        h.World.ApplyPlayerPower(PowerIds.Strength, 2);

        var search = h.World.CloneForSearch(99);

        Assert.False(search is ISimWorld);
        Assert.Equal(h.World.InfoSetHash64(), search.InfoSetHash64());

        var real = h.World.Observe();
        var obs = search.Observe();
        Assert.Equal(real.Player.Hp, obs.Player.Hp);
        Assert.Equal(real.Player.Energy, obs.Player.Energy);
        Assert.Equal(real.Player.Block, obs.Player.Block);
        Assert.Equal(real.Hand.Count, obs.Hand.Count);
        Assert.Equal(real.DiscardCount, obs.DiscardCount);
        Assert.Equal(real.DrawBagCounts.Count, obs.DrawBagCounts.Count);
        Assert.Equal(real.Potions, obs.Potions);
        Assert.Equal(real.Monsters[0].Hp, obs.Monsters[0].Hp);
    }

    [Fact]
    public void SearchStepAndLegalActionsWork()
    {
        var h = CombatTestHarness.New();
        var search = h.World.CloneForSearch(7);

        var mask = search.LegalActions();
        Assert.True(mask.Flat[ActionFlatten.EndTurnSlot]);
        var result = search.Step(new Action(Verb.EndTurn));
        Assert.Equal(StepStatus.Ok, result.Status);
        Assert.Equal(2, search.Observe().Turn);
    }

    [Fact]
    public void ResampleHiddenYieldsDistinctRealizationsSameInfoSet()
    {
        var h = CombatTestHarness.New(
            deck: CombatTestHarness.Deck(
                (TestCards.Strike, 2), (TestCards.Defend, 2), (TestCards.AllHit, 2), (TestCards.VulnHit, 2), (TestCards.ExhaustHit, 2)));
        var baseHandle = h.World.CloneForSearch(1);
        var info = baseHandle.InfoSetHash64();

        var seen = new HashSet<string>();
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var handle = h.World.CloneForSearch(seed);
            Assert.Equal(info, handle.InfoSetHash64());

            // Each determinization realizes its own future: identical scripts diverge
            // once the resampled draw order reshuffles into the hand.
            var lines = new List<string>();
            var w = handle;
            for (int turn = 0; turn < 4 && !w.Observe().Screen.Equals(ScreenType.Victory); turn++)
            {
                lines.Add(string.Join(",", w.Observe().Hand.Select(c => c.CatalogId)));
                w.Step(new Action(Verb.EndTurn));
            }

            seen.Add(string.Join("|", lines));
        }

        Assert.True(seen.Count >= 2, "8 determinizations produced identical realizations; resampling is broken");
    }
}
