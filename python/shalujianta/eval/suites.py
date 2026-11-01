"""Fixed-seed evaluation suites (design 5.9 / PR-012).

Suite definitions live in suites.yaml (flat key: value, no yaml dep).
run_suite executes an agent over a suite and reports:
  win_rate, avg_hp_frac, avg_turns, avg_reward, died_with_lethal_potion,
  illegal_action_rate (must stay 0), stall_cut_rate.
"""

from __future__ import annotations

import json
import random
import time
from pathlib import Path
from typing import Any, Callable

import numpy as np

from shalujianta.env import CombatEnv
from shalujianta.env.generator import FIRE_POTION_ID, play_fight
from shalujianta.env.generator import FightSpec, StarterGenerator
from shalujianta.protocol.enums import InfoMode

REPO_ROOT = Path(__file__).resolve().parents[3]
SUITES_PATH = Path(__file__).resolve().parent / "suites.yaml"
RESULTS_DIR = Path(__file__).resolve().parent / "results"


def load_suites(path: Path | None = None) -> dict[str, dict[str, Any]]:
    suites: dict[str, dict[str, Any]] = {}
    current: str | None = None
    for raw in (path or SUITES_PATH).read_text(encoding="utf-8").splitlines():
        line = raw.split("#", 1)[0].rstrip()
        if not line.strip():
            continue
        if not line.startswith(" "):
            current = line.strip().rstrip(":")
            suites[current] = {}
        elif current is not None:
            key, value = (p.strip() for p in line.split(":", 1))
            try:
                suites[current][key] = int(value)
            except ValueError:
                suites[current][key] = value
    return suites


def random_policy(env: CombatEnv, obs: dict[str, Any]) -> int:
    mask = env.legal_action_mask()
    return int(random.choice(np.flatnonzero(mask).tolist()))


def run_first_fight(
    n: int,
    policy: Callable[[CombatEnv, dict[str, Any]], int],
    mode: InfoMode = InfoMode.NOSL,
    encounter: str = "Act1_TwoTailedRats",
    seed_base: int = 0,
    trace_path: Path | None = None,
    trace_episodes: int = 3,
) -> dict[str, Any]:
    """Starter deck + Burning Blood over `n` fixed seeds of one hallway fight.

    The first `trace_episodes` fights are also written as JSONL for the replay
    viewer (tools/replay_viewer/).
    """
    env = CombatEnv(encounter=encounter, mode=mode)
    generator = StarterGenerator()
    wins = 0
    deaths_with_lethal = 0
    stall_cuts = 0
    hp_fracs: list[float] = []
    turns: list[int] = []
    rewards: list[float] = []
    started = time.perf_counter()
    trace = open(trace_path, "w", encoding="utf-8") if trace_path else None

    def record(event: dict[str, Any]) -> None:
        if trace is not None:
            trace.write(json.dumps(event, ensure_ascii=False) + "\n")

    try:
        for i in range(n):
            fight: FightSpec = generator.fight(seed=seed_base + i)
            on_step = None
            if trace is not None and i < trace_episodes:
                obs0, _info = env.reset(seed=fight.seed)
                record({
                    "event": "reset", "episode": i, "seed": fight.seed, "turn": 1,
                    "player": obs0["combat"]["player"],
                    "monsters": obs0["combat"]["monsters"],
                    "hand": obs0["combat"]["hand"],
                    "potion_slots": obs0["combat"]["potion_slots"],
                })
                on_step = lambda ev, ep=i: record({**ev, "episode": ep})  # noqa: E731
            result = play_fight(env, fight, policy=policy, on_step=on_step)
            wins += 1 if result.win else 0
            deaths_with_lethal += 1 if result.died_with_lethal_potion else 0
            stall_cuts += 1 if result.status == "StallCut" else 0
            hp_fracs.append(result.hp_after / result.max_hp)
            turns.append(result.turns)
            rewards.append(result.reward)
    finally:
        if trace is not None:
            trace.close()
        env.close()

    return {
        "suite": "v0_first_fight",
        "policy": getattr(policy, "__name__", str(policy)),
        "mode": mode.name,
        "encounter": encounter,
        "n": n,
        "seed_base": seed_base,
        "win_rate": wins / n,
        "avg_hp_frac": sum(hp_fracs) / n,
        "avg_turns": sum(turns) / n,
        "avg_reward": sum(rewards) / n,
        "died_with_lethal_potion": deaths_with_lethal,
        "stall_cut_rate": stall_cuts / n,
        "illegal_action_rate": 0.0,  # env.step raises on illegal; reaching here means 0
        "wall_clock_s": round(time.perf_counter() - started, 2),
    }


def run_hallway_suite(
    n: int,
    policy: Callable[[CombatEnv, dict[str, Any]], int],
    mode: InfoMode = InfoMode.NOSL,
    length: int = 3,
    seed_base: int = 0,
) -> dict[str, Any]:
    """v0_act1_hallway: n runs of the 3-fight HP-carry sequence."""
    from shalujianta.env.generator import HallwaySuite

    env = CombatEnv(mode=mode)
    suite = HallwaySuite(length=length)
    suite_wins = 0
    finals: list[float] = []
    lethal = 0
    started = time.perf_counter()
    try:
        for i in range(n):
            report = suite.run(env, seed=seed_base + i, policy=policy)
            suite_wins += 1 if report["suite_win"] else 0
            finals.append(report["final_hp_frac"])
            lethal += sum(1 for f in report["fights"] if f["died_with_lethal_potion"])
    finally:
        env.close()

    return {
        "suite": "v0_act1_hallway",
        "policy": getattr(policy, "__name__", str(policy)),
        "mode": mode.name,
        "n": n,
        "length": length,
        "suite_win_rate": suite_wins / n,
        "avg_final_hp_frac": sum(finals) / n,
        "died_with_lethal_potion": lethal,
        "illegal_action_rate": 0.0,
        "wall_clock_s": round(time.perf_counter() - started, 2),
    }


def save_result(result: dict[str, Any], name: str) -> Path:
    RESULTS_DIR.mkdir(parents=True, exist_ok=True)
    out = RESULTS_DIR / name
    out.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return out
