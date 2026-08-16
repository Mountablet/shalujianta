from __future__ import annotations

from dataclasses import dataclass, field
from typing import Optional

from shalujianta.protocol.action import ActionMask
from shalujianta.protocol.cards import CardInstanceFeat
from shalujianta.protocol.constants import SCHEMA_VERSION
from shalujianta.protocol.enums import CombatPhase, InfoMode, ScreenType


@dataclass(slots=True)
class PowerStack:
    catalog_id: int
    amount: int
    just_applied: bool = False
    extra: int = 0


@dataclass(slots=True)
class OrbSlot:
    catalog_id: int = 0
    passive_value: int = 0
    evoke_value: int = 0


@dataclass(slots=True)
class CompanionState:
    catalog_id: int
    hp: int
    max_hp: int
    block: int = 0
    is_dead: bool = False
    powers: list[PowerStack] = field(default_factory=list)
    intercepts_hits: bool = False


@dataclass(slots=True)
class MonsterState:
    slot: int
    catalog_id: int
    hp: int
    max_hp: int
    block: int = 0
    is_gone: bool = False
    half_dead: bool = False
    intent_id: int = 0
    intent_known: bool = True
    move_id: int = -1
    move_base_damage: int = 0
    move_adjusted_damage: int = -1
    move_hits: int = 1
    powers: list[PowerStack] = field(default_factory=list)
    last_move_id: int = -1


@dataclass(slots=True)
class PlayerState:
    character_id: int
    hp: int
    max_hp: int
    block: int = 0
    energy: int = 3
    max_energy: int = 3
    powers: list[PowerStack] = field(default_factory=list)
    misc_res: list[float] = field(default_factory=lambda: [0.0, 0.0, 0.0, 0.0])
    orbs: list[OrbSlot] = field(default_factory=list)
    orb_count: int = 0
    stance_id: int = 0
    companions: list[CompanionState] = field(default_factory=list)
    cards_played_this_turn: int = 0
    cards_discarded_this_turn: int = 0
    times_damaged_this_combat: int = 0
    potions_used_this_combat: int = 0


@dataclass(slots=True)
class CombatState:
    turn: int
    phase: int
    player: PlayerState
    monsters: list[MonsterState] = field(default_factory=list)
    hand: list[CardInstanceFeat] = field(default_factory=list)
    draw_pile: list[CardInstanceFeat] = field(default_factory=list)
    draw_bag_counts: list[tuple[int, int]] = field(default_factory=list)
    discard_pile: list[CardInstanceFeat] = field(default_factory=list)
    exhaust_pile: list[CardInstanceFeat] = field(default_factory=list)
    limbo: list[CardInstanceFeat] = field(default_factory=list)
    draw_size: int = 0
    discard_size: int = 0
    exhaust_size: int = 0
    shuffle_pending: bool = False
    next_shuffle_revealed: bool = False
    rng_counters: dict[str, int] = field(default_factory=dict)


@dataclass(slots=True)
class RelicState:
    catalog_id: int
    counter: int = -1
    used_this_turn: bool = False
    used_this_combat: bool = False
    pulse: bool = False


@dataclass(slots=True)
class PotionState:
    slot: int
    catalog_id: int = 0
    can_use: bool = False
    can_discard: bool = False
    requires_target: bool = False


@dataclass(slots=True)
class MapNode:
    x: int
    y: int
    symbol: int
    children: list[tuple[int, int]] = field(default_factory=list)
    parents: list[tuple[int, int]] = field(default_factory=list)
    visited: bool = False
    current: bool = False
    revealed: bool = True


@dataclass(slots=True)
class MapState:
    act: int = 1
    act_variant: int = 0
    boss_id: int = 0
    elites_remaining_guess: int = 0
    nodes: list[MapNode] = field(default_factory=list)
    current_x: int = 0
    current_y: int = 0


@dataclass(slots=True)
class ShopItem:
    slot: int
    kind: int
    catalog_id: int
    price: int
    on_sale: bool = False
    sold: bool = False


@dataclass(slots=True)
class ShopState:
    cards: list[ShopItem] = field(default_factory=list)
    relics: list[ShopItem] = field(default_factory=list)
    potions: list[ShopItem] = field(default_factory=list)
    remove_price: int = 75
    remove_available: bool = True
    purge_count_this_run: int = 0


@dataclass(slots=True)
class SelectionOption:
    index: int
    kind: int
    catalog_id: int
    label_hash: int = 0
    enabled: bool = True
    extra: int = 0


@dataclass(slots=True)
class SelectionState:
    prompt_id: int
    min_picks: int = 1
    max_picks: int = 1
    can_skip: bool = False
    can_cancel: bool = False
    options: list[SelectionOption] = field(default_factory=list)
    already_picked: list[int] = field(default_factory=list)


@dataclass(slots=True)
class RunContext:
    floor: int
    act: int
    ascension: int
    character_id: int
    player_count: int = 1
    deck: list[CardInstanceFeat] = field(default_factory=list)
    relics: list[RelicState] = field(default_factory=list)
    potions: list[PotionState] = field(default_factory=list)
    potion_slots: int = 3
    gold: int = 99
    hp: int = 80
    max_hp: int = 80
    current_hp_frac: float = 1.0
    card_remove_count: int = 0
    rest_sites_visited: int = 0
    shops_visited: int = 0
    elites_killed: int = 0
    bosses_killed: int = 0
    monsters_killed: int = 0
    potions_used_this_run: int = 0
    neow_bonus_id: int = 0
    upcoming_boss_id: int = 0
    map: MapState = field(default_factory=MapState)
    shop: Optional[ShopState] = None
    modifiers: list[int] = field(default_factory=list)


@dataclass(slots=True)
class DebugSidecar:
    seed_string: str
    seed_i32: int
    game_version_string: str = "unknown"


@dataclass(slots=True)
class Observation:
    schema_version: int
    catalog_version: str
    mode: InfoMode
    screen: ScreenType
    in_combat: bool
    run: RunContext
    combat: Optional[CombatState] = None
    selection: Optional[SelectionState] = None
    legal: ActionMask = field(default_factory=ActionMask)
    debug: Optional[DebugSidecar] = None


def empty_observation(*, mode: InfoMode = InfoMode.NOSL) -> Observation:
    return Observation(
        schema_version=SCHEMA_VERSION,
        catalog_version="synthetic-v0",
        mode=mode,
        screen=ScreenType.NONE,
        in_combat=False,
        run=RunContext(floor=1, act=1, ascension=0, character_id=0),
        combat=None,
        legal=ActionMask(),
    )


# re-export for type checkers that import CombatPhase from this module
_ = CombatPhase
