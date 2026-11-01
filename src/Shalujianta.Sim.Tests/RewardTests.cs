using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Content;
using Shalujianta.Sim.Codec;
using Shalujianta.Sim.Core;
using Xunit;

namespace Shalujianta.Sim.Tests;

/// <summary>RewardSpec combat_v0.1 exact-value checks (design 5.3).</summary>
public sealed class RewardTests
{
    private static CombatWorld World(int seed, int hp = 80, List<CardInstance>? deck = null, MonsterMove[]? moves = null)
    {
        TestCards.RegisterAll();
        var cfg = new SimConfig { CatalogVersion = "synthetic-v0", Mode = InfoMode.Sl, GameVersionString = "reward-tests" };
        var start = new CombatStart
        {
            PlayerMaxHp = 80,
            PlayerHp = hp,
            MaxEnergy = 3,
            EncounterId = 9002,
            Deck = deck ?? new DeckBuilder().Add(TestCards.Strike, 5).Build(),
            Monsters =
            [
                new MonsterSpawn
                {
                    CatalogId = TestCards.M1,
                    Hp = 12,
                    AiFactory = () => new ScriptedMonsterAi(moves ?? [new MonsterMove(0, IntentId.Attack, 5, 1)]),
                },
            ],
        };
        var world = new CombatWorld(cfg, start, seed);
        world.Start();
        return world;
    }

    [Fact]
    public void WinInOneTurnYieldsExactTerminalReward()
    {
        // 3x AllHit (12 dmg vs 12 hp), no damage taken, no potions: 1.0 + 0.35*1.0.
        var w = World(3, deck: new DeckBuilder().Add(TestCards.AllHit, 5).Build(),
            moves: [new MonsterMove(0, IntentId.Defend, 0, 1)]);
        var aoe = new TargetRef(TargetKind.AllEnemies);
        var r1 = CombatTestHarness.Play(w, 0, aoe);
        var r2 = CombatTestHarness.Play(w, 0, aoe);
        var r3 = CombatTestHarness.Play(w, 0, aoe);

        Assert.Equal(StepStatus.TerminalWin, r3.Status);
        Assert.Equal(0f, r1.Reward, 4);
        Assert.Equal(0f, r2.Reward, 4);
        Assert.Equal(RewardV0.CombatWin + RewardV0.HpFrac, r3.Reward, 4);
    }

    [Fact]
    public void LossInOneEndTurnYieldsExactNegative()
    {
        // hp 40 vs one 50-damage attack: turn -0.004, hp step 0.15*(0-40)/80 = -0.075, lose -1.0.
        var w = World(5, hp: 40, moves: [new MonsterMove(0, IntentId.Attack, 50, 1)]);
        var r = CombatTestHarness.EndTurn(w);
        Assert.Equal(StepStatus.TerminalLoss, r.Status);
        Assert.Equal(RewardV0.Turn - 1.0f + RewardV0.HpStep * (-40f / 80f), r.Reward, 4);
    }

    [Fact]
    public void PotionUseCostsItsPenaltyOnly()
    {
        TestCards.RegisterAll();
        var cfg = new SimConfig { CatalogVersion = "synthetic-v0", Mode = InfoMode.Sl, GameVersionString = "reward-tests" };
        var start = SliceV0.StarterCombat(SliceV0.EncounterKind.SingleRat);
        start.StartingPotions.AddRange(start.StartingPotions);
        var w = new CombatWorld(cfg, start, 5);
        w.Start();
        w.AddPotion(PotionIds.BlockPotion);
        var r = w.Step(new Action(Verb.UsePotion, -1, -1, 0, new TargetRef(TargetKind.None)));
        Assert.Equal(RewardV0.PotionUse, r.Reward, 4);
    }

    [Fact]
    public void OverblockAndTurnPenaltyCompose()
    {
        TestCards.RegisterAll(); // deck argument evaluates before World() registers
        // Block 15 vs incoming 8: overblock (15-8-6)=1 point -> -0.002; turn -0.004.
        var w = World(7, deck: new DeckBuilder().Add(TestCards.Defend, 5).Build());
        CombatTestHarness.Play(w, 0, new TargetRef(TargetKind.None));
        CombatTestHarness.Play(w, 0, new TargetRef(TargetKind.None));
        CombatTestHarness.Play(w, 0, new TargetRef(TargetKind.None));
        Assert.Equal(15, w.PlayerBlock);
        var r = CombatTestHarness.EndTurn(w);
        Assert.Equal(RewardV0.Turn + RewardV0.Overblock * (15 - 5 - 6), r.Reward, 4); // default scripted attack is 5
    }

    [Fact]
    public void ZeroLoopPenaltyAppliesOnceAtWarn()
    {
        var w = World(9, deck: new DeckBuilder().Add(TestCards.ZeroCostLoop, 5).Build(),
            moves: [new MonsterMove(0, IntentId.Defend, 0, 1)]);
        for (int i = 0; i < 11; i++)
        {
            Assert.Equal(0f, CombatTestHarness.Play(w, 0, new TargetRef(TargetKind.None)).Reward, 4);
        }

        Assert.Equal(RewardV0.ZeroLoop, CombatTestHarness.Play(w, 0, new TargetRef(TargetKind.None)).Reward, 4);
        Assert.Equal(0f, CombatTestHarness.Play(w, 0, new TargetRef(TargetKind.None)).Reward, 4);
    }
}
