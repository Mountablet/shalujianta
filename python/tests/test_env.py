from __future__ import annotations

import subprocess
from pathlib import Path

import numpy as np
import pytest

from shalujianta.env import CapabilityError, CombatEnv
from shalujianta.protocol.constants import END_TURN_SLOT, FLAT_SIZE
from shalujianta.protocol.enums import InfoMode

ROOT = Path(__file__).resolve().parents[2]


@pytest.fixture(scope="session")
def bindings_dll() -> str:
    """Build the C# bindings bridge once per session."""
    result = subprocess.run(
        ["dotnet", "build", str(ROOT / "src" / "Shalujianta.Bindings"), "--nologo"],
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        pytest.skip(f"bindings build failed: {result.stderr[-400:]}")
    dll = ROOT / "src" / "Shalujianta.Bindings" / "bin" / "Debug" / "net9.0" / "Shalujianta.Bindings.dll"
    assert dll.exists()
    return str(dll)


@pytest.fixture
def sl_env(bindings_dll: str) -> CombatEnv:
    env = CombatEnv(mode=InfoMode.SL)
    try:
        yield env
    finally:
        env.close()


def test_reset_first_frame_is_hand_five(sl_env: CombatEnv) -> None:
    obs, info = sl_env.reset(seed=42)
    assert info["mode"] == "SL"
    assert obs["screen"] == 2  # COMBAT
    assert len(obs["combat"]["hand"]) == 5
    assert obs["combat"]["player"]["energy"] == 3
    assert obs["combat"]["turn"] == 1

    mask = sl_env.legal_action_mask()
    assert mask.shape == (FLAT_SIZE,)
    assert mask.dtype == np.bool_
    assert bool(mask[END_TURN_SLOT])


def test_step_end_turn_resolves_enemy_in_same_step(sl_env: CombatEnv) -> None:
    sl_env.reset(seed=42)
    obs, _r, terminated, truncated, info = sl_env.step(END_TURN_SLOT)
    # Matches the C# golden trace: both rats Scratch for 16 total.
    assert obs["combat"]["turn"] == 2
    assert obs["combat"]["player"]["hp"] == 64
    assert terminated is False
    assert truncated is False
    assert info["status"] == "Ok"


def test_illegal_action_raises(sl_env: CombatEnv) -> None:
    sl_env.reset(seed=42)
    mask = sl_env.legal_action_mask()
    illegal_slot = int(np.flatnonzero(~mask)[0])
    with pytest.raises(ValueError, match="illegal action"):
        sl_env.step(illegal_slot)


def test_clone_is_sl_only_and_independent(sl_env: CombatEnv) -> None:
    sl_env.reset(seed=42)
    clone = sl_env.clone()
    try:
        assert clone.mode == InfoMode.SL
        base_mask = sl_env.legal_action_mask()
        clone_mask = clone.legal_action_mask()
        assert (base_mask == clone_mask).all()

        clone.step(END_TURN_SLOT)
        assert sl_env.observation()["combat"]["turn"] == 1  # base untouched
        assert clone.observation()["combat"]["turn"] == 2
    finally:
        clone.close()


def test_nosl_clone_raises_capability_error(bindings_dll: str) -> None:
    env = CombatEnv(mode=InfoMode.NOSL)
    try:
        env.reset(seed=42)
        with pytest.raises(CapabilityError, match="SL-only"):
            env.clone()
    finally:
        env.close()


def test_clone_for_search_matches_infoset_and_resamples(sl_env: CombatEnv) -> None:
    sl_env.reset(seed=42)
    search = sl_env.clone_for_search(99)
    try:
        assert search.infoset_hash() == sl_env.infoset_hash()

        mask = search.legal_action_mask()
        assert bool(mask[END_TURN_SLOT])
        _obs, _r, _done, info = search.step(END_TURN_SLOT)
        assert info["status"] == "Ok"

        # Resampling a stepped world keeps ITS public state (turn 2), not the base's.
        resampled = search.resample_hidden(7)
        try:
            assert resampled.infoset_hash() == search.infoset_hash()
            _o, _r2, _d2, info2 = resampled.step(END_TURN_SLOT)
            assert info2["status"] == "Ok"
        finally:
            resampled.close()
    finally:
        search.close()


def test_same_seed_is_deterministic(bindings_dll: str) -> None:
    a = CombatEnv(mode=InfoMode.SL)
    b = CombatEnv(mode=InfoMode.SL)
    try:
        obs_a, _ = a.reset(seed=1234)
        obs_b, _ = b.reset(seed=1234)
        assert obs_a["combat"] == obs_b["combat"]

        for _ in range(3):
            mask_a = a.legal_action_mask()
            assert (mask_a == b.legal_action_mask()).all()
            ra = a.step(END_TURN_SLOT)[0]["combat"]
            rb = b.step(END_TURN_SLOT)[0]["combat"]
            assert ra == rb
    finally:
        a.close()
        b.close()


def test_potion_flow_through_env(sl_env: CombatEnv) -> None:
    obs, _ = sl_env.reset(seed=42)
    assert obs["combat"]["potion_slots"] == [0, 0, 0]
    mask = sl_env.legal_action_mask()

    # No potions at combat start; the discard slots are all masked off.
    assert not mask[135:140].any()
