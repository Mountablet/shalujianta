"""RewardSpec combat_v0.1 — numeric mirror of src/Shalujianta.Sim/Combat/Reward.cs.

The C# sim computes step rewards; Python keeps the same constants for training
(GAE targets, value-head scale) and for the cross-language equality test.
Changing a coefficient requires a new SpecId on BOTH sides (design 5.3).
"""

from __future__ import annotations

SPEC_ID = "combat_v0.1"

COMBAT_WIN = 1.00
COMBAT_LOSE = -1.00
HP_FRAC = 0.35          # terminal * (hp'/max_hp)
HP_STEP = 0.15          # per-step * delta_hp / max_hp
TURN = -0.004           # per player turn (END_TURN)
STALL_HARD = -0.05      # turn >= 15, no hp change, no kill
POTION_USE = -0.020     # per use
POTION_DUMP = -0.10     # discarding without a kill that turn (v0 simplification)
OVERBLOCK = -0.002      # per point of block beyond incoming+6
ZERO_LOOP = -0.02       # once, on crossing ZERO_COST_WARN (12)
STALL_CUT = -1.00       # turn-limit / zero-cost hard cut
POTION_VALUE_OUTSIDE = 0.08  # win: +, lose: - (opportunity cost)
GAMMA = 0.99
