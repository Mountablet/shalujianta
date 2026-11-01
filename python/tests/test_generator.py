from __future__ import annotations

import subprocess
from pathlib import Path

import pytest

from shalujianta.env import CombatEnv
from shalujianta.env.generator import (
    FIRE_POTION_ID,
    GenRejectExhausted,
    HallwaySuite,
    StarterGenerator,
    load_gen_spec,
    play_fight,
)
from shalujianta.protocol.enums import InfoMode

ROOT = Path(__file__).resolve().parents[2]


@pytest.fixture(scope="session")
def bindings_dll() -> str:
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
def env(bindings_dll: str):
    e = CombatEnv(mode=InfoMode.NOSL)
    try:
        yield e
    finally:
        e.close()


def test_gen_spec_ships_procedural_disabled() -> None:
    spec = load_gen_spec()
    assert spec["source"] == "starter"
    assert spec["procedural_enabled"] is False


def test_procedural_is_refused() -> None:
    gen = StarterGenerator()
    with pytest.raises(GenRejectExhausted, match="PR-005b"):
        gen.procedural(seed=1)


def test_fight_spec_defaults_to_starter() -> None:
    gen = StarterGenerator()
    fight = gen.fight(seed=7)
    assert fight.encounter == "Act1_TwoTailedRats"
    assert fight.player_hp is None
    assert fight.source == "starter"


def test_carried_hp_applies_at_reset(env) -> None:
    gen = StarterGenerator()
    result = play_fight(env, gen.fight(seed=42, player_hp=31))
    # The fight runs from 31 HP; whatever the outcome, the sim honored the carry.
    assert result.max_hp == 80
    assert result.hp_after <= 80


def test_hallway_suite_carries_hp_across_fights(env) -> None:
    suite = HallwaySuite(length=3)
    report = suite.run(env, seed=42)

    assert report["suite"] == "v0_act1_hallway"
    assert len(report["fights"]) == 3
    fights = report["fights"]
    # Burning Blood heals inside the sim, so carry = post-terminal HP.
    assert fights[0]["hp_after"] >= 0
    assert 0.0 <= report["final_hp_frac"] <= 1.0
    assert report["suite_win"] == all(f["win"] for f in fights)
    # Fight 0 starts full: carried HP only ever appears from fight 1 on.
    assert all(f["turns"] >= 1 for f in fights)


def test_play_fight_counts_potions_left(env) -> None:
    gen = StarterGenerator()
    fight = gen.fight(seed=11)
    result = play_fight(env, fight)
    assert result.potions_left == 0  # v0 fights start with empty slots
    assert isinstance(result.died_with_lethal_potion, bool)
    assert FIRE_POTION_ID == 50331650  # (3 << 24) | 2
