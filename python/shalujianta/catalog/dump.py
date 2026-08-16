"""Build entity_ids.json from the synthetic CI catalog or (opt-in) spire-codex.

See docs/API_TERMS.md. CI and the default CLI path never call the live API.
"""

from __future__ import annotations

import json
import os
import urllib.request
from pathlib import Path
from typing import Any

from shalujianta.catalog.assign import SKIP_ASSIGN, assert_unk_reserved, assign_ids
from shalujianta.catalog.entity_id import EntityNamespace

SCHEMA_VERSION = 1
DEFAULT_CODEX = "https://spire-codex.com"
LIVE_FLAG = "SHALUJIANTA_ALLOW_LIVE_CODEX"

# Codex list endpoints used only when --source live is explicitly enabled.
LIVE_ENDPOINTS: dict[str, str] = {
    "CARD": "/api/cards",
    "RELIC": "/api/relics",
    "POTION": "/api/potions",
    "POWER": "/api/powers",
    "ORB": "/api/orbs",
    "MONSTER": "/api/monsters",
    "INTENT": "/api/intents",
    "EVENT": "/api/events",
    "KEYWORD": "/api/keywords",
    "ENCHANTMENT": "/api/enchantments",
    "AFFLICTION": "/api/afflictions",
    "ENCOUNTER": "/api/encounters",
    "MODIFIER": "/api/modifiers",
}

HANDMADE_DEFAULTS: dict[str, list[str]] = {
    "MAP_NODE": ["M", "Q", "$", "E", "R", "T", "B"],
    "SCREEN": [
        "NONE",
        "MAIN_MENU",
        "COMBAT",
        "HAND_SELECT",
        "CARD_SELECT_GRID",
        "TARGET_SELECT",
        "MAP",
        "REST",
        "SHOP",
        "EVENT",
        "CARD_REWARD",
        "CHEST",
        "BOSS_RELIC",
        "CARD_REMOVE",
        "CARD_UPGRADE",
        "CARD_TRANSFORM",
        "CARD_DUPLICATE",
        "NEOW",
        "ANCIENT",
        "ENCHANT",
        "GAME_OVER",
        "VICTORY",
        "POTION_REWARD",
        "RELIC_REWARD",
        "COMBAT_REWARD",
        "UNKNOWN",
    ],
    "STANCE": [],
    "COMPANION": [],
    "ANCIENT": [],
}


def repo_root() -> Path:
    here = Path(__file__).resolve()
    for parent in here.parents:
        if (parent / "docs" / "API_TERMS.md").exists():
            return parent
    return Path.cwd()


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def load_aliases(path: Path) -> dict[str, str]:
    if not path.exists():
        return {}
    aliases: dict[str, str] = {}
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.split("#", 1)[0].strip()
        if not line or ":" not in line:
            continue
        src, dst = line.split(":", 1)
        src, dst = src.strip(), dst.strip()
        if src and dst:
            aliases[src] = dst
    return aliases


def apply_alias(key: str, aliases: dict[str, str]) -> str:
    return aliases.get(key, key)


def keys_from_synthetic(entities: dict[str, Any], aliases: dict[str, str]) -> dict[str, list[str]]:
    keys: dict[str, list[str]] = {ns.name: [] for ns in EntityNamespace}
    for ns_name, rows in entities.get("namespaces", {}).items():
        if ns_name in SKIP_ASSIGN:
            continue
        for row in rows:
            key = apply_alias(str(row["key"]), aliases)
            keys.setdefault(ns_name, []).append(key)
    for ns_name, extra in HANDMADE_DEFAULTS.items():
        keys.setdefault(ns_name, [])
        existing = set(keys[ns_name])
        for key in extra:
            aliased = apply_alias(key, aliases)
            if aliased not in existing:
                keys[ns_name].append(aliased)
                existing.add(aliased)
    return keys


def fetch_live_keys(base_url: str, aliases: dict[str, str]) -> dict[str, list[str]]:
    if os.environ.get(LIVE_FLAG) != "1":
        raise RuntimeError(
            f"live Codex dump refused. Set {LIVE_FLAG}=1 and pass --source live. "
            "CI must never do this (see docs/API_TERMS.md)."
        )
    keys: dict[str, list[str]] = {ns.name: [] for ns in EntityNamespace}
    for ns_name, extra in HANDMADE_DEFAULTS.items():
        keys[ns_name].extend(apply_alias(k, aliases) for k in extra)
    for ns_name, path in LIVE_ENDPOINTS.items():
        url = base_url.rstrip("/") + path
        with urllib.request.urlopen(url, timeout=60) as resp:  # noqa: S310 — opt-in live only
            payload = json.loads(resp.read().decode("utf-8"))
        rows = payload if isinstance(payload, list) else payload.get("items", [])
        for row in rows:
            raw = row.get("id") or row.get("key") or row.get("name")
            if not raw:
                continue
            keys.setdefault(ns_name, []).append(apply_alias(str(raw), aliases))
    return keys


def attach_summaries(
    assigned: dict[str, Any],
    entities: dict[str, Any] | None,
) -> dict[str, Any]:
    if not entities:
        return assigned
    by_ns = entities.get("namespaces", {})
    for ns_name, block in assigned["namespaces"].items():
        summaries = {row["key"]: row for row in by_ns.get(ns_name, [])}
        for entry in block["entries"]:
            extra = summaries.get(entry["key"])
            if not extra:
                continue
            summary = {
                k: extra[k]
                for k in ("name", "cost", "rarity", "type", "hp", "max_hp")
                if k in extra and extra[k] is not None
            }
            if summary:
                entry["summary"] = summary
    return assigned


def build_catalog(
    *,
    source: str,
    synthetic_path: Path,
    previous_path: Path | None,
    aliases: dict[str, str],
    codex_url: str,
) -> dict[str, Any]:
    previous = load_json(previous_path) if previous_path and previous_path.exists() else None
    entities = None
    if source == "synthetic":
        entities = load_json(synthetic_path)
        keys = keys_from_synthetic(entities, aliases)
        catalog_version = str(entities.get("catalog_version", "synthetic-v0"))
        game_version = str(entities.get("game_version_string", "synthetic"))
    elif source == "live":
        keys = fetch_live_keys(codex_url, aliases)
        catalog_version = "live"
        game_version = "unknown"
    else:
        raise ValueError(f"unknown source: {source}")

    assigned = assign_ids(keys, previous)
    assert_unk_reserved(assigned)
    assigned = attach_summaries(assigned, entities)
    assigned["schema_version"] = SCHEMA_VERSION
    assigned["catalog_version"] = catalog_version
    assigned["game_version_string"] = game_version
    assigned["source"] = source
    return assigned


def write_outputs(catalog: dict[str, Any], out_dir: Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    ids_path = out_dir / "entity_ids.json"
    ids_path.write_text(json.dumps(catalog, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    meta = {
        "schema_version": catalog["schema_version"],
        "catalog_version": catalog["catalog_version"],
        "game_version_string": catalog["game_version_string"],
        "source": catalog["source"],
    }
    (out_dir / "schema_version.json").write_text(
        json.dumps(meta, indent=2) + "\n", encoding="utf-8"
    )
