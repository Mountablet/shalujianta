"""Starter-only CombatGenerator (PR-009).

Design 5.1: v0 allows source=starter only; procedural generation stays disabled
until PR-005b widens the hallway card/encounter pool. GenSpec lives in
gen_spec_v0.yaml and is read with a deliberately tiny parser (no yaml dep).

The generator does not simulate: it produces fight specs (encounter key, seed,
carried HP) that a CombatEnv consumes via reset(options={"player_hp": ...}).
"""

from __future__ import annotations

import random
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable

import numpy as np

from shalujianta.env.combat_env import CombatEnv

REPO_ROOT = Path(__file__).resolve().parents[3]
GEN_SPEC_PATH = REPO_ROOT / "python" / "shalujianta" / "env" / "gen_spec_v0.yaml"


@dataclass(slots=True)
class FightSpec:
    encounter: str
    seed: int
    player_hp: int | None  # None = full HP
    index: int = 0
    source: str = "starter"


@dataclass(slots=True)
class FightResult:
    spec: FightSpec
    win: bool
    status: str
    hp_after: int
    max_hp: int
    turns: int
    potions_left: int
    reward: float
    died_with_lethal_potion: bool = False


class GenRejectExhausted(RuntimeError):
    """Raised when a generator cannot produce a legal spec (design 5.1)."""


def load_gen_spec(path: Path | None = None) -> dict[str, Any]:
    """Tiny key: value parser for gen_spec_v0.yaml (flat, no nesting, # comments)."""
    spec: dict[str, Any] = {}
    for raw in (path or GEN_SPEC_PATH).read_text(encoding="utf-8").splitlines():
        line = raw.split("#", 1)[0].strip()
        if not line or ":" not in line:
            continue
        key, value = (part.strip() for part in line.split(":", 1))
        if value in ("true", "false"):
            spec[key] = value == "true"
        else:
            try:
                spec[key] = int(value)
            except ValueError:
                spec[key] = value
    return spec


class StarterGenerator:
    """SliceV0 fights: Ironclad starter deck + Burning Blood, chosen encounter."""

    def __init__(self, gen_spec_path: Path | None = None) -> None:
        self.spec = load_gen_spec(gen_spec_path)
        if self.spec.get("procedural_enabled", False):
            raise GenRejectExhausted("gen_spec_v0 must ship with procedural_enabled: false")

    def fight(self, seed: int, encounter: str | None = None, player_hp: int | None = None) -> FightSpec:
        return FightSpec(
            encounter=encounter or str(self.spec["encounter"]),
            seed=seed,
            player_hp=player_hp,
            source="starter",
        )

    def procedural(self, *_args: Any, **_kwargs: Any) -> FightSpec:
        raise GenRejectExhausted(
            "procedural generation is disabled until PR-005b (design 5.1); "
            "v0 trains on source=starter only"
        )


def play_fight(
    env: CombatEnv,
    fight: FightSpec,
    policy: Callable[[CombatEnv, dict[str, Any]], int] | None = None,
    max_steps: int = 120,
    on_step: Callable[[dict[str, Any]], None] | None = None,
) -> FightResult:
    """Runs one fight to terminal; policy defaults to uniform random legal."""
    rng = random.Random(fight.seed)
    obs, _info = env.reset(seed=fight.seed, options={"player_hp": fight.player_hp})
    if fight.player_hp is not None:
        assert obs["combat"]["player"]["hp"] == fight.player_hp, "carried HP not applied"
    max_hp = obs["combat"]["player"]["max_hp"]

    reward_total = 0.0
    turns = obs["combat"]["turn"]
    steps = 0
    while True:
        if policy is None:
            mask = env.legal_action_mask()
            slot = int(rng.choice(np.flatnonzero(mask).tolist()))
        else:
            slot = policy(env, obs)
        obs, reward, terminated, _trunc, info = env.step(slot)
        reward_total += reward
        turns = obs["combat"]["turn"]
        steps += 1
        if on_step is not None:
            on_step({
                "event": "step",
                "step": steps,
                "flat": int(slot),
                "reward": reward,
                "status": info["status"],
                "turn": turns,
                "player": obs["combat"]["player"],
                "monsters": obs["combat"]["monsters"],
                "hand": obs["combat"]["hand"],
                "potion_slots": obs["combat"]["potion_slots"],
            })
        if terminated:
            win = info["status"] == "TerminalWin"
            player = obs["combat"]["player"]
            monsters = [m for m in obs["combat"]["monsters"] if not m["is_gone"]]
            potions = [p for p in obs["combat"]["potion_slots"] if p != 0]
            return FightResult(
                spec=fight,
                win=win,
                status=info["status"],
                hp_after=player["hp"],
                max_hp=max_hp,
                turns=turns,
                potions_left=len(potions),
                reward=reward_total,
                # PR-012 formalizes the metric; the cheap v0 estimate is
                # "lost while holding a Fire Potion that could have killed".
                died_with_lethal_potion=(
                    not win and any(p == 29360129 for p in potions)
                    and monsters and min(m["hp"] for m in monsters) <= 20
                ),
            )
        if steps >= max_steps:
            raise RuntimeError(f"fight exceeded {max_steps} steps (stall guard)")


# Fire Potion catalog id (synthetic-v0): namespace 3 << 24 | local 2.
FIRE_POTION_ID = (3 << 24) | 2


class HallwaySuite:
    """v0_act1_hallway (design 5.1.1): fixed fight sequence, HP carried, nothing between.

    Burning Blood (+6 on win, capped) is applied inside the sim at terminal, so the
    carried HP is simply the observed post-terminal HP.
    """

    def __init__(self, length: int = 3, encounter: str = "Act1_TwoTailedRats") -> None:
        self.length = length
        self.encounter = encounter

    def run(
        self,
        env: CombatEnv,
        seed: int,
        policy: Callable[[CombatEnv, dict[str, Any]], int] | None = None,
    ) -> dict[str, Any]:
        hp: int | None = None
        results: list[FightResult] = []
        generator = StarterGenerator()
        for i in range(self.length):
            fight = generator.fight(seed=seed + 1000 + i, encounter=self.encounter, player_hp=hp)
            fight.index = i
            result = play_fight(env, fight, policy=policy)
            results.append(result)
            hp = result.hp_after

        return {
            "suite": "v0_act1_hallway",
            "seed": seed,
            "length": self.length,
            "suite_win": all(r.win for r in results),
            "final_hp_frac": hp / results[0].max_hp if results else 0.0,
            "total_reward": sum(r.reward for r in results),
            "avg_turns": sum(r.turns for r in results) / len(results),
            "fights": [
                {
                    "index": r.spec.index,
                    "win": r.win,
                    "status": r.status,
                    "hp_after": r.hp_after,
                    "turns": r.turns,
                    "potions_left": r.potions_left,
                    "died_with_lethal_potion": r.died_with_lethal_potion,
                }
                for r in results
            ],
        }
