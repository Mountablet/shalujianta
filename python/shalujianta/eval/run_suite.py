"""Run an evaluation suite and store the numbers.

Usage:
    PYTHONPATH=python python -m shalujianta.eval.run_suite \
        --suite v0_first_fight --policy heuristic --n 100 \
        --out v0_first_fight_heuristic40.json
"""

from __future__ import annotations

import argparse
from pathlib import Path

from shalujianta.eval.suites import load_suites, random_policy, run_first_fight, run_hallway_suite, save_result
from shalujianta.protocol.enums import InfoMode
from shalujianta.search.shallow import greedy_policy


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--suite", default="v0_first_fight",
                        choices=("v0_first_fight", "v0_act1_hallway"))
    parser.add_argument("--policy", default="heuristic", choices=("heuristic", "random"))
    parser.add_argument("--n", type=int, default=None, help="override suite n")
    parser.add_argument("--mode", default="NOSL", choices=("SL", "NOSL"))
    parser.add_argument("--seed-base", type=int, default=0)
    parser.add_argument("--out", default=None, help="results file name under eval/results/")
    parser.add_argument("--trace", default=None, help="write the first episodes as JSONL for the replay viewer")
    args = parser.parse_args(argv)

    suites = load_suites()
    n = args.n or int(suites[args.suite]["n"])
    policy = greedy_policy if args.policy == "heuristic" else random_policy
    mode = InfoMode.SL if args.mode == "SL" else InfoMode.NOSL

    if args.suite == "v0_first_fight":
        result = run_first_fight(
            n, policy, mode=mode, seed_base=args.seed_base,
            trace_path=Path(args.trace) if args.trace else None,
        )
    else:
        result = run_hallway_suite(n, policy, mode=mode, seed_base=args.seed_base)

    print(result)
    if args.out:
        path = save_result(result, args.out)
        print(f"saved: {path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
