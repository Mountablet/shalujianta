using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Codec;
using Shalujianta.Sim.Core;
using Shalujianta.Sim.Potions;
using Xunit;

namespace Shalujianta.Sim.Tests;

/// <summary>
/// PR-007 gates: the 256-slot table matches §4.9 exactly, X-cost and HAND_SELECT are
/// permanently masked in v0, and LegalActions() agrees with Step() on every slot.
/// </summary>
public sealed class ActionMaskTests
{
    [Fact]
    public void MetaRegionAndBannedVerbsStayZeroInCombat()
    {
        var h = CombatTestHarness.New();
        var mask = h.World.LegalActions();

        for (int slot = ActionFlatten.CombatFlatEnd; slot < ActionFlatten.Size; slot++)
        {
            Assert.False(mask.Flat[slot]);
        }

        foreach (var verb in new[]
                 {
                     Verb.MapSelect, Verb.RestChoose, Verb.ShopBuy, Verb.ShopRemove, Verb.ShopLeave,
                     Verb.EventOption, Verb.CardRewardPick, Verb.CardRewardSkip, Verb.BossRelicPick,
                     Verb.NeowPick, Verb.HandSelectToggle, Verb.HandSelectConfirm, Verb.Resign,
                     Verb.Choose, Verb.Proceed, Verb.Skip,
                 })
        {
            Assert.False(mask.VerbsAllowed[(int)verb]);
        }
    }

    [Fact]
    public void XCostCardIsFullyMaskedAndRejected()
    {
        TestCards.RegisterAll();
        if (!CardRegistry.TryGet(TestCards.XCostBlock, out _))
        {
            CardRegistry.Register(new CardDef(
                TestCards.XCostBlock, CardType.Skill, -1, CardTarget.None, CardFlags.XCost,
                (w, c, t) => w.GainPlayerBlock(1)));
        }

        var h = CombatTestHarness.New(deck: CombatTestHarness.Deck((TestCards.XCostBlock, 5)));
        var mask = h.World.LegalActions();

        for (int t = 0; t < ActionFlatten.MaxTargets; t++)
        {
            Assert.False(mask.Flat[ActionFlatten.PlayCardSlot(0, t)]);
        }

        Assert.False(mask.PlayableHand[0]);
        var result = h.World.Step(new Action(
            Verb.PlayCard, 0, h.World.Hand[0].Uuid, -1, new TargetRef(TargetKind.None)));
        Assert.Equal(StepStatus.Illegal, result.Status);
        Assert.Contains(h.Logs, l => l.StartsWith("x_cost_disabled_v0"));
    }

    [Fact]
    public void FlatMaskAgreesWithStepValidationOnEverySlot()
    {
        var h = CombatTestHarness.New(
            deck: CombatTestHarness.Deck((TestCards.Strike, 2), (TestCards.Defend, 2), (TestCards.ZeroCostLoop, 1)),
            monsters: [(TestCards.M1, 30, [new MonsterMove(0, IntentId.Attack, 5, 1)]),
                       (TestCards.M2, 30, [new MonsterMove(0, IntentId.Attack, 5, 1)])]);
        h.World.AddPotion(PotionIds.FirePotion);
        h.World.AddPotion(PotionIds.BlockPotion);

        var mask = h.World.LegalActions();
        Assert.True(mask.Flat.Cast<bool>().Count(b => b) > 0, "mask must have some legal action");

        for (int slot = 0; slot < ActionFlatten.Size; slot++)
        {
            var action = ActionFlatten.DecodeSlot(slot);
            if (action.Verb is Verb.Noop or Verb.Wait or Verb.State)
            {
                continue; // reserved / passthrough verbs: outside the mask contract
            }

            var clone = (CombatWorld)h.World.Clone();
            bool accepted = clone.Step(action).Status == StepStatus.Ok;
            Assert.Equal(mask.Flat[slot], accepted);
        }
    }
}
