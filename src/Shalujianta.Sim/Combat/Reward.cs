namespace Shalujianta.Sim.Combat;

/// <summary>
/// RewardSpec combat_v0.1 (design 5.3). Must stay numerically identical to
/// python/shalujianta/train/rewards.py — enforced by RewardConstantsMatchPython.
/// Changing any coefficient requires a new SpecId and archived old curves.
/// </summary>
public static class RewardV0
{
    public const string SpecId = "combat_v0.1";

    public const float CombatWin = 1.00f;
    public const float CombatLose = -1.00f;
    public const float HpFrac = 0.35f;          // terminal * (hp'/max_hp)
    public const float HpStep = 0.15f;          // per-step * delta_hp / max_hp
    public const float Turn = -0.004f;          // per player turn (END_TURN)
    public const float StallHard = -0.05f;      // turn >= 15, no hp change, no kill
    public const float PotionUse = -0.020f;     // per use
    public const float PotionDump = -0.10f;     // discarding without a kill that turn
    public const float Overblock = -0.002f;     // per point of block beyond incoming+6
    public const float ZeroLoop = -0.02f;       // once, on crossing ZERO_COST_WARN
    public const float StallCut = -1.00f;       // turn-limit / zero-cost hard cut
    public const float PotionValueOutside = 0.08f;  // win: +, lose: - (opportunity cost)
    public const float Gamma = 0.99f;           // training-side discount, kept for parity
}
