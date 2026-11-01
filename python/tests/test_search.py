from __future__ import annotations

import pytest

from shalujianta.env import CombatEnv
from shalujianta.env.generator import play_fight, StarterGenerator
from shalujianta.eval.suites import random_policy, run_first_fight
from shalujianta.protocol.constants import END_TURN_SLOT
from shalujianta.protocol.enums import InfoMode
from shalujianta.search.heuristic import features, score
from shalujianta.search.shallow import choose_action, greedy_policy


@pytest.fixture
def nosl_env(bindings_dll: str):
    env = CombatEnv(mode=InfoMode.NOSL)
    try:
        yield env
    finally:
        env.close()


def _obs_of(env: CombatEnv, seed: int = 42) -> dict:
    obs, _ = env.reset(seed=seed)
    return obs


def test_heuristic_prefers_healthy_untouched_board(nosl_env) -> None:
    obs = _obs_of(nosl_env, seed=42)
    healthy = score(obs)

    damaged = _obs_of(nosl_env, seed=42)
    damaged["combat"]["player"]["hp"] = 20
    damaged["combat"]["player"]["block"] = 0
    assert score(damaged) < healthy

    cleared = _obs_of(nosl_env, seed=42)
    for m in cleared["combat"]["monsters"]:
        m["is_gone"] = True
    assert score(cleared) > healthy


def test_sl_only_and_semantics_features_are_structurally_zero(nosl_env) -> None:
    obs = _obs_of(nosl_env, seed=42)
    feats = features(obs)
    assert feats["H17_draw_pile_key_cards"] == 0.0  # SL-only information
    assert feats["H08_playable_attacks"] == 0.0     # card semantics stay server-side in v0
    assert feats["H20_lethal_in_reach"] == 0.0
    assert feats["H15_potions_left"] == 0.0         # v0 fights start with empty slots


def test_shallow_search_runs_on_nosl_env(nosl_env) -> None:
    # If shallow used env.clone() this would raise CapabilityError on a NoSL env.
    obs = _obs_of(nosl_env, seed=42)
    for _ in range(6):
        slot = choose_action(nosl_env, obs, determinization_seed=1)
        mask = nosl_env.legal_action_mask()
        assert bool(mask[slot])
        obs, _r, done, _trunc, _info = nosl_env.step(slot)
        if done:
            break


def test_greedy_policy_returns_legal_action(nosl_env) -> None:
    obs = _obs_of(nosl_env, seed=42)
    slot = greedy_policy(nosl_env, obs)
    assert nosl_env.legal_action_mask()[slot]


def test_heuristic_beats_random_on_first_fight(bindings_dll: str) -> None:
    """v0 gate: Heuristic40 > RandomLegal (design 8.1) on the shared metrics."""
    heur = run_first_fight(30, greedy_policy, seed_base=500)
    rand = run_first_fight(30, random_policy, seed_base=500)

    assert heur["illegal_action_rate"] == 0.0
    assert rand["illegal_action_rate"] == 0.0
    # Both saturate win rate on this easy encounter; the gap shows in HP kept
    # and turns taken (heuristic must not be worse on any of them).
    assert heur["win_rate"] >= rand["win_rate"]
    assert heur["avg_hp_frac"] > rand["avg_hp_frac"]
    assert heur["avg_turns"] < rand["avg_turns"]
