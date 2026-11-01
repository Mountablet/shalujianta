"""Random legal rollout against the C# sim (Phase-1 DoD, design 4.1).

Usage:
    python -m shalujianta.tools.random_rollout --slice v0 --n 10000
    python -m shalujianta.tools.random_rollout --n 2000 --mode NOSL

Samples uniformly from the legal mask every step. Any crash, illegal action,
or mask mismatch fails the run (exit 1). Throughput is reported, not gated:
v0 wants ~1e4 steps/s but only the crash-free property is a merge condition.
"""

from __future__ import annotations

import argparse
import random
import sys
import time

import numpy as np

from shalujianta.env import CombatEnv
from shalujianta.protocol.enums import InfoMode


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--slice", default="v0", choices=("v0",))
    parser.add_argument("--n", type=int, default=10000, help="number of steps")
    parser.add_argument("--encounter", default="Act1_TwoTailedRats")
    parser.add_argument("--mode", default="NOSL", choices=("SL", "NOSL"))
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument(
        "--max-steps-per-episode",
        type=int,
        default=120,
        help="safety bound; episodes end earlier on win/loss/stall-cut",
    )
    args = parser.parse_args(argv)

    rng = random.Random(args.seed)
    env = CombatEnv(
        encounter=args.encounter,
        mode=InfoMode.SL if args.mode == "SL" else InfoMode.NOSL,
    )

    steps = 0
    episodes = 0
    wins = losses = cuts = 0
    started = time.perf_counter()

    try:
        obs, _ = env.reset(seed=args.seed)
        episodes += 1
        episode_steps = 0
        while steps < args.n:
            legal = env.legal_action_mask()
            legal_slots = np.flatnonzero(legal)
            if legal_slots.size == 0:
                print(f"FAIL: no legal actions at step {steps}", file=sys.stderr)
                return 1
            slot = int(rng.choice(legal_slots.tolist()))
            obs, _reward, terminated, _trunc, info = env.step(slot)
            steps += 1
            episode_steps += 1

            if terminated:
                status = info["status"]
                if status == "TerminalWin":
                    wins += 1
                elif status == "TerminalLoss":
                    losses += 1
                else:
                    cuts += 1
                obs, _ = env.reset(seed=args.seed + steps)
                episodes += 1
                episode_steps = 0
            elif episode_steps >= args.max_steps_per_episode:
                print(f"FAIL: episode exceeded {args.max_steps_per_episode} steps", file=sys.stderr)
                return 1
    except Exception as exc:  # noqa: BLE001 — the tool's whole job is "no crash"
        print(f"FAIL after {steps} steps: {exc!r}", file=sys.stderr)
        return 1
    finally:
        env.close()

    elapsed = time.perf_counter() - started
    rate = steps / elapsed if elapsed > 0 else 0.0
    print(
        f"ok: {steps} steps / {episodes} episodes in {elapsed:.2f}s "
        f"({rate:,.0f} steps/s) wins={wins} losses={losses} stall_cuts={cuts} "
        f"encounter={args.encounter} mode={args.mode}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
