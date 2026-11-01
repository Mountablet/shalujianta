"""CombatEnv over the C# headless sim via the Bindings JSONL bridge (PR-008).

The bridge process owns every piece of hidden state; Python only ever sees
NoSL-projected observations (bag counts, no draw order, no rng counters).
API is gymnasium-shaped (reset/step/legal_action_mask); gymnasium.Env
subclassing lands with the training PRs.

- CombatEnv.clone(): SL only; NoSL raises CapabilityError (KD-16).
- CombatEnv.clone_for_search(seed): PIMC determinization returning a
  NoSlSearchEnv that speaks INoSlSearchWorld semantics only.
- step() accepts a flat slot int or a protocol Action; illegal actions raise
  ValueError (the caller must sample from legal_action_mask()).
"""

from __future__ import annotations

import json
import os
import subprocess
from pathlib import Path
from typing import Any

import numpy as np

from shalujianta.protocol.action import Action, TargetRef, flat_to_action
from shalujianta.protocol.enums import InfoMode, Verb

REPO_ROOT = Path(__file__).resolve().parents[3]
DEFAULT_BINDINGS = REPO_ROOT / "src" / "Shalujianta.Bindings" / "bin" / "Debug" / "net9.0" / "Shalujianta.Bindings.dll"

_TARGET_KIND_NAMES = {
    0: "None",
    1: "Monster",
    2: "Player",
    3: "AllEnemies",
    4: "RandomEnemy",
    5: "Companion",
}


class CapabilityError(RuntimeError):
    """Raised when the current info mode forbids an operation (SL/NoSL isolation)."""


class _Bridge:
    """One Bindings subprocess shared by any number of world handles."""

    def __init__(self, dll: str | None = None) -> None:
        path = dll or os.environ.get("SHALUJIANTA_BINDINGS", str(DEFAULT_BINDINGS))
        if not Path(path).exists():
            raise RuntimeError(
                f"bindings executable not found: {path}\n"
                "Build it first:  dotnet build src/Shalujianta.Bindings"
            )
        self.proc = subprocess.Popen(
            ["dotnet", path],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            text=True,
            encoding="utf-8",
        )

    def request(self, payload: dict[str, Any]) -> dict[str, Any]:
        if self.proc.stdin is None or self.proc.stdout is None:
            raise RuntimeError("bridge is closed")
        self.proc.stdin.write(json.dumps(payload) + "\n")
        self.proc.stdin.flush()
        line = self.proc.stdout.readline()
        if not line:
            raise RuntimeError("bindings process exited unexpectedly")
        resp = json.loads(line)
        if not resp.get("ok"):
            raise RuntimeError(f"bindings error: {resp.get('error')}")
        return resp

    def close(self) -> None:
        try:
            self.request({"cmd": "quit"})
        except Exception:
            pass
        if self.proc.stdin:
            self.proc.stdin.close()
        self.proc.terminate()
        try:
            self.proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            self.proc.kill()


def _action_payload(action: Action | int) -> dict[str, Any]:
    act = flat_to_action(action) if isinstance(action, int) else action
    return {
        "verb": act.verb.name,
        "card_index": act.card_index,
        "card_uuid": act.card_uuid,
        "potion_slot": act.potion_slot,
        "target_kind": _TARGET_KIND_NAMES.get(act.target.kind, "None"),
        "target_index": act.target.index,
        "option_index": act.option_index,
    }


class _WorldHandle:
    def __init__(self, bridge: _Bridge, handle: int) -> None:
        self._bridge = bridge
        self._handle = handle
        self._obs: dict[str, Any] | None = None
        self._legal: np.ndarray | None = None

    def _absorb(self, obs: dict[str, Any]) -> dict[str, Any]:
        self._obs = obs
        self._legal = np.asarray(obs["legal"]["flat"], dtype=bool)
        return obs

    def legal_action_mask(self) -> np.ndarray:
        if self._legal is None:
            raise RuntimeError("call reset() before requesting the legal mask")
        return self._legal.copy()

    def observation(self) -> dict[str, Any]:
        if self._obs is None:
            raise RuntimeError("call reset() first")
        return self._obs

    def step(self, action: Action | int) -> tuple[dict[str, Any], float, bool, dict[str, Any]]:
        resp = self._bridge.request(
            {"cmd": "step", "handle": self._handle, "action": _action_payload(action)}
        )
        if resp["status"] == "Illegal":
            raise ValueError(f"illegal action rejected by the sim: {action}")
        obs = self._absorb(resp["obs"])
        done = bool(resp["done"])
        return obs, float(resp["reward"]), done, {"status": resp["status"]}

    def infoset_hash(self) -> int:
        resp = self._bridge.request({"cmd": "hash", "handle": self._handle})
        return int(resp["infoset_hash64"])

    def close(self) -> None:
        self._bridge.request({"cmd": "close", "handle": self._handle})


class CombatEnv:
    """Single-combat environment. reset() -> (obs, info); step() -> (obs, r, terminated, truncated, info)."""

    metadata = {"render_modes": ["json"]}

    def __init__(
        self,
        encounter: str = "Act1_TwoTailedRats",
        mode: InfoMode = InfoMode.SL,
        stall_turn_limit: int = 50,
        bridge: _Bridge | None = None,
    ) -> None:
        self._owns_bridge = bridge is None
        self._bridge = bridge or _Bridge()
        self._encounter = encounter
        self._mode = mode
        self._stall = stall_turn_limit
        self._handle: _WorldHandle | None = None

    @property
    def mode(self) -> InfoMode:
        return self._mode

    def reset(self, seed: int | None = None, options: dict[str, Any] | None = None) -> tuple[dict[str, Any], dict[str, Any]]:
        if self._handle is not None:
            self._handle.close()
        options = options or {}
        payload: dict[str, Any] = {
            "cmd": "new",
            "seed": int(seed) if seed is not None else 0,
            "encounter": options.get("encounter", self._encounter),
            "mode": "SL" if self._mode == InfoMode.SL else "NOSL",
            "stall_turn_limit": self._stall,
        }
        if options.get("player_hp") is not None:
            payload["player_hp"] = int(options["player_hp"])  # hallway HP carry (PR-009)
        resp = self._bridge.request(payload)
        self._handle = _WorldHandle(self._bridge, resp["handle"])
        obs = self._handle._absorb(resp["obs"])
        return obs, {"handle": resp["handle"], "mode": resp["mode"]}

    def legal_action_mask(self) -> np.ndarray:
        if self._handle is None:
            raise RuntimeError("call reset() first")
        return self._handle.legal_action_mask()

    def observation(self) -> dict[str, Any]:
        if self._handle is None:
            raise RuntimeError("call reset() first")
        return self._handle.observation()

    def step(self, action: Action | int) -> tuple[dict[str, Any], float, bool, bool, dict[str, Any]]:
        if self._handle is None:
            raise RuntimeError("call reset() first")
        obs, reward, done, info = self._handle.step(action)
        return obs, reward, done, False, info

    def clone(self) -> "CombatEnv":
        """SL ONLY: deep copy including draw order and rng (KD-16)."""
        if self._mode != InfoMode.SL:
            raise CapabilityError("clone() is SL-only; NoSL agents must use clone_for_search()")
        if self._handle is None:
            raise RuntimeError("call reset() first")
        resp = self._bridge.request({"cmd": "clone", "handle": self._handle._handle})
        env = CombatEnv.__new__(CombatEnv)
        env._owns_bridge = False
        env._bridge = self._bridge
        env._encounter = self._encounter
        env._mode = self._mode
        env._stall = self._stall
        env._handle = _WorldHandle(self._bridge, resp["handle"])
        env._handle._absorb(resp["obs"])
        return env

    def clone_for_search(self, resample_seed: int) -> "NoSlSearchEnv":
        """PIMC determinization: NoSL search may only hold this handle (KD-16)."""
        if self._handle is None:
            raise RuntimeError("call reset() first")
        resp = self._bridge.request(
            {"cmd": "clone_for_search", "handle": self._handle._handle, "seed": int(resample_seed)}
        )
        return NoSlSearchEnv(self._bridge, resp["handle"], obs=resp.get("obs"))

    def infoset_hash(self) -> int:
        if self._handle is None:
            raise RuntimeError("call reset() first")
        return self._handle.infoset_hash()

    def close(self) -> None:
        if self._handle is not None:
            self._handle.close()
            self._handle = None
        if self._owns_bridge:
            self._bridge.close()

    def __enter__(self) -> "CombatEnv":
        return self

    def __exit__(self, *_exc: object) -> None:
        self.close()


class NoSlSearchEnv:
    """INoSlSearchWorld handle: step/legal/infoset/resample only. No clone, no restore."""

    def __init__(self, bridge: _Bridge, handle: int, obs: dict[str, Any] | None = None) -> None:
        self._delegate = _WorldHandle(bridge, handle)
        if obs is not None:
            self._delegate._absorb(obs)

    def legal_action_mask(self) -> np.ndarray:
        return self._delegate.legal_action_mask()

    def observation(self) -> dict[str, Any]:
        return self._delegate.observation()

    def step(self, action: Action | int) -> tuple[dict[str, Any], float, bool, dict[str, Any]]:
        return self._delegate.step(action)

    def resample_hidden(self, resample_seed: int) -> "NoSlSearchEnv":
        resp = self._delegate._bridge.request(
            {"cmd": "resample", "handle": self._delegate._handle, "seed": int(resample_seed)}
        )
        return NoSlSearchEnv(self._delegate._bridge, resp["handle"], obs=resp.get("obs"))

    def infoset_hash(self) -> int:
        return self._delegate.infoset_hash()

    def close(self) -> None:
        self._delegate.close()
