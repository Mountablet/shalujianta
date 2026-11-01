from __future__ import annotations

import re
from pathlib import Path

import pytest

from shalujianta.train import rewards

ROOT = Path(__file__).resolve().parents[2]
REWARD_CS = ROOT / "src" / "Shalujianta.Sim" / "Combat" / "Reward.cs"

# python name -> C# constant name
PAIRS = {
    "SPEC_ID": "SpecId",
    "COMBAT_WIN": "CombatWin",
    "COMBAT_LOSE": "CombatLose",
    "HP_FRAC": "HpFrac",
    "HP_STEP": "HpStep",
    "TURN": "Turn",
    "STALL_HARD": "StallHard",
    "POTION_USE": "PotionUse",
    "POTION_DUMP": "PotionDump",
    "OVERBLOCK": "Overblock",
    "ZERO_LOOP": "ZeroLoop",
    "STALL_CUT": "StallCut",
    "POTION_VALUE_OUTSIDE": "PotionValueOutside",
    "GAMMA": "Gamma",
}


def test_reward_constants_match_csharp() -> None:
    text = REWARD_CS.read_text(encoding="utf-8")
    for py_name, cs_name in PAIRS.items():
        assert hasattr(rewards, py_name), f"rewards.py is missing {py_name}"
        if py_name == "SPEC_ID":
            match = re.search(rf'public const string {cs_name} = "([^"]+)";', text)
        else:
            match = re.search(rf"public const float {cs_name} = (-?[\d.]+)f;", text)
        assert match, f"Reward.cs is missing {cs_name}"
        cs_value = match.group(1) if py_name == "SPEC_ID" else float(match.group(1))
        assert rewards.__getattribute__(py_name) == cs_value, cs_name


def test_reward_env_passes_csharp_reward_through(bindings_dll: str) -> None:
    from shalujianta.env import CombatEnv
    from shalujianta.protocol.constants import END_TURN_SLOT
    from shalujianta.protocol.enums import InfoMode

    env = CombatEnv(mode=InfoMode.NOSL)
    try:
        env.reset(seed=42)
        _obs, reward, _done, _trunc, _info = env.step(END_TURN_SLOT)
        # turn penalty + per-step hp shaping from the two scripted rats: strictly negative
        assert reward < 0.0
    finally:
        env.close()


def test_win_reward_exceeds_loss_reward_via_env(bindings_dll: str) -> None:
    from shalujianta.env import CombatEnv
    from shalujianta.env.generator import play_fight, StarterGenerator
    from shalujianta.protocol.enums import InfoMode

    env = CombatEnv(mode=InfoMode.NOSL)
    try:
        gen = StarterGenerator()
        win = play_fight(env, gen.fight(seed=42))
        loss = play_fight(env, gen.fight(seed=99, player_hp=1))
        if win.win and not loss.win:
            assert win.reward > 0 > loss.reward
    finally:
        env.close()
