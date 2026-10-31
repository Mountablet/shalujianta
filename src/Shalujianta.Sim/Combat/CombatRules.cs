namespace Shalujianta.Sim.Combat;

/// <summary>
/// Named combat constants (§4.9 / §4.11). Numbers are pinned; reward coefficients
/// live in PR-010's Reward.cs, not here.
/// </summary>
public static class CombatRules
{
    public const int HandCap = 10;
    public const int DrawPerTurn = 5;
    public const int MaxMonsters = 6;
    public const int DefaultMaxEnergy = 3;

    /// <summary>Zero-cost plays in one player turn without END_TURN before a warn is logged.</summary>
    public const int ZeroCostWarn = 12;

    /// <summary>
    /// Zero-cost plays in one player turn without END_TURN that hard-cut the combat
    /// with StepStatus.StallCut. Independent of SimConfig.StallTurnLimit (§4.9).
    /// </summary>
    public const int MaxZeroCostHardCut = 32;

    // STS-convention multipliers. Final authority is the live dll; PR-014 parity
    // will re-check these before any golden parity claim.
    public const float VulnerableMultiplier = 1.5f;
    public const float WeakMultiplier = 0.75f;
    public const float FrailMultiplier = 0.75f;
}
