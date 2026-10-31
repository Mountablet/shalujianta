using System.Text.Json;
using Shalujianta.Sim.Codec;

namespace Shalujianta.Sim.Core;

public sealed class SimCheckpoint
{
    public ushort SchemaVersion { get; set; } = 1;
    public string CatalogVersion { get; set; } = "";
    public string GameVersionString { get; set; } = "unknown";
    public int RunSeedI32 { get; set; }
    public int CharacterId { get; set; }
    public int Ascension { get; set; }
    public int Floor { get; set; }
    public int Act { get; set; }
    public int ActVariant { get; set; }
    public InfoMode Mode { get; set; }
    public RngBackendKind RngKind { get; set; }
    public string RngStateBase64 { get; set; } = "";
    public int Turn { get; set; }
    public int CombatId { get; set; }
    public int EncounterId { get; set; }

    /// <summary>Combat live state; null for run-level checkpoints taken outside combat.</summary>
    public CombatSnapshot? Combat { get; set; }

    public static SimCheckpoint Capture(IRngBackend rng, string catalogVersion, string gameVersion)
    {
        if (rng is not MultiRng multi)
        {
            throw new ArgumentException("Capture requires MultiRng.", nameof(rng));
        }

        return new SimCheckpoint
        {
            SchemaVersion = 1,
            CatalogVersion = catalogVersion,
            GameVersionString = gameVersion,
            RunSeedI32 = multi.RunSeed,
            RngKind = multi.Kind,
            RngStateBase64 = Convert.ToBase64String(multi.ExportState()),
        };
    }

    public void RestoreInto(IRngBackend rng)
    {
        rng.ImportState(Convert.FromBase64String(RngStateBase64));
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    public static SimCheckpoint FromJson(string json) =>
        JsonSerializer.Deserialize<SimCheckpoint>(json, JsonOpts)
        ?? throw new InvalidOperationException("empty checkpoint");

    public ulong Hash64()
    {
        var bytes = Convert.FromBase64String(RngStateBase64);
        ulong h = 14695981039346656037UL;
        foreach (byte b in bytes)
        {
            h ^= b;
            h *= 1099511628211UL;
        }

        h ^= (ulong)(uint)RunSeedI32;
        h ^= (ulong)SchemaVersion << 32;
        foreach (char c in GameVersionString)
        {
            h ^= c;
            h *= 1099511628211UL;
        }

        if (Combat is not null)
        {
            byte[] combatBytes = JsonSerializer.SerializeToUtf8Bytes(Combat, JsonOpts);
            h ^= (ulong)combatBytes.Length << 8;
            foreach (byte b in combatBytes)
            {
                h ^= b;
                h *= 1099511628211UL;
            }
        }

        return h;
    }

    // Enums serialize as integers so the JSON matches the Python codec and
    // proto/*.schema.json (mode: 0=NOSL 1=SL, rng_kind: 0=SystemRandomLegacy 1=Pcg32).
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };
}

/// <summary>Combat live state serialized inside a SimCheckpoint (SL is not SL without this).</summary>
public sealed class CombatSnapshot
{
    public int Turn { get; set; }
    public byte Phase { get; set; }
    public int PlayerHp { get; set; }
    public int PlayerMaxHp { get; set; }
    public int PlayerBlock { get; set; }
    public int PlayerEnergy { get; set; }
    public int PlayerMaxEnergy { get; set; }
    public int TimesDamagedThisCombat { get; set; }
    public int CardsPlayedThisTurn { get; set; }
    public int ZeroCostPlaysThisTurn { get; set; }
    public List<PowerSnapshot> PlayerPowers { get; set; } = [];
    public List<CardSnapshot> Hand { get; set; } = [];
    public List<CardSnapshot> Draw { get; set; } = [];
    public List<CardSnapshot> Discard { get; set; } = [];
    public List<CardSnapshot> Exhaust { get; set; } = [];
    public List<CardSnapshot> Limbo { get; set; } = [];
    public List<MonsterSnapshot> Monsters { get; set; } = [];
}

public sealed class CardSnapshot
{
    public int CatalogId { get; set; }
    public int Uuid { get; set; }
    public int Upgrade { get; set; }
    public byte Flags { get; set; }
    public int CurrentCost { get; set; }
    public int Misc { get; set; }
}

public sealed class PowerSnapshot
{
    public int CatalogId { get; set; }
    public int Amount { get; set; }
    public bool JustApplied { get; set; }
}

public sealed class MonsterSnapshot
{
    public int Slot { get; set; }
    public int CatalogId { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Block { get; set; }
    public bool Alive { get; set; }
    public bool HalfDead { get; set; }
    public byte Intent { get; set; }
    public bool IntentKnown { get; set; }
    public int MoveId { get; set; }
    public int MoveBaseDamage { get; set; }
    public int MoveHits { get; set; }
    public int Misc { get; set; }
    public List<PowerSnapshot> Powers { get; set; } = [];
}
