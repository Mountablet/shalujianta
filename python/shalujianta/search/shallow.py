"""1-ply shallow search (design 5.5 / PR-011).

For every legal flat action the world is determinized ONCE via
clone_for_search (shared seed = one realization, fair comparison across
actions), the action is applied on the search handle, and the successor is
scored with Heuristic40 plus the immediate reward.

KD-16 gate: this module must never call env.clone() — it only uses
clone_for_search, so running it against a NoSL env is proof by construction.
"""

from __future__ import annotations

from typing import Any

import numpy as np

from shalujianta.env import CombatEnv
from shalujianta.search.heuristic import score

TERMINAL_WIN_SCORE = 100.0
TERMINAL_LOSS_SCORE = -100.0


def choose_action(env: CombatEnv, obs: dict[str, Any], determinization_seed: int = 1) -> int:
    """Returns the flat slot of the best legal action for the current state."""
    mask = env.legal_action_mask()
    legal = np.flatnonzero(mask).tolist()
    if not legal:
        raise RuntimeError("no legal actions")

    best_slot = legal[0]
    best_score = -float("inf")
    for slot in legal:
        handle = env.clone_for_search(determinization_seed)
        try:
            _obs2, reward, done, info = handle.step(slot)
            if done:
                value = TERMINAL_WIN_SCORE if info["status"] == "TerminalWin" else TERMINAL_LOSS_SCORE
            else:
                value = reward + score(handle.observation())
        finally:
            handle.close()
        if value > best_score:
            best_score = value
            best_slot = slot
    return best_slot


def greedy_policy(env: CombatEnv, obs: dict[str, Any], seed_base: int = 0) -> int:
    """Policy callable for generator.play_fight / suites: one-ply heuristic search."""
    turn = obs.get("combat", {}).get("turn", 0)
    return choose_action(env, obs, determinization_seed=seed_base * 100 + turn)
