using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Codec;
using Shalujianta.Sim.Core;
using Xunit;

namespace Shalujianta.Sim.Tests;

/// <summary>
/// Kernel test doubles. Ids use unassigned catalog slots ((ns&lt;&lt;24)|700+) and never
/// collide with the synthetic SliceV0 entities that PR-005 will register.
/// </summary>
internal static class TestCards
{
    internal const int Strike = (1 << 24) | 700;       // 6 dmg, cost 1
    internal const int Defend = (1 << 24) | 701;       // 5 block, cost 1
    internal const int ZeroCostLoop = (1 << 24) | 702; // 1 block + self-recast, cost 0
    internal const int AllHit = (1 << 24) | 703;       // 4 dmg AOE, cost 1
    internal const int ExhaustHit = (1 << 24) | 704;   // 6 dmg, exhausts, cost 1
    internal const int VulnHit = (1 << 24) | 705;      // 6 dmg + Vulnerable 2, cost 2
    internal const int BigHit = (1 << 24) | 706;       // 50 dmg, cost 5
    internal const int RetainBlock = (1 << 24) | 707;  // 1 block, retains, cost 1
    internal const int EtherealBlock = (1 << 24) | 708; // 1 block, ethereal, cost 1
    internal const int NoApply = (1 << 24) | 709;      // def registered, Apply null
    internal const int Unplayable = (1 << 24) | 710;   // Unplayable flag
    internal const int InnateStrike = (1 << 24) | 711; // 6 dmg, innate, cost 1
    internal const int XCostBlock = (1 << 24) | 712;   // X-cost block, banned in v0
    internal const int M1 = (7 << 24) | 700;
    internal const int M2 = (7 << 24) | 701;

    internal static void RegisterAll()
    {
        Register(new CardDef(Strike, CardType.Attack, 1, CardTarget.Enemy, CardFlags.None,
            (w, c, t) => w.DamageMonster(t.Index, 6, w.PlayerPowers)));
        Register(new CardDef(Defend, CardType.Skill, 1, CardTarget.None, CardFlags.None,
            (w, c, t) => w.GainPlayerBlock(5)));
        Register(new CardDef(ZeroCostLoop, CardType.Skill, 0, CardTarget.None, CardFlags.None,
            (w, c, t) =>
            {
                w.GainPlayerBlock(1);
                w.ReturnCardToHand(c.Uuid);
            }));
        Register(new CardDef(AllHit, CardType.Attack, 1, CardTarget.AllEnemies, CardFlags.None,
            (w, c, t) =>
            {
                for (int s = 0; s < w.Monsters.Count; s++)
                {
                    w.DamageMonster(s, 4, w.PlayerPowers);
                }
            }));
        Register(new CardDef(ExhaustHit, CardType.Attack, 1, CardTarget.Enemy, CardFlags.Exhausts,
            (w, c, t) => w.DamageMonster(t.Index, 6, w.PlayerPowers)));
        Register(new CardDef(VulnHit, CardType.Attack, 2, CardTarget.Enemy, CardFlags.None,
            (w, c, t) =>
            {
                w.DamageMonster(t.Index, 6, w.PlayerPowers);
                w.ApplyMonsterPower(t.Index, PowerIds.Vulnerable, 2);
            }));
        Register(new CardDef(BigHit, CardType.Attack, 5, CardTarget.Enemy, CardFlags.None,
            (w, c, t) => w.DamageMonster(t.Index, 50, w.PlayerPowers)));
        Register(new CardDef(RetainBlock, CardType.Skill, 1, CardTarget.None, CardFlags.Retain,
            (w, c, t) => w.GainPlayerBlock(1)));
        Register(new CardDef(EtherealBlock, CardType.Skill, 1, CardTarget.None, CardFlags.Ethereal,
            (w, c, t) => w.GainPlayerBlock(1)));
        Register(new CardDef(NoApply, CardType.Skill, 1, CardTarget.None, CardFlags.None, null));
        Register(new CardDef(Unplayable, CardType.Curse, 1, CardTarget.None, CardFlags.Unplayable,
            (w, c, t) => { }));
        Register(new CardDef(InnateStrike, CardType.Attack, 1, CardTarget.Enemy, CardFlags.Innate,
            (w, c, t) => w.DamageMonster(t.Index, 6, w.PlayerPowers)));
    }

    private static void Register(CardDef def)
    {
        if (!CardRegistry.TryGet(def.CatalogId, out _))
        {
            CardRegistry.Register(def);
        }
    }
}

internal static class CombatTestHarness
{
    internal sealed record WorldHandle(CombatWorld World, List<string> Logs);

    internal static WorldHandle New(
        int seed = 42,
        int hp = 80,
        int stallTurnLimit = 50,
        InfoMode mode = InfoMode.Sl,
        List<CardInstance>? deck = null,
        (int catalogId, int hp, MonsterMove[] moves)[]? monsters = null,
        bool autoStart = true)
    {
        TestCards.RegisterAll();
        var logs = new List<string>();
        var cfg = new SimConfig
        {
            CatalogVersion = "synthetic-v0",
            Mode = mode,
            GameVersionString = "kernel-tests",
            StallTurnLimit = stallTurnLimit,
            HookLog = logs.Add,
        };

        var spawns = new List<MonsterSpawn>();
        foreach (var (catalogId, monsterHp, moves) in monsters ?? [(TestCards.M1, 30, [new MonsterMove(0, IntentId.Attack, 5, 1)])])
        {
            var captured = moves;
            spawns.Add(new MonsterSpawn
            {
                CatalogId = catalogId,
                Hp = monsterHp,
                AiFactory = () => new ScriptedMonsterAi(captured),
            });
        }

        deck ??= new DeckBuilder()
            .Add(TestCards.Strike, 5)
            .Build();

        var start = new CombatStart
        {
            PlayerMaxHp = hp,
            MaxEnergy = 3,
            EncounterId = 9001,
            Deck = deck,
            Monsters = spawns,
        };

        var world = new CombatWorld(cfg, start, seed);
        if (autoStart)
        {
            world.Start();
        }

        return new WorldHandle(world, logs);
    }

    internal static List<CardInstance> Deck(params (int id, int count)[] specs)
    {
        TestCards.RegisterAll();
        var builder = new DeckBuilder();
        foreach (var (id, count) in specs)
        {
            builder.Add(id, count);
        }

        return builder.Build();
    }

    internal static StepResult Play(CombatWorld w, int handIndex, TargetRef? target = null)
    {
        var card = w.Hand[handIndex];
        return w.Step(new Action(
            Verb.PlayCard,
            handIndex,
            card.Uuid,
            -1,
            target ?? new TargetRef(TargetKind.Monster, 0)));
    }

    /// <summary>Plays the first hand card with the given catalog id (battle-start shuffle randomizes order).</summary>
    internal static StepResult PlayCard(CombatWorld w, int catalogId, TargetRef? target = null)
    {
        int idx = w.Hand.FindIndex(c => c.CatalogId == catalogId);
        Assert.True(idx >= 0, $"card {catalogId} not in hand");
        return Play(w, idx, target);
    }

    /// <summary>Pulls specific cards from the (shuffled) draw pile into the hand before turn start.</summary>
    internal static void MoveFromDrawToHand(CombatWorld w, int catalogId, int count)
    {
        for (int n = 0; n < count; n++)
        {
            int idx = w.DrawPile.FindIndex(c => c.CatalogId == catalogId);
            Assert.True(idx >= 0, $"card {catalogId} not in draw pile");
            var card = w.DrawPile[idx];
            w.DrawPile.RemoveAt(idx);
            w.Hand.Add(card);
        }
    }

    internal static StepResult EndTurn(CombatWorld w) => w.Step(new Action(Verb.EndTurn));
}
