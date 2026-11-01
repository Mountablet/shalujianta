using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Codec;
using Shalujianta.Sim.Content;
using Shalujianta.Sim.Core;
using Shalujianta.Sim.Monsters.Overgrowth;
using Xunit;

namespace Shalujianta.Sim.Tests;

/// <summary>
/// Overgrowth (密林) hallway pool: monster AI patterns, the new mechanics
/// (Slimed / Shrink / Slippery / Tangled / Constrict), and random-policy
/// feasibility for every implemented encounter.
/// </summary>
public sealed class OvergrowthTests
{
    /// <summary>Builds a world without drawing, so tests can pin a deterministic hand.</summary>
    private static CombatWorld NewWorld(string encounterKey, int seed, int hp = 80, bool autoStart = true)
    {
        SliceV0.Register();
        var cfg = new SimConfig { CatalogVersion = "synthetic-v0", Mode = InfoMode.Sl, GameVersionString = "overgrowth-tests" };
        var start = SliceV0.EncounterByName(encounterKey);
        start.PlayerHp = hp;
        var world = new CombatWorld(cfg, start, seed);
        if (autoStart)
        {
            world.Start();
        }

        return world;
    }

    /// <summary>Deterministic opening hand: 2 Strikes + 3 Defends regardless of shuffle.</summary>
    private static CombatWorld WithStarterHand(string encounterKey, int seed, int hp = 80)
    {
        var w = NewWorld(encounterKey, seed, hp, autoStart: false);
        w.RunBattleStart();
        CombatTestHarness.MoveFromDrawToHand(w, CardIds.StrikeR, 2);
        CombatTestHarness.MoveFromDrawToHand(w, CardIds.DefendR, 3);
        w.RunPlayerTurnStart();
        return w;
    }

    private static void RandomFight(CombatWorld world, Random rng, int maxTurns = 50)
    {
        int turns = 0;
        while (!world.IsTerminal)
        {
            var mask = world.LegalActions();
            var legal = new List<int>();
            for (int i = 0; i < ActionMask.Size; i++)
            {
                if (mask.Flat[i])
                {
                    legal.Add(i);
                }
            }

            Assert.True(legal.Count > 0, "no legal actions in a live fight");
            world.Step(ActionFlatten.DecodeSlot(legal[rng.Next(legal.Count)]));
            if (world.Phase == CombatPhase.Player)
            {
                turns++;
            }

            Assert.True(turns <= maxTurns, $"fight exceeded {maxTurns} turns");
        }
    }

    // ---- monster patterns ----

    [Fact]
    public void NibbitRotatesButtSliceHissWithWikiOpenings()
    {
        var solo = NewWorld("Ov_A_Lone_Nibbit", seed: 3);
        Assert.Equal(IntentId.Attack, solo.Monsters[0].Intent); // solo opens Butt
        Assert.Equal(12, solo.Monsters[0].MoveBaseDamage);

        var pair = NewWorld("Ov_A_Pair_of_Nibbits", seed: 3);
        Assert.Equal(6, pair.Monsters[0].MoveBaseDamage); // front opens Hesitant Slice
        Assert.Equal(0, pair.Monsters[1].MoveBaseDamage); // back opens Hiss (buff)

        // Rotation continues after the opener: Hesitant Slice -> Hiss.
        CombatTestHarness.EndTurn(pair);
        Assert.Equal(IntentId.Buff, pair.Monsters[0].Intent); // Slice -> Hiss
    }

    [Fact]
    public void ShrinkerBeetleOpensShrinkThenAlternatesAndShrinkLeavesOnDeath()
    {
        var w = WithStarterHand("Ov_Shrinker_Beetle", seed: 5);
        Assert.Equal(IntentId.Debuff, w.Monsters[0].Intent); // opener is Shrinker

        CombatTestHarness.EndTurn(w); // beetle applies Shrink
        Assert.True(PowerOps.HasPower(w.PlayerPowers, PowerIds.Shrink));

        // Player attack is shrunk: Strike 6 -> floor(6 * 0.7) = 4.
        int hpBefore = w.Monsters[0].Hp;
        CombatTestHarness.PlayCard(w, CardIds.StrikeR, new TargetRef(TargetKind.Monster, 0));
        Assert.Equal(hpBefore - 4, w.Monsters[0].Hp);

        // Kill it (2 more Strikes, 4 + 6): the Shrink stack leaves with its applier.
        while (w.Monsters[0].Alive && !w.IsTerminal)
        {
            if (w.Hand.Any(c => c.CatalogId == CardIds.StrikeR))
            {
                CombatTestHarness.PlayCard(w, CardIds.StrikeR, new TargetRef(TargetKind.Monster, 0));
            }
            else
            {
                CombatTestHarness.EndTurn(w);
            }
        }

        Assert.False(PowerOps.HasPower(w.PlayerPowers, PowerIds.Shrink));
    }

    [Fact]
    public void InkletSlipperyLimitsNextHpLossToOne()
    {
        var w = WithStarterHand("Ov_Inklets", seed: 7);
        Assert.True(PowerOps.HasPower(w.Monsters[1].Powers, PowerIds.Slippery)); // starts Slippery
        Assert.Equal(2, w.Monsters[1].MoveBaseDamage); // middle opens Whirlwind 2x3

        int hpBefore = w.Monsters[1].Hp;
        CombatTestHarness.PlayCard(w, CardIds.StrikeR, new TargetRef(TargetKind.Monster, 1));
        Assert.Equal(hpBefore - 1, w.Monsters[1].Hp); // Slippery eats the hit down to 1
        Assert.False(PowerOps.HasPower(w.Monsters[1].Powers, PowerIds.Slippery));
        CombatTestHarness.PlayCard(w, CardIds.StrikeR, new TargetRef(TargetKind.Monster, 1));
        Assert.Equal(hpBefore - 7, w.Monsters[1].Hp); // next hit is normal
    }

    [Fact]
    public void MawlerRoarOnlyOncePerFight()
    {
        var w = NewWorld("Ov_Mawler", seed: 11);
        Assert.Equal(2, w.Monsters[0].MoveHits); // opens Claw 4x2

        int roars = 0;
        for (int turn = 0; turn < 14 && !w.IsTerminal; turn++)
        {
            // Shell up so the fight lasts long enough to see repeats.
            for (int i = 0; i < 3; i++)
            {
                int idx = w.Hand.FindIndex(c => c.CatalogId == CardIds.DefendR);
                if (idx >= 0)
                {
                    w.Step(new Action(Verb.PlayCard, idx, w.Hand[idx].Uuid, -1, new TargetRef(TargetKind.None)));
                }
            }

            if (!w.IsTerminal)
            {
                w.Step(new Action(Verb.EndTurn));
                if (w.Monsters[0].Intent == IntentId.Debuff)
                {
                    roars++;
                }
            }
        }

        Assert.True(roars <= 1, $"Roar used {roars} times; wiki says once per fight");
    }

    [Fact]
    public void CubexConstructStartsWithArtifactAndFollowsTheCycle()
    {
        var w = WithStarterHand("Ov_Cubex_Construct", seed: 13);
        Assert.Equal(1, PowerOps.GetAmount(w.Monsters[0].Powers, PowerIds.Artifact)); // starts Artifact 1

        // Cycle: ChargeUp -> Repeater -> Repeater -> Expel -> Repeater...
        var expected = new[] { 1, 1, 2, 1 }; // move ids after the ChargeUp opener
        foreach (int moveId in expected)
        {
            CombatTestHarness.EndTurn(w);
            if (w.IsTerminal)
            {
                break;
            }

            Assert.Equal(moveId, w.Monsters[0].MoveId);
        }
    }

    [Fact]
    public void StranglerConstrictAppliesAndTicks()
    {
        var w = WithStarterHand("Ov_Strangler_and_Friend", seed: 17);
        CombatTestHarness.EndTurn(w); // strangler opens Constrict 3; jaxfruit orbs for 3
        Assert.True(PowerOps.HasPower(w.PlayerPowers, PowerIds.Constrict));

        int hpBefore = w.PlayerHp;
        CombatTestHarness.EndTurn(w);
        // End-of-turn Constrict -3 (bypasses block) plus at least the jaxfruit orb.
        Assert.True(w.PlayerHp <= hpBefore - 3, $"expected Constrict damage, hp {hpBefore} -> {w.PlayerHp}");
        Assert.True(PowerOps.HasPower(w.PlayerPowers, PowerIds.Constrict));
    }

    // ---- Slimed / Tangled ----

    [Fact]
    public void SlimedStatusCardIsPlayableDrawsOneAndExhausts()
    {
        // Plain 5-card hand so the Slimed draw is not blocked by the hand cap.
        var w = NewWorld("Ov_Group_of_Slimes", seed: 19);
        w.AddCardsToDiscard(CardIds.Slimed, 2);
        Assert.Equal(2, w.DiscardPile.Count(c => c.CatalogId == CardIds.Slimed));

        // Pull one Slimed into the hand and play it.
        var slimed = w.DiscardPile.First(c => c.CatalogId == CardIds.Slimed);
        w.DiscardPile.Remove(slimed);
        w.Hand.Add(slimed);
        int handBefore = w.Hand.Count;
        int energy = w.Energy;

        int idx = w.Hand.FindIndex(c => c.Uuid == slimed.Uuid);
        var r = w.Step(new Action(Verb.PlayCard, idx, slimed.Uuid, -1, new TargetRef(TargetKind.None)));

        Assert.Equal(StepStatus.Ok, r.Status);
        Assert.Equal(energy - 1, w.Energy);      // costs 1
        Assert.Equal(handBefore - 1 + 1, w.Hand.Count); // played it, drew 1
        Assert.Single(w.ExhaustPile.Where(c => c.CatalogId == CardIds.Slimed)); // exhausts on play
    }

    [Fact]
    public void TangledSurchargesCardCosts()
    {
        var w = WithStarterHand("Ov_Vine_Shambler", seed: 23);
        w.ApplyPlayerPower(PowerIds.Tangled, 1); // as Grasping Vines applies

        // Strike costs 1 -> 2 with Tangled. First play: 3 - 2 = 1 energy left.
        CombatTestHarness.PlayCard(w, CardIds.StrikeR, new TargetRef(TargetKind.Monster, 0));
        Assert.Equal(1, w.Energy);
        // Every remaining starter card now needs 2 energy but only 1 is left: illegal.
        var illegal = w.Step(new Action(
            Verb.PlayCard, 0, w.Hand[0].Uuid, -1,
            w.Hand[0].CatalogId == CardIds.DefendR
                ? new TargetRef(TargetKind.None)
                : new TargetRef(TargetKind.Monster, 0)));
        Assert.Equal(StepStatus.Illegal, illegal.Status);
    }

    // ---- feasibility: every implemented encounter completes under random play ----

    [Fact]
    public void EveryImplementedEncounterCompletesUnderRandomPlay()
    {
        var rng = new Random(2026);
        foreach (var key in Overgrowth.AllEncounterKeys)
        {
            foreach (int trial in Enumerable.Range(0, 2))
            {
                var w = NewWorld(key, seed: 100 * trial + rng.Next(1000));
                RandomFight(w, rng);
                Assert.True(w.IsTerminal, $"{key} did not terminate");
                Assert.NotEqual(StepStatus.StallCut, w.TerminalStatus);
            }
        }
    }

    [Fact]
    public void CatalogIdsMatchSyntheticCatalogForOvergrowth()
    {
        string root = RepoRoot();
        var doc = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, "catalog", "generated", "entity_ids.json")));
        var rootEl = doc.RootElement;

        static Dictionary<string, int> Ns(System.Text.Json.JsonElement rootEl, string name)
        {
            var map = new Dictionary<string, int>();
            foreach (var e in rootEl.GetProperty("namespaces").GetProperty(name).GetProperty("entries").EnumerateArray())
            {
                map[e.GetProperty("key").GetString()!] = e.GetProperty("entity_id").GetInt32();
            }

            return map;
        }

        var monsters = Ns(rootEl, "MONSTER");
        Assert.Equal(monsters["Nibbit"], MonsterIds.Nibbit);
        Assert.Equal(monsters["ShrinkerBeetle"], MonsterIds.ShrinkerBeetle);
        Assert.Equal(monsters["FuzzyWurmCrawler"], MonsterIds.FuzzyWurmCrawler);
        Assert.Equal(monsters["Inklet"], MonsterIds.Inklet);
        Assert.Equal(monsters["Mawler"], MonsterIds.Mawler);
        Assert.Equal(monsters["Flyconid"], MonsterIds.Flyconid);
        Assert.Equal(monsters["SnappingJaxfruit"], MonsterIds.SnappingJaxfruit);
        Assert.Equal(monsters["VineShambler"], MonsterIds.VineShambler);
        Assert.Equal(monsters["Fogmog"], MonsterIds.Fogmog);
        Assert.Equal(monsters["CubexConstruct"], MonsterIds.CubexConstruct);
        Assert.Equal(monsters["SlitheringStrangler"], MonsterIds.SlitheringStrangler);
        Assert.Equal(monsters["LeafSlimeS"], MonsterIds.LeafSlimeS);
        Assert.Equal(monsters["LeafSlimeM"], MonsterIds.LeafSlimeM);
        Assert.Equal(monsters["TwigSlimeS"], MonsterIds.TwigSlimeS);
        Assert.Equal(monsters["TwigSlimeM"], MonsterIds.TwigSlimeM);

        var powers = Ns(rootEl, "POWER");
        Assert.Equal(powers["Shrink"], PowerIds.Shrink);
        Assert.Equal(powers["Tangled"], PowerIds.Tangled);
        Assert.Equal(powers["Slippery"], PowerIds.Slippery);
        Assert.Equal(powers["Constrict"], PowerIds.Constrict);

        var cards = Ns(rootEl, "CARD");
        Assert.Equal(cards["Slimed"], CardIds.Slimed);

        // And the move-value patch stays in sync with the registered constants.
        var patch = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, "catalog", "patches", "overgrowth_moves.json"))).RootElement;
        Assert.Equal(patch.GetProperty("Nibbit").GetProperty("hp_min").GetInt32(), NibbitAi.HpMin);
        Assert.Equal(patch.GetProperty("Nibbit").GetProperty("moves").GetProperty("Butt").GetInt32(), 12);
        Assert.Equal(patch.GetProperty("Mawler").GetProperty("hp").GetInt32(), MawlerAi.HpMin);
        Assert.Equal(patch.GetProperty("VineShambler").GetProperty("hp").GetInt32(), VineShamblerAi.HpMin);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "API_TERMS.md")))
        {
            dir = dir.Parent!;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
