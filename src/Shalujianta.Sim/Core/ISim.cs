using Shalujianta.Sim.Codec;

namespace Shalujianta.Sim.Core;

public enum StepStatus : byte
{
    Ok,
    Illegal,
    TerminalWin,
    TerminalLoss,
    StallCut,
}

public readonly record struct StepResult(StepStatus Status, float Reward, bool Done);

/// <summary>
/// Run/combat level knobs. Reward is introduced in PR-010; until then StepResult.Reward stays 0.
/// </summary>
public sealed class SimConfig
{
    public required string CatalogVersion { get; init; }
    public ushort SchemaVersion { get; init; } = 1;
    public InfoMode Mode { get; init; } = InfoMode.NoSl;
    public int CharacterId { get; init; }
    public int Ascension { get; init; }
    public int StallTurnLimit { get; init; } = 50;
    public RngBackendKind RngBackend { get; init; } = RngBackendKind.SystemRandomLegacy;
    public bool EnableSlPeekRewards { get; init; }
    public string GameVersionString { get; init; } = "unknown";

    /// <summary>Sink for hook stubs, hand_overflow, zero_cost_warn etc. Null = silent.</summary>
    public System.Action<string>? HookLog { get; init; }
}

/// <summary>
/// Phase-1 world API (§4.10). CloneForSearch / INoSlSearchWorld land with PR-008;
/// Observe() lives on CombatWorld until the full Observation codec lands.
/// </summary>
public interface ISimWorld
{
    SimConfig Config { get; }
    InfoMode Mode { get; }
    ulong Hash64();
    ulong InfoSetHash64();
    SimCheckpoint Checkpoint();
    void Restore(in SimCheckpoint ckpt);

    /// <summary>Deep copy including draw order and all RNG internals. SL ONLY (KD-16).</summary>
    ISimWorld Clone();
    ActionMask LegalActions();
    StepResult Step(in Action action);
}
