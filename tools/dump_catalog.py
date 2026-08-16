#!/usr/bin/env python3
"""Dump a stable EntityId catalog.

Default source is catalog/ci/synthetic/ (no network). Live spire-codex
requires SHALUJIANTA_ALLOW_LIVE_CODEX=1. See docs/API_TERMS.md.

Usage:
    python tools/dump_catalog.py
    python tools/dump_catalog.py --source synthetic
    python tools/dump_catalog.py --source live   # opt-in, never CI
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "python"))

from shalujianta.catalog.dump import (  # noqa: E402
    DEFAULT_CODEX,
    build_catalog,
    load_aliases,
    repo_root,
    write_outputs,
)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--source",
        choices=("synthetic", "live"),
        default="synthetic",
        help="synthetic (CI default) or live Codex (opt-in)",
    )
    parser.add_argument(
        "--synthetic",
        type=Path,
        default=None,
        help="path to catalog/ci/synthetic/entities.json",
    )
    parser.add_argument(
        "--out",
        type=Path,
        default=None,
        help="output directory (default catalog/generated)",
    )
    parser.add_argument(
        "--previous",
        type=Path,
        default=None,
        help="existing entity_ids.json to append onto",
    )
    parser.add_argument("--codex-url", default=DEFAULT_CODEX)
    args = parser.parse_args(argv)

    root = repo_root()
    synthetic = args.synthetic or (root / "catalog" / "ci" / "synthetic" / "entities.json")
    out_dir = args.out or (root / "catalog" / "generated")
    previous = args.previous
    if previous is None:
        candidate = out_dir / "entity_ids.json"
        previous = candidate if candidate.exists() else None
    aliases = load_aliases(root / "catalog" / "patches" / "aliases.yaml")

    catalog = build_catalog(
        source=args.source,
        synthetic_path=synthetic,
        previous_path=previous,
        aliases=aliases,
        codex_url=args.codex_url,
    )
    write_outputs(catalog, out_dir)
    n = sum(len(b["entries"]) for b in catalog["namespaces"].values())
    print(f"wrote {out_dir / 'entity_ids.json'} ({n} entities, source={catalog['source']})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
