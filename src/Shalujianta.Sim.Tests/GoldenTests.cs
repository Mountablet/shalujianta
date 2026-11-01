using System.Text.Json;
using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Codec;
using Shalujianta.Sim.Content;
using Shalujianta.Sim.Core;
using Xunit;

namespace Shalujianta.Sim.Tests;

/// <summary>
/// Replays golden JSON scripts from tests/golden against the real sim. No sts2.dll
/// is ever loaded; these pin SliceV0 determinism (design 4.12). The first frame of
/// every golden must assert len(hand) == 5 (double-draw regression guard).
/// </summary>
public sealed class GoldenTests
{
    private static readonly Dictionary<string, int> CardKeys = new()
    {
        ["Strike_R"] = CardIds.StrikeR,
        ["Defend_R"] = CardIds.DefendR,
        ["Bash"] = CardIds.Bash,
    };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "API_TERMS.md")))
        {
            dir = dir.Parent!;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    public static IEnumerable<object[]> GoldenFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src", "Shalujianta.Sim.Tests", "golden"), "*.json")
            .Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(GoldenFiles))]
    public void GoldenReplayMatches(string path)
    {
        SliceV0.Register();
        var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        var cfg = new SimConfig
        {
            CatalogVersion = "synthetic-v0",
            Mode = InfoMode.Sl,
            GameVersionString = "golden",
        };
        var start = SliceV0.StarterCombat(EncounterKindOf(root.GetProperty("encounter").GetString()!));
        var world = new CombatWorld(cfg, start, root.GetProperty("seed").GetInt32());
        world.Start();

        AssertExpect(root.GetProperty("initial_expect"), world, result: null, deltas: null);

        foreach (var step in root.GetProperty("steps").EnumerateArray())
        {
            var before = Snapshot(world);
            var result = Step(world, step.GetProperty("action"));
            AssertExpect(step.GetProperty("expect"), world, result, before);
            if (result.Done)
            {
                AssertExpect(root.GetProperty("final_expect"), world, result, before);
                return;
            }
        }

        AssertExpect(root.GetProperty("final_expect"), world, null, null);
    }

    private static SliceV0.EncounterKind EncounterKindOf(string name) => name switch
    {
        "Act1_TwoTailedRats" => SliceV0.EncounterKind.TwoTailedRats,
        "SingleTwoTailedRat" => SliceV0.EncounterKind.SingleRat,
        _ => throw new ArgumentException($"unknown encounter key {name}"),
    };

    private static StepResult Step(CombatWorld world, JsonElement action)
    {
        string verbName = action.GetProperty("verb").GetString()!;
        if (verbName == "PLAY_ALL")
        {
            // Play each named card if it is in the hand (shuffle makes the opening hand
            // order random), then end the turn. "first_alive" is re-resolved before every
            // card so kills pass the target on to the next living monster.
            StepResult result = default;
            foreach (var key in action.GetProperty("cards").EnumerateArray())
            {
                int cardId = CardKeys[key.GetString()!];
                int idx = world.Hand.FindIndex(c => c.CatalogId == cardId);
                if (idx >= 0 && !world.IsTerminal)
                {
                    var target = ParseTarget(world, action);
                    result = world.Step(new Action(Verb.PlayCard, idx, world.Hand[idx].Uuid, -1, target));
                    if (result.Done)
                    {
                        return result;
                    }
                }
            }

            if (action.TryGetProperty("then_end_turn", out var end) && end.GetBoolean())
            {
                result = world.Step(new Action(Verb.EndTurn));
            }

            return result;
        }

        var verb = ParseVerb(verbName);
        if (verb == Verb.EndTurn)
        {
            return world.Step(new Action(Verb.EndTurn));
        }

        if (verb == Verb.PlayCard)
        {
            int cardId = CardKeys[action.GetProperty("card").GetString()!];
            int idx = world.Hand.FindIndex(c => c.CatalogId == cardId);
            Assert.True(idx >= 0, $"golden action: card {cardId} not in hand");
            var target = ParseTarget(world, action);
            return world.Step(new Action(Verb.PlayCard, idx, world.Hand[idx].Uuid, -1, target));
        }

        throw new NotSupportedException($"golden verb {verb} not supported");
    }

    private static Verb ParseVerb(string name) =>
        Enum.TryParse<Verb>(name.Replace("_", ""), ignoreCase: true, out var v)
            ? v
            : throw new ArgumentException($"unknown verb {name}");

    private static TargetRef ParseTarget(CombatWorld world, JsonElement action)
    {
        if (!action.TryGetProperty("target", out var t))
        {
            return new TargetRef(TargetKind.None, -1);
        }

        if (t.ValueKind == JsonValueKind.Number)
        {
            return new TargetRef(TargetKind.Monster, t.GetInt32());
        }

        return t.GetString() switch
        {
            "first_alive" => new TargetRef(TargetKind.Monster, FirstAliveSlot(world)),
            "none" => new TargetRef(TargetKind.None, -1),
            "all_enemies" => new TargetRef(TargetKind.AllEnemies, -1),
            _ => throw new ArgumentException($"unknown target {t.GetString()}"),
        };
    }

    private static int FirstAliveSlot(CombatWorld world)
    {
        for (int i = 0; i < world.Monsters.Count; i++)
        {
            if (world.Monsters[i].Alive)
            {
                return i;
            }
        }

        throw new InvalidOperationException("no living monster for golden target");
    }

    private static int[] MonsterSnapshot(CombatWorld world, Func<MonsterInstance, int> pick)
    {
        var list = new List<int>();
        foreach (var m in world.Monsters)
        {
            list.Add(pick(m));
        }

        return [.. list];
    }

    private static (int PlayerHp, int[] MonsterHp) Snapshot(CombatWorld world) =>
        (world.PlayerHp, MonsterSnapshot(world, m => m.Hp));

    private static void AssertExpect(JsonElement expect, CombatWorld world, StepResult? result, (int, int[])? deltas)
    {
        if (expect.ValueKind == JsonValueKind.Null || !expect.EnumerateObject().Any())
        {
            return;
        }

        foreach (var prop in expect.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "status":
                    Assert.NotNull(result);
                    Assert.Equal(prop.Value.GetString(), result!.Value.Status.ToString());
                    break;
                case "turn":
                    Assert.Equal(prop.Value.GetInt32(), world.Turn);
                    break;
                case "energy":
                    Assert.Equal(prop.Value.GetInt32(), world.Energy);
                    break;
                case "player_hp":
                    Assert.Equal(prop.Value.GetInt32(), world.PlayerHp);
                    break;
                case "player_hp_delta":
                    Assert.NotNull(deltas);
                    Assert.Equal(prop.Value.GetInt32(), world.PlayerHp - deltas!.Value.Item1);
                    break;
                case "player_block":
                    Assert.Equal(prop.Value.GetInt32(), world.PlayerBlock);
                    break;
                case "hand_count":
                    Assert.Equal(prop.Value.GetInt32(), world.Hand.Count);
                    break;
                case "monster_count":
                    Assert.Equal(prop.Value.GetInt32(), world.Monsters.Count);
                    break;
                case "monster_hp":
                    AssertArray(prop.Value, MonsterSnapshot(world, m => m.Hp));
                    break;
                case "monster_hp_delta":
                    Assert.NotNull(deltas);
                    AssertArray(prop.Value, MonsterSnapshot(world, m => m.Hp), deltas!.Value.Item2);
                    break;
                case "monster_vulnerable":
                    AssertArray(prop.Value, MonsterSnapshot(world, m => PowerOps.GetAmount(m.Powers, PowerIds.Vulnerable)));
                    break;
                case "monster_alive":
                    AssertBoolArray(prop.Value, MonsterSnapshot(world, m => m.Alive ? 1 : 0));
                    break;
                default:
                    throw new NotSupportedException($"golden expect field {prop.Name}");
            }
        }
    }

    private static void AssertArray(JsonElement expected, int[] actual, int[]? baseline = null)
    {
        int i = 0;
        foreach (var e in expected.EnumerateArray())
        {
            Assert.True(i < actual.Length, $"monster index {i} out of range");
            if (e.ValueKind != JsonValueKind.Null)
            {
                int want = e.GetInt32() + (baseline?[i] ?? 0);
                Assert.True(want == actual[i], $"monster[{i}]: expected {want}, actual {actual[i]}");
            }

            i++;
        }
    }

    private static void AssertBoolArray(JsonElement expected, int[] actual)
    {
        int i = 0;
        foreach (var e in expected.EnumerateArray())
        {
            Assert.True(i < actual.Length, $"monster index {i} out of range");
            if (e.ValueKind != JsonValueKind.Null)
            {
                int want = e.GetBoolean() ? 1 : 0;
                Assert.True(want == actual[i], $"monster[{i}] alive: expected {want != 0}, actual {actual[i] != 0}");
            }

            i++;
        }
    }
}
