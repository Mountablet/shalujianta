from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def deterministic_hash_code(text: str) -> int:
    def i32(v: int) -> int:
        v &= 0xFFFFFFFF
        if v >= 0x80000000:
            v -= 0x100000000
        return v

    num = 352_654_597
    num2 = num
    for i in range(0, len(text), 2):
        num = i32(i32((num << 5) + num) ^ ord(text[i]))
        if i == len(text) - 1:
            break
        num2 = i32(i32((num2 << 5) + num2) ^ ord(text[i + 1]))
    return i32(num + i32(num2 * 1_566_083_941))


def test_known_seed_string_hashes_to_int_min() -> None:
    assert deterministic_hash_code("0P2ENNHM") == -2_147_483_648


def test_hash_string_table_covers_required_streams() -> None:
    table = json.loads(
        (ROOT / "catalog" / "generated" / "rng_stream_hash_strings.json").read_text(
            encoding="utf-8"
        )
    )
    by_enum = {row["enum"]: row for row in table["streams"]}
    assert by_enum["Shuffle"]["hash_string"] == "shuffle"
    assert by_enum["CombatOrbGeneration"]["hash_string"] == "combat_orbs"
    assert by_enum["Neow"]["hash_string"] == "NEOW"
    assert by_enum["Neow"]["extra_offset"] == 1
    assert by_enum["UpFront"]["hash_string"] == "up_front"


def test_crng_retest_record_keeps_legacy_default() -> None:
    record = json.loads(
        (ROOT / "catalog" / "generated" / "crng_retest.json").read_text(encoding="utf-8")
    )
    assert record["default_backend"] == "SystemRandomLegacy"
    assert record["switch_to_pcg32"] is False
    assert record["game_sts2_dll_retest"] == "not_run_ci_forbids_dll"
