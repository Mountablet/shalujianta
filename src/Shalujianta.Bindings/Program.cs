using System.Text.Json;
using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Codec;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Content;
using Shalujianta.Sim.Core;

namespace Shalujianta.Bindings;

/// <summary>
/// v0 bridge between Python and the headless sim (KD-20: gRPC/ABI are post-v0; this
/// JSONL-over-stdio process is not the training hot path). One line in, one line out:
///   {"cmd":"new","seed":42,"encounter":"Act1_TwoTailedRats","mode":"SL"}
///   {"cmd":"step","handle":1,"action":{"verb":"PLAY_CARD","card_index":0,"target_kind":"MONSTER","target_index":0}}
///   {"cmd":"legal"|"clone"|"clone_for_search"|"resample"|"hash"|"close","handle":..}
/// Handles to search worlds expose only INoSlSearchWorld semantics.
/// </summary>
public static class Program
{
    private static readonly Dictionary<int, object> Worlds = new();
    private static int _nextHandle;

    public static int Main()
    {
        SliceV0.Register();
        string? line;
        while ((line = Console.ReadLine()) is not null)
        {
            if (line.Trim().Length == 0)
            {
                continue;
            }

            Dictionary<string, object?> response;
            try
            {
                using var doc = JsonDocument.Parse(line);
                response = Dispatch(doc.RootElement);
            }
            catch (Exception ex)
            {
                response = new Dictionary<string, object?> { ["ok"] = false, ["error"] = ex.Message };
            }

            Console.WriteLine(JsonSerializer.Serialize(response));
        }

        return 0;
    }

    private static Dictionary<string, object?> Dispatch(JsonElement req)
    {
        var cmd = req.GetProperty("cmd").GetString();
        switch (cmd)
        {
            case "quit":
                return Resp(true);
            case "new":
                return New(req);
            case "step":
                return Step(req);
            case "legal":
                return Legal(Handle<INoSlSearchWorld>(req, out _));
            case "clone":
                return Clone(req);
            case "clone_for_search":
                return CloneForSearch(req);
            case "resample":
                return Resample(req);
            case "hash":
                return Hash(req);
            case "close":
                Worlds.Remove(req.GetProperty("handle").GetInt32());
                return Resp(true);
            default:
                throw new ArgumentException($"unknown cmd {cmd}");
        }
    }

    private static Dictionary<string, object?> Resp(bool ok, Dictionary<string, object?>? extra = null)
    {
        var dict = new Dictionary<string, object?> { ["ok"] = ok };
        if (extra is not null)
        {
            foreach (var (k, v) in extra)
            {
                dict[k] = v;
            }
        }

        return dict;
    }

    private static T Handle<T>(JsonElement req, out int handle)
        where T : class
    {
        handle = req.GetProperty("handle").GetInt32();
        return (T)Worlds[handle];
    }

    private static Dictionary<string, object?> New(JsonElement req)
    {
        int seed = req.GetProperty("seed").GetInt32();
        var kind = req.GetProperty("encounter").GetString() switch
        {
            "SingleTwoTailedRat" => SliceV0.EncounterKind.SingleRat,
            _ => SliceV0.EncounterKind.TwoTailedRats,
        };
        var mode = req.GetProperty("mode").GetString() == "NOSL" ? InfoMode.NoSl : InfoMode.Sl;
        int stall = req.TryGetProperty("stall_turn_limit", out var st) ? st.GetInt32() : 50;

        var cfg = new SimConfig
        {
            CatalogVersion = "synthetic-v0",
            Mode = mode,
            GameVersionString = "bindings-v0",
            StallTurnLimit = stall,
        };
        var start = SliceV0.StarterCombat(kind);
        if (req.TryGetProperty("player_hp", out var ph))
        {
            start.PlayerHp = ph.GetInt32(); // hallway HP carry (PR-009)
        }

        var world = new CombatWorld(cfg, start, seed);
        world.Start();

        int handle = ++_nextHandle;
        Worlds[handle] = world;
        return Resp(true, new Dictionary<string, object?>
        {
            ["handle"] = handle,
            ["mode"] = mode == InfoMode.Sl ? "SL" : "NOSL",
            ["obs"] = BuildObs(world),
        });
    }

    private static Dictionary<string, object?> Step(JsonElement req)
    {
        int handle = req.GetProperty("handle").GetInt32();
        var action = ParseAction(req.GetProperty("action"));

        if (Worlds[handle] is CombatWorld world)
        {
            var r = world.Step(action);
            return Resp(true, new Dictionary<string, object?>
            {
                ["status"] = r.Status.ToString(),
                ["done"] = r.Done,
                ["reward"] = r.Reward,
                ["obs"] = BuildObs(world),
            });
        }

        var search = (INoSlSearchWorld)Worlds[handle];
        var sr = search.Step(action);
        return Resp(true, new Dictionary<string, object?>
        {
            ["status"] = sr.Status.ToString(),
            ["done"] = sr.Done,
            ["reward"] = sr.Reward,
            ["obs"] = BuildObs(search.Observe(), search.LegalActions()),
        });
    }

    private static Dictionary<string, object?> Legal(INoSlSearchWorld world)
    {
        var mask = world.LegalActions();
        return Resp(true, new Dictionary<string, object?>
        {
            ["flat"] = mask.Flat.Select(b => b).ToArray(),
            ["verbs_allowed"] = mask.VerbsAllowed.Select(b => b).ToArray(),
        });
    }

    private static Dictionary<string, object?> Clone(JsonElement req)
    {
        var world = Handle<CombatWorld>(req, out _);
        var copy = (CombatWorld)world.Clone();
        int handle = ++_nextHandle;
        Worlds[handle] = copy;
        return Resp(true, new Dictionary<string, object?> { ["handle"] = handle, ["obs"] = BuildObs(copy) });
    }

    private static Dictionary<string, object?> CloneForSearch(JsonElement req)
    {
        var world = Handle<CombatWorld>(req, out _);
        ulong seed = req.TryGetProperty("seed", out var s) ? s.GetUInt64() : 1UL;
        var search = (INoSlSearchWorld)world.CloneForSearch(seed);
        int handle = ++_nextHandle;
        Worlds[handle] = search;
        return Resp(true, new Dictionary<string, object?>
        {
            ["handle"] = handle,
            ["search"] = true,
            ["obs"] = BuildObs(search),
        });
    }

    private static Dictionary<string, object?> Resample(JsonElement req)
    {
        var search = Handle<INoSlSearchWorld>(req, out _);
        ulong seed = req.TryGetProperty("seed", out var s) ? s.GetUInt64() : 1UL;
        var resampled = search.ResampleHidden(seed);
        int handle = ++_nextHandle;
        Worlds[handle] = resampled;
        return Resp(true, new Dictionary<string, object?>
        {
            ["handle"] = handle,
            ["search"] = true,
            ["obs"] = BuildObs(resampled),
        });
    }

    private static Dictionary<string, object?> Hash(JsonElement req)
    {
        int handle = req.GetProperty("handle").GetInt32();
        if (Worlds[handle] is CombatWorld world)
        {
            return Resp(true, new Dictionary<string, object?>
            {
                ["hash64"] = world.Hash64().ToString(),
                ["infoset_hash64"] = world.InfoSetHash64().ToString(),
            });
        }

        var search = (INoSlSearchWorld)Worlds[handle];
        return Resp(true, new Dictionary<string, object?>
        {
            ["infoset_hash64"] = search.InfoSetHash64().ToString(),
        });
    }

    private static Action ParseAction(JsonElement a)
    {
        var verb = Enum.TryParse<Verb>(a.GetProperty("verb").GetString()!.Replace("_", ""), true, out var v)
            ? v
            : Verb.Noop;
        var target = new TargetRef(
            Enum.TryParse<TargetKind>(a.TryGetProperty("target_kind", out var tk) ? tk.GetString() ?? "None" : "None", true, out var tkind) ? tkind : TargetKind.None,
            a.TryGetProperty("target_index", out var ti) ? ti.GetInt32() : -1);
        return new Action(
            verb,
            a.TryGetProperty("card_index", out var ci) ? ci.GetInt32() : -1,
            a.TryGetProperty("card_uuid", out var cu) ? cu.GetInt32() : -1,
            a.TryGetProperty("potion_slot", out var ps) ? ps.GetInt32() : -1,
            target,
            a.TryGetProperty("option_index", out var oi) ? oi.GetInt32() : -1);
    }

    /// <summary>
    /// Observation-schema-shaped payload. The v0 bridge never ships draw order — the
    /// ordered pile stays inside the C# process (SL JSON export is a PR-014 trace job).
    /// </summary>
    private static Dictionary<string, object?> BuildObs(CombatWorld world)
    {
        var obs = world.Observe();
        return BuildObs(obs, world.LegalActions(), world, world.Config);
    }

    private static Dictionary<string, object?> BuildObs(INoSlSearchWorld search)
    {
        return BuildObs(search.Observe(), search.LegalActions(), null, null);
    }

    private static Dictionary<string, object?> BuildObs(
        CombatObservation obs, ActionMask mask, CombatWorld? realWorld = null, SimConfig? config = null)
    {
        var combat = new Dictionary<string, object?>
        {
            ["turn"] = obs.Turn,
            ["phase"] = (int)obs.Phase,
            ["player"] = new Dictionary<string, object?>
            {
                ["character_id"] = config?.CharacterId ?? 0,
                ["hp"] = obs.Player.Hp,
                ["max_hp"] = obs.Player.MaxHp,
                ["block"] = obs.Player.Block,
                ["energy"] = obs.Player.Energy,
                ["max_energy"] = obs.Player.MaxEnergy,
                ["powers"] = obs.Player.Powers.Select(p => new Dictionary<string, object?>
                {
                    ["catalog_id"] = p.CatalogId,
                    ["amount"] = p.Amount,
                    ["just_applied"] = p.JustApplied,
                }).ToList(),
            },
            ["monsters"] = obs.Monsters.Select(m => new Dictionary<string, object?>
            {
                ["slot"] = m.Slot,
                ["catalog_id"] = m.CatalogId,
                ["hp"] = m.Hp,
                ["max_hp"] = m.MaxHp,
                ["block"] = m.Block,
                ["is_gone"] = !m.Alive,
                ["intent_known"] = m.IntentKnown,
                ["intent_id"] = m.IntentId,
                ["move_id"] = m.IntentKnown ? m.IntentId : -1,
                ["move_base_damage"] = m.MoveBaseDamage,
                ["move_hits"] = m.MoveHits,
                ["powers"] = m.Powers.Select(p => new Dictionary<string, object?>
                {
                    ["catalog_id"] = p.CatalogId,
                    ["amount"] = p.Amount,
                    ["just_applied"] = p.JustApplied,
                }).ToList(),
            }).ToList(),
            ["hand"] = obs.Hand.Select(c => new Dictionary<string, object?>
            {
                ["catalog_id"] = c.CatalogId,
                ["uuid"] = c.Uuid,
                ["upgrade"] = c.Upgrade,
                ["current_cost"] = c.CurrentCost,
                ["known_in_draw"] = false,
            }).ToList(),
            ["draw_pile"] = Array.Empty<object>(),   // v0 bridge: order never leaves the sim
            ["draw_bag_counts"] = obs.DrawBagCounts.Select(b => new[] { b.CatalogId, b.Count }).ToList(),
            ["discard_pile"] = Array.Empty<object>(),
            ["exhaust_pile"] = Array.Empty<object>(),
            ["limbo"] = Array.Empty<object>(),
            ["draw_size"] = obs.DrawCount,
            ["discard_size"] = obs.DiscardCount,
            ["exhaust_size"] = obs.ExhaustCount,
            ["potion_slots"] = obs.Potions,
            ["shuffle_pending"] = obs.ShufflePending,
        };

        var run = new Dictionary<string, object?>
        {
            ["floor"] = 1,
            ["act"] = 1,
            ["ascension"] = config?.Ascension ?? 0,
            ["character_id"] = config?.CharacterId ?? 0,
            ["gold"] = 99,
            ["hp"] = obs.Player.Hp,
            ["max_hp"] = obs.Player.MaxHp,
            ["potion_slots_count"] = obs.Potions.Count,
        };

        var payload = new Dictionary<string, object?>
        {
            ["schema_version"] = obs.SchemaVersion,
            ["catalog_version"] = obs.CatalogVersion,
            ["mode"] = (int)obs.Mode,
            ["screen"] = (int)obs.Screen,
            ["in_combat"] = true,
            ["run"] = run,
            ["combat"] = combat,
            ["legal"] = new Dictionary<string, object?>
            {
                ["n"] = ActionMask.Size,
                ["flat"] = mask.Flat.Select(b => b).ToList(),
                ["verbs_allowed"] = mask.VerbsAllowed.Select(b => b).ToList(),
                ["playable_hand"] = mask.PlayableHand.Select(b => b).ToList(),
                ["potion_usable"] = mask.PotionUsable.Select(b => b).ToList(),
                ["potion_discardable"] = mask.PotionDiscardable.Select(b => b).ToList(),
            },
        };

        if (realWorld is not null)
        {
            payload["debug"] = new Dictionary<string, object?>
            {
                ["seed_string"] = "bindings-handle",
                ["game_version_string"] = config?.GameVersionString ?? "unknown",
            };
        }

        return payload;
    }
}
