using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Codec;
using Shalujianta.Sim.Core;
using Xunit;

namespace Shalujianta.Sim.Tests;

/// <summary>
/// PR-004 merge blockers: BattleStart draws nothing; every PlayerTurnStart (including
/// the first) refills energy / draws 5 / clears block; first frame hand == 5; END_TURN
/// resolves the enemy turn inside the same Step; hand cap 10; MAX_ZERO_COST_HARD_CUT=32
/// triggers StallCut; unimplemented hooks log instead of silently passing.
/// </summary>
public sealed class CombatKernelTests
{
    // ---- turn machine ----

    [Fact]
    public void BattleStartDrawsNothing_PlayerTurnStartDrawsFive()
    {
        var h = CombatTestHarness.New(autoStart: false);
        h.World.RunBattleStart();
        Assert.Empty(h.World.Hand);
        Assert.Equal(5, h.World.DrawPile.Count);

        h.World.RunPlayerTurnStart();
        Assert.Equal(5, h.World.Hand.Count);
        Assert.Equal(0, h.World.DrawPile.Count);
    }

    [Fact]
    public void FirstFrameHandIsFiveEnergyThreeTurnOne()
    {
        var h = CombatTestHarness.New();
        var obs = h.World.Observe();
        Assert.Equal(5, obs.Hand.Count);
        Assert.Equal(3, obs.Player.Energy);
        Assert.Equal(1, obs.Turn);
        Assert.Equal(ScreenType.Combat, obs.Screen);
        Assert.Equal(CombatPhase.Player, obs.Phase);
    }

    [Fact]
    public void EndTurnRunsEnemyInSameStep()
    {
        var h = CombatTestHarness.New(monsters: [(TestCards.M1, 30, [new MonsterMove(0, IntentId.Attack, 5, 1)])]);
        var result = CombatTestHarness.EndTurn(h.World);
        Assert.Equal(StepStatus.Ok, result.Status);
        Assert.False(result.Done);
        Assert.Equal(75, h.World.PlayerHp); // enemy resolved inside this Step
        Assert.Equal(2, h.World.Turn);
        Assert.Equal(3, h.World.Energy);    // refilled for turn 2
        Assert.Equal(0, h.World.PlayerBlock);
        Assert.Equal(5, h.World.Hand.Count);
    }

    [Fact]
    public void MonsterBlockPersistsThroughPlayerTurnAndClearsOnItsTurn()
    {
        var h = CombatTestHarness.New(monsters: [(TestCards.M1, 30,
            [new MonsterMove(0, IntentId.Defend, 8, 1), new MonsterMove(1, IntentId.Attack, 5, 1)])]);

        // Turn 1: the opening intent is Defend but the monster has not acted yet -> no block.
        CombatTestHarness.PlayCard(h.World, TestCards.Strike);
        Assert.Equal(24, h.World.Monsters[0].Hp);
        Assert.Equal(0, h.World.Monsters[0].Block);

        CombatTestHarness.EndTurn(h.World); // enemy executes Defend: block 8
        Assert.Equal(8, h.World.Monsters[0].Block);

        // Turn 2: that block persists through the whole player turn.
        CombatTestHarness.PlayCard(h.World, TestCards.Strike);
        Assert.Equal(24, h.World.Monsters[0].Hp);
        Assert.Equal(2, h.World.Monsters[0].Block);

        CombatTestHarness.EndTurn(h.World); // enemy turn: block clears, then attacks 5
        Assert.Equal(0, h.World.Monsters[0].Block);
        Assert.Equal(75, h.World.PlayerHp);
    }

    [Fact]
    public void PlayerTurnStartRefillsEnergyAndClearsBlock()
    {
        var h = CombatTestHarness.New(deck: CombatTestHarness.Deck((TestCards.Defend, 5)));
        CombatTestHarness.Play(h.World, 0, new TargetRef(TargetKind.None));
        Assert.True(h.World.PlayerBlock > 0);
        CombatTestHarness.EndTurn(h.World); // block 5 absorbs the scripted attack
        Assert.Equal(0, h.World.PlayerBlock);
        Assert.Equal(3, h.World.Energy);
    }

    [Fact]
    public void HandCapTenSkipsDrawAndLogs()
    {
        var h = CombatTestHarness.New(
            deck: CombatTestHarness.Deck((TestCards.RetainBlock, 12)),
            monsters: [(TestCards.M1, 30, [new MonsterMove(0, IntentId.Defend, 0, 1)])]);

        // Fill hand to 10 with retained cards, leave 2 on the draw pile.
        h.World.Hand.AddRange(h.World.DrawPile.GetRange(0, 5));
        h.World.DrawPile.RemoveRange(0, 5);
        Assert.Equal(10, h.World.Hand.Count);
        Assert.Equal(2, h.World.DrawPile.Count);

        var result = CombatTestHarness.EndTurn(h.World);
        Assert.Equal(StepStatus.Ok, result.Status);
        Assert.Equal(10, h.World.Hand.Count);   // retain kept everything
        Assert.Equal(2, h.World.DrawPile.Count); // draw skipped, cards stay on the pile
        Assert.Contains(h.Logs, l => l.StartsWith("hand_overflow"));
    }

    [Fact]
    public void TurnLimitTriggersStallCut()
    {
        var h = CombatTestHarness.New(stallTurnLimit: 3);
        Assert.Equal(StepStatus.Ok, CombatTestHarness.EndTurn(h.World).Status);
        Assert.Equal(StepStatus.Ok, CombatTestHarness.EndTurn(h.World).Status);
        var third = CombatTestHarness.EndTurn(h.World);
        Assert.Equal(StepStatus.StallCut, third.Status);
        Assert.True(third.Done);
    }

    // ---- zero-cost loop ----

    [Fact]
    public void ZeroCostLoopWarnsAtTwelve()
    {
        var h = CombatTestHarness.New(
            deck: CombatTestHarness.Deck((TestCards.ZeroCostLoop, 5)),
            monsters: [(TestCards.M1, 30, [new MonsterMove(0, IntentId.Defend, 0, 1)])]);

        for (int i = 0; i < 11; i++)
        {
            Assert.Equal(StepStatus.Ok, CombatTestHarness.Play(h.World, 0, new TargetRef(TargetKind.None)).Status);
        }

        Assert.DoesNotContain(h.Logs, l => l.StartsWith("zero_cost_warn"));
        Assert.Equal(StepStatus.Ok, CombatTestHarness.Play(h.World, 0, new TargetRef(TargetKind.None)).Status);
        Assert.Equal(12, h.World.ZeroCostPlaysThisTurn);
        Assert.Contains(h.Logs, l => l.StartsWith("zero_cost_warn"));
    }

    [Fact]
    public void ZeroCostHardCutAtThirtyTwo()
    {
        var h = CombatTestHarness.New(
            deck: CombatTestHarness.Deck((TestCards.ZeroCostLoop, 5)),
            monsters: [(TestCards.M1, 30, [new MonsterMove(0, IntentId.Defend, 0, 1)])]);

        for (int i = 0; i < 31; i++)
        {
            Assert.Equal(StepStatus.Ok, CombatTestHarness.Play(h.World, 0, new TargetRef(TargetKind.None)).Status);
        }

        var cut = CombatTestHarness.Play(h.World, 0, new TargetRef(TargetKind.None));
        Assert.Equal(StepStatus.StallCut, cut.Status);
        Assert.True(cut.Done);
        Assert.Equal(32, h.World.ZeroCostPlaysThisTurn);
    }

    // ---- damage math ----

    [Fact]
    public void VulnerableAppliesFiftyPercentOnNextHit()
    {
        var h = CombatTestHarness.New(deck: CombatTestHarness.Deck((TestCards.VulnHit, 2), (TestCards.Strike, 3)));
        CombatTestHarness.PlayCard(h.World, TestCards.VulnHit); // 6 dmg (no vuln yet), then Vulnerable 2
        Assert.Equal(24, h.World.Monsters[0].Hp);
        CombatTestHarness.EndTurn(h.World); // just_applied cleared, stacks kept
        CombatTestHarness.Play(h.World, 0); // any 6-dmg card -> floor(6 * 1.5) = 9
        Assert.Equal(15, h.World.Monsters[0].Hp);
    }

    [Fact]
    public void WeakOnAttackerReducesDamage()
    {
        var h = CombatTestHarness.New();
        h.World.ApplyPlayerPower(PowerIds.Weak, 1);
        CombatTestHarness.Play(h.World, 0); // floor(6 * 0.75) = 4
        Assert.Equal(26, h.World.Monsters[0].Hp);
    }

    [Fact]
    public void StrengthAddsAndBlockMathUsesDexterityAndFrail()
    {
        var h = CombatTestHarness.New(deck: CombatTestHarness.Deck((TestCards.Strike, 2), (TestCards.Defend, 3)));
        h.World.ApplyPlayerPower(PowerIds.Strength, 2);
        CombatTestHarness.PlayCard(h.World, TestCards.Strike); // 6 + 2 = 8
        Assert.Equal(22, h.World.Monsters[0].Hp);

        h.World.ApplyPlayerPower(PowerIds.Frail, 2);
        CombatTestHarness.PlayCard(h.World, TestCards.Defend, new TargetRef(TargetKind.None)); // floor(5 * 0.75) = 3
        Assert.Equal(3, h.World.PlayerBlock);

        h.World.ApplyPlayerPower(PowerIds.Dexterity, 2);
        // Frail still active: floor((5 + 2) * 0.75) = 5
        CombatTestHarness.PlayCard(h.World, TestCards.Defend, new TargetRef(TargetKind.None));
        Assert.Equal(8, h.World.PlayerBlock);
    }

    [Fact]
    public void PowersDecayAtOwnerTurnEnd()
    {
        var h = CombatTestHarness.New();
        h.World.ApplyMonsterPower(0, PowerIds.Vulnerable, 2);
        h.World.ApplyPlayerPower(PowerIds.Strength, 3);
        CombatTestHarness.EndTurn(h.World); // monster turn end: just_applied cleared, no decay yet
        Assert.Equal(2, PowerOps.GetAmount(h.World.Monsters[0].Powers, PowerIds.Vulnerable));
        CombatTestHarness.EndTurn(h.World); // decay -> 1
        Assert.Equal(1, PowerOps.GetAmount(h.World.Monsters[0].Powers, PowerIds.Vulnerable));
        CombatTestHarness.EndTurn(h.World); // decay -> removed
        Assert.Equal(0, PowerOps.GetAmount(h.World.Monsters[0].Powers, PowerIds.Vulnerable));
        Assert.Equal(3, PowerOps.GetAmount(h.World.PlayerPowers, PowerIds.Strength)); // Strength persists
    }

    [Fact]
    public void MultiHitAttackAppliesPerHitWithBlock()
    {
        var h = CombatTestHarness.New(
            hp: 80,
            deck: CombatTestHarness.Deck((TestCards.Defend, 5)),
            monsters: [(TestCards.M1, 30, [new MonsterMove(0, IntentId.Attack, 3, 2)])]);
        CombatTestHarness.Play(h.World, 0, new TargetRef(TargetKind.None)); // block 5
        CombatTestHarness.EndTurn(h.World); // 3 then 3: block eats 5, hp takes 1
        Assert.Equal(79, h.World.PlayerHp);
    }

    // ---- zones ----

    [Fact]
    public void ExhaustAndEtherealGoToExhaustPile()
    {
        var h = CombatTestHarness.New(
            deck: CombatTestHarness.Deck(
                (TestCards.ExhaustHit, 1), (TestCards.EtherealBlock, 1), (TestCards.Strike, 10)),
            autoStart: false);
        h.World.RunBattleStart();
        CombatTestHarness.MoveFromDrawToHand(h.World, TestCards.ExhaustHit, 1);
        CombatTestHarness.MoveFromDrawToHand(h.World, TestCards.EtherealBlock, 1);
        h.World.RunPlayerTurnStart();

        CombatTestHarness.PlayCard(h.World, TestCards.ExhaustHit);
        Assert.Single(h.World.ExhaustPile);
        Assert.Empty(h.World.DiscardPile);
        CombatTestHarness.EndTurn(h.World); // ethereal exhausts from hand
        Assert.Equal(2, h.World.ExhaustPile.Count);
        Assert.Equal(5, h.World.DiscardPile.Count); // 5 plain strikes discarded...
        Assert.Equal(0, h.World.DrawPile.Count);    // ...then drawn again at turn start (draw pile was 5)
        Assert.Equal(5, h.World.Hand.Count);
    }

    [Fact]
    public void ReshuffleUsesShuffleStreamAndIsDeterministic()
    {
        List<CardInstance> DeckOf5() => CombatTestHarness.Deck(
            (TestCards.Strike, 1), (TestCards.Defend, 1), (TestCards.AllHit, 1),
            (TestCards.VulnHit, 1), (TestCards.ExhaustHit, 1));
        var a = CombatTestHarness.New(seed: 7, deck: DeckOf5());
        var b = CombatTestHarness.New(seed: 7, deck: DeckOf5());

        CombatTestHarness.EndTurn(a.World);
        CombatTestHarness.EndTurn(b.World);

        // Counter counts RNG samples: a 5-card Fisher-Yates draws 4 samples per shuffle.
        Assert.Equal(8, a.World.ShuffleCounter); // battle start + reshuffle
        Assert.Equal(8, b.World.ShuffleCounter);
        Assert.Equal(5, a.World.Hand.Count);
        Assert.Equal(
            a.World.Hand.Select(c => c.Uuid),
            b.World.Hand.Select(c => c.Uuid));
    }

    // ---- legality ----

    [Fact]
    public void IllegalActionsAreRejectedWithoutMutation()
    {
        var h = CombatTestHarness.New(deck: CombatTestHarness.Deck((TestCards.Strike, 5)));
        ulong before = h.World.Hash64();

        // wrong uuid
        var badUuid = new Action(Verb.PlayCard, 0, 999999, -1, new TargetRef(TargetKind.Monster, 0));
        Assert.Equal(StepStatus.Illegal, h.World.Step(badUuid).Status);

        // wrong target kind
        Assert.Equal(StepStatus.Illegal,
            CombatTestHarness.Play(h.World, 0, new TargetRef(TargetKind.None)).Status);

        // non-existent monster slot
        Assert.Equal(StepStatus.Illegal,
            CombatTestHarness.Play(h.World, 0, new TargetRef(TargetKind.Monster, 3)).Status);

        // unsupported verb in v0 combat
        Assert.Equal(StepStatus.Illegal, h.World.Step(new Action(Verb.UsePotion, -1, -1, 0)).Status);

        Assert.Equal(before, h.World.Hash64());
        Assert.Equal(5, h.World.Hand.Count);
        Assert.Equal(3, h.World.Energy);
    }

    [Fact]
    public void NotEnoughEnergyIsRejected()
    {
        var h = CombatTestHarness.New(deck: CombatTestHarness.Deck((TestCards.VulnHit, 5)));
        CombatTestHarness.Play(h.World, 0); // cost 2 -> energy 1
        Assert.Equal(1, h.World.Energy);
        Assert.Equal(StepStatus.Illegal, CombatTestHarness.Play(h.World, 0).Status);
        Assert.Equal(1, h.World.Energy);
    }

    [Fact]
    public void UnplayableAndNullApplyCardsAreRejectedWithLog()
    {
        var h = CombatTestHarness.New(deck: CombatTestHarness.Deck(
            (TestCards.NoApply, 1), (TestCards.Unplayable, 1), (TestCards.Strike, 3)));

        int noApplyIdx = h.World.Hand.FindIndex(c => c.CatalogId == TestCards.NoApply);
        var noApply = h.World.Step(new Action(
            Verb.PlayCard, noApplyIdx, h.World.Hand[noApplyIdx].Uuid, -1, new TargetRef(TargetKind.None)));
        Assert.Equal(StepStatus.Illegal, noApply.Status);
        Assert.Contains(h.Logs, l => l.Contains("not_implemented_hook") && l.Contains("Apply"));

        int unplayableIdx = h.World.Hand.FindIndex(c => c.CatalogId == TestCards.Unplayable);
        var unplayable = h.World.Step(new Action(
            Verb.PlayCard, unplayableIdx, h.World.Hand[unplayableIdx].Uuid, -1, new TargetRef(TargetKind.None)));
        Assert.Equal(StepStatus.Illegal, unplayable.Status);
        Assert.Equal(5, h.World.Hand.Count);
    }

    [Fact]
    public void LegalMaskMatchesSlotTableAndCombatValidation()
    {
        var h = CombatTestHarness.New(deck: CombatTestHarness.Deck((TestCards.Strike, 5)));
        var mask = h.World.LegalActions();

        Assert.True(mask.Flat[ActionFlatten.EndTurnSlot]);
        Assert.True(mask.Flat[ActionFlatten.PlayCardSlot(0, 1)]);   // monster slot 0 -> target slot 1
        Assert.False(mask.Flat[ActionFlatten.PlayCardSlot(0, 0)]);  // enemy card cannot take none
        Assert.False(mask.Flat[ActionFlatten.PlayCardSlot(0, 8)]);  // not an AOE card
        Assert.False(mask.Flat[ActionFlatten.UsePotionBase]);       // potions are PR-006
        Assert.False(mask.Flat[ActionFlatten.MapSelectBase]);       // meta stays zero in combat

        // drain energy: 3 of 5 strikes playable
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(StepStatus.Ok, CombatTestHarness.Play(h.World, 0).Status);
        }

        mask = h.World.LegalActions();
        Assert.False(mask.PlayableHand[0]);
        Assert.False(mask.Flat[ActionFlatten.PlayCardSlot(0, 1)]);
        Assert.True(mask.Flat[ActionFlatten.EndTurnSlot]);
    }

    // ---- death / terminal ----

    [Fact]
    public void KillingAllMonstersWins()
    {
        var h = CombatTestHarness.New(
            deck: CombatTestHarness.Deck((TestCards.AllHit, 5)),
            monsters: [(TestCards.M1, 12, [new MonsterMove(0, IntentId.Attack, 3, 1)])]);
        var aoe = new TargetRef(TargetKind.AllEnemies);
        CombatTestHarness.Play(h.World, 0, aoe);
        CombatTestHarness.Play(h.World, 0, aoe);
        var last = CombatTestHarness.Play(h.World, 0, aoe);
        Assert.Equal(StepStatus.TerminalWin, last.Status);
        Assert.True(last.Done);
        Assert.True(h.World.IsTerminal);
        Assert.Equal(ScreenType.Victory, h.World.Observe().Screen);
    }

    [Fact]
    public void LethalEnemyAttackEndsCombatAsLoss()
    {
        var h = CombatTestHarness.New(
            hp: 40,
            monsters: [(TestCards.M1, 30, [new MonsterMove(0, IntentId.Attack, 50, 1)])]);
        var result = CombatTestHarness.EndTurn(h.World);
        Assert.Equal(StepStatus.TerminalLoss, result.Status);
        Assert.True(result.Done);
        Assert.Equal(0, h.World.PlayerHp);
        Assert.Equal(ScreenType.GameOver, h.World.Observe().Screen);
    }

    [Fact]
    public void AoeOnlyDamagesAliveMonstersAndDeadTargetBecomesIllegal()
    {
        var h = CombatTestHarness.New(
            deck: CombatTestHarness.Deck((TestCards.AllHit, 3), (TestCards.Strike, 7)),
            monsters: [(TestCards.M1, 30, [new MonsterMove(0, IntentId.Attack, 3, 1)]),
                       (TestCards.M2, 12, [new MonsterMove(0, IntentId.Attack, 3, 1)])],
            autoStart: false);
        h.World.RunBattleStart();
        CombatTestHarness.MoveFromDrawToHand(h.World, TestCards.AllHit, 3);
        CombatTestHarness.MoveFromDrawToHand(h.World, TestCards.Strike, 1);
        h.World.RunPlayerTurnStart();

        var aoe = new TargetRef(TargetKind.AllEnemies);
        CombatTestHarness.PlayCard(h.World, TestCards.AllHit, aoe);
        CombatTestHarness.PlayCard(h.World, TestCards.AllHit, aoe);
        CombatTestHarness.PlayCard(h.World, TestCards.AllHit, aoe); // M2 (12 hp) dies, M1 (18) survives
        Assert.False(h.World.Monsters[1].Alive);
        Assert.True(h.World.Monsters[0].Alive);

        // strike (now hand[0]) targeting the dead slot 1 is illegal
        var strikeIndex = h.World.Hand.FindIndex(c => c.CatalogId == TestCards.Strike);
        Assert.True(strikeIndex >= 0);
        Assert.Equal(StepStatus.Illegal,
            CombatTestHarness.Play(h.World, strikeIndex, new TargetRef(TargetKind.Monster, 1)).Status);
    }

    // ---- SL machinery ----

    [Fact]
    public void CloneIsDeepAndSlOnly()
    {
        var h = CombatTestHarness.New();
        ulong before = h.World.Hash64();
        var clone = (CombatWorld)h.World.Clone();
        CombatTestHarness.Play(clone, 0);
        Assert.Equal(before, h.World.Hash64()); // original untouched
        Assert.NotEqual(h.World.Hash64(), clone.Hash64());
        Assert.NotEqual(h.World.InfoSetHash64(), clone.InfoSetHash64());

        var nosl = CombatTestHarness.New(mode: InfoMode.NoSl);
        Assert.Throws<InvalidOperationException>(() => nosl.World.Clone());
    }

    [Fact]
    public void CheckpointRestoresMidCombatState()
    {
        List<CardInstance> DeckOf5() => CombatTestHarness.Deck(
            (TestCards.Strike, 1), (TestCards.Defend, 1), (TestCards.AllHit, 1),
            (TestCards.VulnHit, 1), (TestCards.ExhaustHit, 1));
        var moves = new MonsterMove[] { new(0, IntentId.Attack, 5, 1) };

        var h = CombatTestHarness.New(
            seed: 42,
            deck: DeckOf5(),
            monsters: [(TestCards.M1, 30, moves)]);
        CombatTestHarness.Play(h.World, 0);
        var ck = h.World.Checkpoint();

        // Seed 999: the restored RNG state must fully override the constructor seed.
        var b = CombatTestHarness.New(
            seed: 999,
            monsters: [(TestCards.M1, 30, moves)],
            autoStart: false);
        b.World.Restore(ck);

        Assert.Equal(h.World.PlayerHp, b.World.PlayerHp);
        Assert.Equal(h.World.Energy, b.World.Energy);
        Assert.Equal(h.World.Hand.Count, b.World.Hand.Count);
        Assert.Equal(h.World.DiscardPile.Count, b.World.DiscardPile.Count);
        Assert.Equal(h.World.Monsters[0].Hp, b.World.Monsters[0].Hp);
        Assert.Equal(h.World.Hash64(), b.World.Hash64());

        var ra = CombatTestHarness.EndTurn(h.World);
        var rb = CombatTestHarness.EndTurn(b.World);
        Assert.Equal(ra.Status, rb.Status);
        Assert.Equal(h.World.PlayerHp, b.World.PlayerHp);
        Assert.Equal(h.World.Hash64(), b.World.Hash64()); // restored rng -> same reshuffle -> same hands
    }

    [Fact]
    public void InfoSetHashIgnoresDrawOrderButHash64DoesNot()
    {
        var h = CombatTestHarness.New(seed: 42, deck: CombatTestHarness.Deck(
            (TestCards.Strike, 2), (TestCards.Defend, 2), (TestCards.AllHit, 2), (TestCards.VulnHit, 2)));
        Assert.Equal(3, h.World.DrawPile.Count);

        ulong hashBefore = h.World.Hash64();
        ulong infoBefore = h.World.InfoSetHash64();
        (h.World.DrawPile[0], h.World.DrawPile[2]) = (h.World.DrawPile[2], h.World.DrawPile[0]);

        Assert.NotEqual(hashBefore, h.World.Hash64());
        Assert.Equal(infoBefore, h.World.InfoSetHash64());
    }

    [Fact]
    public void CheckpointJsonRoundTripKeepsCombatState()
    {
        var h = CombatTestHarness.New();
        CombatTestHarness.Play(h.World, 0);
        var ck = h.World.Checkpoint();
        string json = ck.ToJson();
        var back = SimCheckpoint.FromJson(json);

        Assert.NotNull(back.Combat);
        Assert.Equal(ck.Combat!.Hand.Count, back.Combat.Hand.Count);
        Assert.Equal(ck.Combat.Monsters[0].Hp, back.Combat.Monsters[0].Hp);
        Assert.Equal(ck.Hash64(), back.Hash64());
    }

    [Fact]
    public void PowerIdsMatchSyntheticCatalog()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
        var path = Path.GetFullPath(Path.Combine(root, "catalog", "generated", "entity_ids.json"));
        var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var map = new Dictionary<string, int>();
        foreach (var e in doc.RootElement.GetProperty("namespaces").GetProperty("POWER").GetProperty("entries").EnumerateArray())
        {
            map[e.GetProperty("key").GetString()!] = e.GetProperty("entity_id").GetInt32();
        }

        Assert.Equal(map["Artifact"], PowerIds.Artifact);
        Assert.Equal(map["Buffer"], PowerIds.Buffer);
        Assert.Equal(map["Dexterity"], PowerIds.Dexterity);
        Assert.Equal(map["Frail"], PowerIds.Frail);
        Assert.Equal(map["Poison"], PowerIds.Poison);
        Assert.Equal(map["Strength"], PowerIds.Strength);
        Assert.Equal(map["Vulnerable"], PowerIds.Vulnerable);
        Assert.Equal(map["Weak"], PowerIds.Weak);
    }
}
