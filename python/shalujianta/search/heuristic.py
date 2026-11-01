"""Heuristic40 baseline (design 5.5).

Weighted features over a NoSL-projected observation dict. Weights are the design
v0 initials (not learned). Features that need card semantics (H08, H20) or
SL-only information (H17) are structurally zero in v0: the bridge never ships
draw contents, and card effects stay server-side until PR-016 embeddings.
"""

from __future__ import annotations

from typing import Any

# H-id -> weight (design 5.5 v0 initials)
WEIGHTS: dict[str, float] = {
    "H00_all_enemies_dead": 100.0,
    "H01_player_dead": -100.0,
    "H02_player_hp_frac": 8.0,
    "H03_enemy_hp_frac": -6.0,
    "H04_incoming_unblocked_frac": -12.0,
    "H05_block": 0.15,
    "H06_excess_block": -0.05,
    "H07_energy_left": -0.4,
    "H08_playable_attacks": 0.3,
    "H09_vulnerable_on_enemy": 1.2,
    "H10_weak_on_enemy": 0.8,
    "H11_vulnerable_on_player": -1.5,
    "H12_weak_on_player": -1.0,
    "H13_strength_player": 0.6,
    "H14_strength_enemy": -0.7,
    "H15_potions_left": 0.5,
    "H16_potions_used_this_turn": -0.3,
    "H17_draw_pile_key_cards": 0.2,  # SL-only; structurally 0 in NoSL (and in the v0 bridge)
    "H18_status_in_hand": -0.4,
    "H20_lethal_in_reach": 4.0,      # needs card damage semantics: 0 in v0
    "H21_aoe_value": 0.4,
    "H22_alive_enemies": -1.5,
    "H23_attack_intent_enemies": -0.5,
    "H24_buff_intent_enemies": 0.8,
    "H26_turn": -0.2,
    "H27_pile_volume": -0.02,
}

_CARD_FLAG_STATUS = 64
_CARD_FLAG_CURSE = 128

# POWER ids (synthetic-v0): Strength 6, Vulnerable 7, Weak 8.
_STRENGTH, _VULNERABLE, _WEAK = 0x04000006, 0x04000007, 0x04000008

_INTENT_ATTACK, _INTENT_BUFF = 1, 3


def features(obs: dict[str, Any]) -> dict[str, float]:
    combat = obs.get("combat") or {}
    player = combat.get("player", {})
    monsters = combat.get("monsters", [])
    alive = [m for m in monsters if not m.get("is_gone")]
    hand = combat.get("hand", [])
    potions = [p for p in combat.get("potion_slots", []) if p != 0]

    def amount(unit: dict[str, Any], power_id: int) -> int:
        return sum(p["amount"] for p in unit.get("powers", []) if p["catalog_id"] == power_id)

    incoming = sum(
        m.get("move_base_damage", 0) * max(1, m.get("move_hits", 1))
        for m in alive
        if m.get("intent_id") == _INTENT_ATTACK and m.get("intent_known", True)
    )
    block = player.get("block", 0)
    hp = player.get("hp", 0)
    max_hp = max(1, player.get("max_hp", 80))
    enemy_hp = sum(m["hp"] for m in alive)
    enemy_max = sum(max(1, m["max_hp"]) for m in alive)

    return {
        "H00_all_enemies_dead": 1.0 if (monsters and not alive) else 0.0,
        "H01_player_dead": 1.0 if hp <= 0 else 0.0,
        "H02_player_hp_frac": hp / max_hp,
        "H03_enemy_hp_frac": (enemy_hp / enemy_max) if alive else 0.0,
        "H04_incoming_unblocked_frac": max(0, incoming - block) / max_hp,
        "H05_block": block,
        "H06_excess_block": max(0, block - incoming),
        "H07_energy_left": player.get("energy", 0),
        "H08_playable_attacks": 0.0,  # needs card damage semantics (v0: 0)
        "H09_vulnerable_on_enemy": sum(amount(m, _VULNERABLE) for m in alive),
        "H10_weak_on_enemy": sum(amount(m, _WEAK) for m in alive),
        "H11_vulnerable_on_player": amount(player, _VULNERABLE),
        "H12_weak_on_player": amount(player, _WEAK),
        "H13_strength_player": amount(player, _STRENGTH),
        "H14_strength_enemy": sum(amount(m, _STRENGTH) for m in alive),
        "H15_potions_left": len(potions),
        "H16_potions_used_this_turn": 0.0,  # not tracked in the v0 observation
        "H17_draw_pile_key_cards": 0.0,  # never observable in the v0 bridge
        "H18_status_in_hand": sum(
            1 for c in hand if c.get("flags", 0) & (_CARD_FLAG_STATUS | _CARD_FLAG_CURSE)
        ),
        "H20_lethal_in_reach": 0.0,  # needs card damage semantics (v0: 0)
        "H21_aoe_value": 0.0,  # needs card targeting semantics (v0: 0)
        "H22_alive_enemies": len(alive),
        "H23_attack_intent_enemies": sum(1 for m in alive if m.get("intent_id") == _INTENT_ATTACK),
        "H24_buff_intent_enemies": sum(1 for m in alive if m.get("intent_id") == _INTENT_BUFF),
        "H26_turn": combat.get("turn", 0),
        "H27_pile_volume": combat.get("draw_size", 0) + combat.get("discard_size", 0) + len(hand),
    }


def score_with_features(obs: dict[str, Any]) -> tuple[float, dict[str, float]]:
    feats = features(obs)
    return sum(w * feats.get(name, 0.0) for name, w in WEIGHTS.items()), feats


def score(obs: dict[str, Any]) -> float:
    return score_with_features(obs)[0]
