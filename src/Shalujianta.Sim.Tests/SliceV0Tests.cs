using System.Text.Json;
using Shalujianta.Sim.Cards.Ironclad;
using Shalujianta.Sim.Codec;
using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Monsters;
using Shalujianta.Sim.Content;
using Shalujianta.Sim.Core;
using Xunit;

namespace Shalujianta.Sim.Tests;

/// <summary>SliceV0 content tests (PR-005): starter deck, Burning Blood, Two-Tailed Rat AI.</summary>
public sealed class SliceV0Tests
{
    [Fact]
    public void StarterDeckIsFiveStrikesFourDefendsOneBash()
    {
        SliceV0.Register();
        var deck = SliceV0.IroncladStarterDeck();
        Assert.Equal(10, deck.Count);
        Assert.Equal(5, deck.Count(c => c.CatalogId == CardIds.StrikeR));
        Assert.Equal(4, deck.Count(c => c.CatalogId == CardIds.DefendR));
        Assert.Equal(1, deck.Count(c => c.CatalogId == CardIds.Bash));

        var start = SliceV0.StarterCombat(SliceV0.EncounterKind.TwoTailedRats);
        Assert.Equal(RelicIds.BurningBlood, Assert.Single(start.RelicIds));
        Assert.Equal(80, start.PlayerMaxHp);
    }

    [Fact]
    public void BurningBloodHealsSixCappedOnWin()
    {
        SliceV0.Register();
        var cfg = new SimConfig { CatalogVersion = "synthetic-v0", Mode = InfoMode.Sl, GameVersionString = "slice-v0-tests" };
        var world = new CombatWorld(cfg, SliceV0.StarterCombat(SliceV0.EncounterKind.SingleRat), runSeedI32: 7);
        world.Start();

        int prev = world.PlayerHp;
        for (int turn = 0; turn < 8 && !world.IsTerminal; turn++)
        {
            foreach (int id in new[] { CardIds.Bash, CardIds.StrikeR, CardIds.StrikeR })
            {
                if (world.IsTerminal)
                {
                    break;
                }

                int idx = world.Hand.FindIndex(c => c.CatalogId == id);
                if (idx >= 0 && world.Monsters.Any(m => m.Alive))
                {
                    int slot = world.Monsters.FindIndex(m => m.Alive);
                    prev = world.PlayerHp;
                    world.Step(new Action(Verb.PlayCard, idx, world.Hand[idx].Uuid, -1, new TargetRef(TargetKind.Monster, slot)));
                }
            }

            if (!world.IsTerminal)
            {
                world.Step(new Action(Verb.EndTurn));
            }
        }

        Assert.Equal(StepStatus.TerminalWin, world.TerminalStatus);
        Assert.Equal(Math.Min(80, prev + 6), world.PlayerHp);
    }

    [Fact]
    public void RatAiNeverRepeatsTheSameMoveTwiceInARow()
    {
        SliceV0.Register();
        var cfg = new SimConfig { CatalogVersion = "synthetic-v0", Mode = InfoMode.Sl, GameVersionString = "slice-v0-tests" };
        var world = new CombatWorld(cfg, SliceV0.StarterCombat(SliceV0.EncounterKind.TwoTailedRats), runSeedI32: 123);
        world.Start();

        var history = world.Monsters.Select(m => new List<int> { m.MoveId }).ToList();
        for (int turn = 0; turn < 8 && !world.IsTerminal; turn++)
        {
            // Defensive shell: block against the rats' attacks.
            for (int i = 0; i < 3; i++)
            {
                int idx = world.Hand.FindIndex(c => c.CatalogId == CardIds.DefendR);
                if (idx >= 0)
                {
                    world.Step(new Action(Verb.PlayCard, idx, world.Hand[idx].Uuid, -1, new TargetRef(TargetKind.None)));
                }
            }

            if (!world.IsTerminal)
            {
                world.Step(new Action(Verb.EndTurn));
                for (int slot = 0; slot < world.Monsters.Count; slot++)
                {
                    if (world.Monsters[slot].Alive)
                    {
                        history[slot].Add(world.Monsters[slot].MoveId);
                    }
                }
            }
        }

        foreach (var seq in history)
        {
            Assert.True(seq.Count >= 5, "expected a multi-turn history");
            for (int i = 1; i < seq.Count; i++)
            {
                Assert.NotEqual(seq[i - 1], seq[i]);
            }
        }
    }

    [Fact]
    public void RatHpIsRolledOnMonsterAiStreamWithinWikiRange()
    {
        SliceV0.Register();
        var cfg = new SimConfig { CatalogVersion = "synthetic-v0", Mode = InfoMode.Sl, GameVersionString = "slice-v0-tests" };
        var a = new CombatWorld(cfg, SliceV0.StarterCombat(SliceV0.EncounterKind.TwoTailedRats), runSeedI32: 42);
        a.Start();
        var b = new CombatWorld(cfg, SliceV0.StarterCombat(SliceV0.EncounterKind.TwoTailedRats), runSeedI32: 42);
        b.Start();
        var c = new CombatWorld(cfg, SliceV0.StarterCombat(SliceV0.EncounterKind.TwoTailedRats), runSeedI32: 43);
        c.Start();

        foreach (var w in new[] { a, b, c })
        {
            foreach (var m in w.Monsters)
            {
                Assert.InRange(m.Hp, 17, 21);
            }
        }

        Assert.Equal(a.Monsters[0].Hp, b.Monsters[0].Hp);
        Assert.Equal(a.Monsters[1].Hp, b.Monsters[1].Hp);
    }

    [Fact]
    public void CatalogIdsMatchSyntheticCatalog()
    {
        string root = RepoRoot();
        var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "catalog", "generated", "entity_ids.json")));
        static Dictionary<string, int> Ns(JsonElement rootEl, string name)
        {
            var map = new Dictionary<string, int>();
            foreach (var e in rootEl.GetProperty("namespaces").GetProperty(name).GetProperty("entries").EnumerateArray())
            {
                map[e.GetProperty("key").GetString()!] = e.GetProperty("entity_id").GetInt32();
            }

            return map;
        }

        var rootEl = doc.RootElement;
        var cards = Ns(rootEl, "CARD");
        Assert.Equal(cards["AscendersBane"], CardIds.AscendersBane);
        Assert.Equal(cards["Bash"], CardIds.Bash);
        Assert.Equal(cards["Defend_R"], CardIds.DefendR);
        Assert.Equal(cards["Strike_R"], CardIds.StrikeR);
        Assert.Equal(cards["Wound"], CardIds.Wound);

        var relics = Ns(rootEl, "RELIC");
        Assert.Equal(relics["BurningBlood"], RelicIds.BurningBlood);

        var potions = Ns(rootEl, "POTION");
        Assert.Equal(potions["BlockPotion"], PotionIds.BlockPotion);
        Assert.Equal(potions["FirePotion"], PotionIds.FirePotion);

        var monsters = Ns(rootEl, "MONSTER");
        Assert.Equal(monsters["TwoTailedRat"], MonsterIds.TwoTailedRat);

        var encounters = Ns(rootEl, "ENCOUNTER");
        Assert.Equal(encounters["Act1_TwoTailedRats"], EncounterIds.Act1TwoTailedRats);

        // And the move-value patch stays in sync with the registered card defs.
        var patch = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "catalog", "patches", "slice_v0_moves.json"))).RootElement;
        Assert.Equal(patch.GetProperty("cards").GetProperty("Strike_R").GetProperty("damage").GetInt32(), Strike.Damage);
        Assert.Equal(patch.GetProperty("cards").GetProperty("Bash").GetProperty("damage").GetInt32(), Bash.Damage);
        Assert.Equal(patch.GetProperty("cards").GetProperty("Bash").GetProperty("vulnerable").GetInt32(), Bash.Vulnerable);
        Assert.Equal(patch.GetProperty("monsters").GetProperty("TwoTailedRat").GetProperty("hp_min").GetInt32(), TwoTailedRatAi.HpMin);
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
