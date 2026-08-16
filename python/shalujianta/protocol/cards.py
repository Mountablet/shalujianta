from __future__ import annotations

from dataclasses import dataclass


@dataclass(slots=True)
class CardInstanceFeat:
    catalog_id: int
    uuid: int
    upgrade: int = 0
    upgrade_branch: int = 0
    current_cost: int = 0
    base_cost: int = 0
    cost_modified: bool = False
    is_playable: bool = True
    has_target: bool = False
    exhausts: bool = False
    ethereal: bool = False
    retain: bool = False
    innate: bool = False
    unplayable: bool = False
    is_x_cost: bool = False
    is_status: bool = False
    is_curse: bool = False
    times_played_this_combat: int = 0
    enchantment_id: int = 0
    generated_from: int = 0
    zone: int = 0
    zone_index: int = 0
    known_in_draw: bool = False
