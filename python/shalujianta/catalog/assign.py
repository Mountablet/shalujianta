"""Append-only local_id assignment. local_id 0 is permanently UNK."""

from __future__ import annotations

from typing import Any

from shalujianta.catalog.entity_id import (
    RESERVED_CAPACITIES,
    EntityId,
    EntityNamespace,
    capacity,
)

# Status/Curse stay in CARD. GENERATED has no dump table.
SKIP_ASSIGN = frozenset({"STATUS_CARD", "GENERATED", "UNK"})


def assign_ids(
    keys_by_ns: dict[str, list[str]],
    previous: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """Assign stable local_ids.

    New keys in each namespace are sorted and appended after the current max.
    Removed keys keep their id (holes). Nothing ever receives local_id 0.
    """
    prev_ns = (previous or {}).get("namespaces", {})
    out: dict[str, Any] = {"namespaces": {}}

    for ns in EntityNamespace:
        name = ns.name
        cap = capacity(name)
        prev_entries = {
            e["key"]: e
            for e in prev_ns.get(name, {}).get("entries", [])
            if e.get("local_id", 0) != 0
        }

        unk_eid = int(EntityId.pack(ns, 0))
        block: dict[str, Any] = {
            "value": int(ns),
            "reserved_capacity": cap,
            "unk": {"key": "UNK", "local_id": 0, "entity_id": unk_eid},
            "entries": [],
        }

        if name in SKIP_ASSIGN:
            # Keep any previously recorded holes; never add new rows.
            block["entries"] = sorted(
                prev_entries.values(), key=lambda e: e["local_id"]
            )
            out["namespaces"][name] = block
            continue

        incoming = list(dict.fromkeys(keys_by_ns.get(name, [])))
        if any(k == "UNK" for k in incoming):
            raise ValueError(f"{name}: key UNK is reserved for local_id 0")

        next_id = 1
        if prev_entries:
            next_id = max(e["local_id"] for e in prev_entries.values()) + 1

        merged = dict(prev_entries)
        new_keys = sorted(k for k in incoming if k not in merged)
        for key in new_keys:
            if next_id >= cap:
                raise ValueError(
                    f"{name}: local_id {next_id} exceeds reserved capacity {cap}"
                )
            merged[key] = {
                "key": key,
                "local_id": next_id,
                "entity_id": int(EntityId.pack(ns, next_id)),
            }
            next_id += 1

        incoming_set = set(incoming)
        for key, entry in merged.items():
            if key not in incoming_set:
                entry = dict(entry)
                entry["retired"] = True
                merged[key] = entry
            elif entry.get("retired"):
                entry = dict(entry)
                entry.pop("retired", None)
                merged[key] = entry
            if entry["local_id"] == 0:
                raise AssertionError(f"{name}: real entity {key} has local_id 0")

        block["entries"] = sorted(merged.values(), key=lambda e: e["local_id"])
        out["namespaces"][name] = block

    # Unused reserved namespace numbers 21-255 are not materialized.
    _ = RESERVED_CAPACITIES
    return out


def assert_unk_reserved(catalog: dict[str, Any]) -> None:
    for name, block in catalog["namespaces"].items():
        if block["unk"]["local_id"] != 0:
            raise AssertionError(f"{name}: UNK local_id is not 0")
        for entry in block["entries"]:
            if entry["local_id"] == 0:
                raise AssertionError(f"{name}: entity {entry['key']} stole local_id 0")
