using System.Text.Json;
using System.Text.Json.Serialization;
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

        return h;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
