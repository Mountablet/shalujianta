from __future__ import annotations

import json
from pathlib import Path

import pytest

from shalujianta.catalog.assign import assert_unk_reserved, assign_ids
from shalujianta.catalog.dump import LIVE_FLAG, build_catalog, write_outputs
from shalujianta.catalog.entity_id import EntityId, EntityNamespace, capacity

ROOT = Path(__file__).resolve().parents[2]
SYNTHETIC = ROOT / "catalog" / "ci" / "synthetic" / "entities.json"


def test_pack_unpack_roundtrip() -> None:
    packed = EntityId.pack(EntityNamespace.CARD, 7)
    assert packed.namespace == int(EntityNamespace.CARD)
    assert packed.local_id == 7
    assert packed.unpack() == (1, 7)
    assert int(EntityId.pack(EntityNamespace.UNK, 0)) == 0


def test_local_id_zero_never_assigned_to_real_entity() -> None:
    assigned = assign_ids({"CARD": ["Bash", "Strike_R"]})
    assert_unk_reserved(assigned)
    card = assigned["namespaces"]["CARD"]
    assert card["unk"]["local_id"] == 0
    assert all(e["local_id"] >= 1 for e in card["entries"])
    assert [e["key"] for e in card["entries"]] == ["Bash", "Strike_R"]


def test_ids_are_sorted_then_stable() -> None:
    first = assign_ids({"CARD": ["Strike_R", "Bash", "Defend_R"]})
    second = assign_ids({"CARD": ["Wound", "Bash", "Strike_R", "Defend_R"]}, first)
    first_map = {e["key"]: e["local_id"] for e in first["namespaces"]["CARD"]["entries"]}
    second_map = {e["key"]: e["local_id"] for e in second["namespaces"]["CARD"]["entries"]}
    assert first_map["Bash"] == second_map["Bash"] == 1
    assert first_map["Defend_R"] == second_map["Defend_R"]
    assert first_map["Strike_R"] == second_map["Strike_R"]
    assert second_map["Wound"] == max(first_map.values()) + 1


def test_retired_keys_keep_holes() -> None:
    first = assign_ids({"POTION": ["FirePotion", "BlockPotion"]})
    second = assign_ids({"POTION": ["BlockPotion"]}, first)
    entries = {e["key"]: e for e in second["namespaces"]["POTION"]["entries"]}
    assert entries["FirePotion"]["retired"] is True
    assert entries["FirePotion"]["local_id"] == 2 or entries["BlockPotion"]["local_id"] != entries["FirePotion"]["local_id"]
    assert entries["BlockPotion"].get("retired") is None


def test_status_card_namespace_stays_empty() -> None:
    assigned = assign_ids({"STATUS_CARD": ["Wound"], "CARD": ["Wound"]})
    assert assigned["namespaces"]["STATUS_CARD"]["entries"] == []
    assert assigned["namespaces"]["CARD"]["entries"][0]["key"] == "Wound"


def test_capacity_guard() -> None:
    tiny = assign_ids({"ORB": []})
    with pytest.raises(ValueError, match="reserved capacity"):
        # force overflow by stuffing previous max at cap-1
        prev = {
            "namespaces": {
                "ORB": {
                    "entries": [
                        {"key": "x", "local_id": capacity("ORB") - 1, "entity_id": 1}
                    ]
                }
            }
        }
        assign_ids({"ORB": ["x", "y"]}, prev)


def test_synthetic_dump_has_slice_v0_and_no_zero_ids(tmp_path: Path) -> None:
    catalog = build_catalog(
        source="synthetic",
        synthetic_path=SYNTHETIC,
        previous_path=None,
        aliases={},
        codex_url="http://127.0.0.1:9",
    )
    assert catalog["source"] == "synthetic"
    write_outputs(catalog, tmp_path)
    written = json.loads((tmp_path / "entity_ids.json").read_text(encoding="utf-8"))
    assert_unk_reserved(written)
    cards = {e["key"]: e for e in written["namespaces"]["CARD"]["entries"]}
    relics = {e["key"]: e for e in written["namespaces"]["RELIC"]["entries"]}
    assert "Strike_R" in cards and "Bash" in cards and "Defend_R" in cards
    assert "BurningBlood" in relics
    assert cards["Bash"]["local_id"] != 0
    assert written["namespaces"]["CARD"]["unk"]["entity_id"] == int(
        EntityId.pack(EntityNamespace.CARD, 0)
    )


def test_live_source_refused_without_env(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv(LIVE_FLAG, raising=False)

    def boom(*_args, **_kwargs):
        raise AssertionError("network should not be touched")

    monkeypatch.setattr("shalujianta.catalog.dump.urllib.request.urlopen", boom)
    with pytest.raises(RuntimeError, match="live Codex dump refused"):
        build_catalog(
            source="live",
            synthetic_path=SYNTHETIC,
            previous_path=None,
            aliases={},
            codex_url="https://example.invalid",
        )
