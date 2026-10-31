from __future__ import annotations

from dataclasses import dataclass, field

from shalujianta.protocol.constants import (
    BOSS_RELIC_BASE,
    CARD_REWARD_BASE,
    CHOOSE_BASE,
    DISCARD_POTION_BASE,
    END_TURN_SLOT,
    EVENT_BASE,
    FLAT_SIZE,
    MAP_SELECT_BASE,
    MAX_HAND,
    MAX_POTIONS,
    MAX_TARGETS,
    MAX_VERBS,
    PLAY_CARD_BASE,
    PROCEED_SLOT,
    REST_BASE,
    SHOP_BASE,
    SHOP_BUY_COUNT,
    SHOP_LEAVE_SLOT,
    SHOP_REMOVE_SLOT,
    SKIP_SLOT,
    TARGET_ALL_ENEMIES,
    TARGET_COMPANION,
    TARGET_MONSTER,
    TARGET_NONE,
    TARGET_PLAYER,
    TARGET_RANDOM_ENEMY,
    USE_POTION_BASE,
)
from shalujianta.protocol.enums import ScreenType, Verb


@dataclass(slots=True)
class TargetRef:
    kind: int = TARGET_NONE
    index: int = -1


@dataclass(slots=True)
class Action:
    verb: Verb = Verb.NOOP
    card_index: int = -1
    card_uuid: int = -1
    potion_slot: int = -1
    target: TargetRef = field(default_factory=TargetRef)
    option_index: int = -1
    map_x: int = -1
    map_y: int = -1
    extra: int = 0


@dataclass(slots=True)
class ActionMask:
    n: int = FLAT_SIZE
    verbs_allowed: list[bool] = field(default_factory=lambda: [False] * MAX_VERBS)
    playable_hand: list[bool] = field(default_factory=lambda: [False] * MAX_HAND)
    play_targets: list[list[bool]] = field(
        default_factory=lambda: [[False] * MAX_TARGETS for _ in range(MAX_HAND)]
    )
    potion_usable: list[bool] = field(default_factory=lambda: [False] * MAX_POTIONS)
    potion_targets: list[list[bool]] = field(
        default_factory=lambda: [[False] * MAX_TARGETS for _ in range(MAX_POTIONS)]
    )
    potion_discardable: list[bool] = field(default_factory=lambda: [False] * MAX_POTIONS)
    choose_options: list[bool] = field(default_factory=lambda: [False] * 16)
    map_nodes: list[bool] = field(default_factory=lambda: [False] * 16)
    flat: list[bool] = field(default_factory=lambda: [False] * FLAT_SIZE)
    flat_to_action: list[Action] = field(default_factory=lambda: [Action() for _ in range(FLAT_SIZE)])


def target_to_slot(target: TargetRef) -> int:
    if target.kind == TARGET_NONE:
        return 0
    if target.kind == TARGET_MONSTER:
        if not 0 <= target.index < 6:
            raise ValueError(f"monster index out of range: {target.index}")
        return 1 + target.index
    if target.kind == TARGET_PLAYER:
        return 7
    if target.kind in (TARGET_ALL_ENEMIES, TARGET_COMPANION, TARGET_RANDOM_ENEMY):
        return 8
    raise ValueError(f"unknown target kind: {target.kind}")


def slot_to_target(slot: int, *, companion: bool = False) -> TargetRef:
    if slot == 0:
        return TargetRef(TARGET_NONE, -1)
    if 1 <= slot <= 6:
        return TargetRef(TARGET_MONSTER, slot - 1)
    if slot == 7:
        return TargetRef(TARGET_PLAYER, -1)
    if slot == 8:
        kind = TARGET_COMPANION if companion else TARGET_ALL_ENEMIES
        return TargetRef(kind, 0 if companion else -1)
    raise ValueError(f"target slot out of range: {slot}")


def action_to_flat(action: Action) -> int:
    verb = Verb(action.verb)
    if verb == Verb.PLAY_CARD:
        if not 0 <= action.card_index < MAX_HAND:
            raise ValueError("PLAY_CARD requires card_index 0..9")
        return PLAY_CARD_BASE + action.card_index * MAX_TARGETS + target_to_slot(action.target)
    if verb == Verb.USE_POTION:
        if not 0 <= action.potion_slot < MAX_POTIONS:
            raise ValueError("USE_POTION requires potion_slot 0..4")
        return USE_POTION_BASE + action.potion_slot * MAX_TARGETS + target_to_slot(action.target)
    if verb == Verb.DISCARD_POTION:
        if not 0 <= action.potion_slot < MAX_POTIONS:
            raise ValueError("DISCARD_POTION requires potion_slot 0..4")
        return DISCARD_POTION_BASE + action.potion_slot
    if verb == Verb.END_TURN:
        return END_TURN_SLOT
    if verb == Verb.MAP_SELECT:
        if not 0 <= action.option_index < 16:
            raise ValueError("MAP_SELECT uses option_index as edge 0..15")
        return MAP_SELECT_BASE + action.option_index
    if verb == Verb.CARD_REWARD_PICK:
        if not 0 <= action.option_index <= 2:
            raise ValueError("CARD_REWARD_PICK option_index 0..2")
        return CARD_REWARD_BASE + action.option_index
    if verb == Verb.CARD_REWARD_SKIP:
        return CARD_REWARD_BASE + 3
    if verb == Verb.REST_CHOOSE:
        if not 0 <= action.option_index < 6:
            raise ValueError("REST_CHOOSE option_index 0..5")
        return REST_BASE + action.option_index
    if verb == Verb.SHOP_BUY:
        if not 0 <= action.option_index < SHOP_BUY_COUNT:
            raise ValueError(f"SHOP_BUY option_index 0..{SHOP_BUY_COUNT - 1}")
        return SHOP_BASE + action.option_index
    if verb == Verb.SHOP_REMOVE:
        return SHOP_REMOVE_SLOT
    if verb == Verb.SHOP_LEAVE:
        return SHOP_LEAVE_SLOT
    if verb in (Verb.EVENT_OPTION, Verb.NEOW_PICK, Verb.ANCIENT_PICK):
        if not 0 <= action.option_index < 8:
            raise ValueError("event-like option_index 0..7")
        return EVENT_BASE + action.option_index
    if verb == Verb.BOSS_RELIC_PICK:
        if not 0 <= action.option_index <= 2:
            raise ValueError("BOSS_RELIC_PICK option_index 0..2")
        return BOSS_RELIC_BASE + action.option_index
    if verb == Verb.BOSS_RELIC_SKIP:
        return BOSS_RELIC_BASE + 3
    if verb == Verb.CHOOSE:
        if not 0 <= action.option_index < 16:
            raise ValueError("CHOOSE option_index 0..15")
        return CHOOSE_BASE + action.option_index
    if verb == Verb.PROCEED:
        return PROCEED_SLOT
    if verb == Verb.SKIP:
        return SKIP_SLOT
    raise ValueError(f"verb {verb.name} is not in the 256-slot table")


# Slots 183-190 are shared by EVENT_OPTION / NEOW_PICK / ANCIENT_PICK; the
# three verbs never coexist on one screen, so decode needs the screen context.
_EVENT_SCREEN_VERB = {
    int(ScreenType.EVENT): Verb.EVENT_OPTION,
    int(ScreenType.NEOW): Verb.NEOW_PICK,
    int(ScreenType.ANCIENT): Verb.ANCIENT_PICK,
}


def flat_to_action(
    slot: int,
    *,
    companion_slot8: bool = False,
    event_screen: int = ScreenType.EVENT,
) -> Action:
    if not 0 <= slot < FLAT_SIZE:
        raise ValueError(f"flat slot out of range: {slot}")
    if slot < USE_POTION_BASE:
        card_index = (slot - PLAY_CARD_BASE) // MAX_TARGETS
        tslot = (slot - PLAY_CARD_BASE) % MAX_TARGETS
        return Action(
            verb=Verb.PLAY_CARD,
            card_index=card_index,
            target=slot_to_target(tslot, companion=companion_slot8),
        )
    if slot < DISCARD_POTION_BASE:
        rel = slot - USE_POTION_BASE
        potion_slot = rel // MAX_TARGETS
        tslot = rel % MAX_TARGETS
        return Action(
            verb=Verb.USE_POTION,
            potion_slot=potion_slot,
            target=slot_to_target(tslot, companion=companion_slot8),
        )
    if slot < END_TURN_SLOT:
        return Action(verb=Verb.DISCARD_POTION, potion_slot=slot - DISCARD_POTION_BASE)
    if slot == END_TURN_SLOT:
        return Action(verb=Verb.END_TURN)
    if slot < CARD_REWARD_BASE:
        return Action(verb=Verb.MAP_SELECT, option_index=slot - MAP_SELECT_BASE)
    if slot < REST_BASE:
        rel = slot - CARD_REWARD_BASE
        if rel == 3:
            return Action(verb=Verb.CARD_REWARD_SKIP)
        return Action(verb=Verb.CARD_REWARD_PICK, option_index=rel)
    if slot < SHOP_BASE:
        return Action(verb=Verb.REST_CHOOSE, option_index=slot - REST_BASE)
    if slot == SHOP_REMOVE_SLOT:
        return Action(verb=Verb.SHOP_REMOVE)
    if slot == SHOP_LEAVE_SLOT:
        return Action(verb=Verb.SHOP_LEAVE)
    if slot < EVENT_BASE:
        return Action(verb=Verb.SHOP_BUY, option_index=slot - SHOP_BASE)
    if slot < BOSS_RELIC_BASE:
        verb = _EVENT_SCREEN_VERB.get(event_screen, Verb.EVENT_OPTION)
        return Action(verb=verb, option_index=slot - EVENT_BASE)
    if slot < CHOOSE_BASE:
        rel = slot - BOSS_RELIC_BASE
        if rel == 3:
            return Action(verb=Verb.BOSS_RELIC_SKIP)
        return Action(verb=Verb.BOSS_RELIC_PICK, option_index=rel)
    if slot < PROCEED_SLOT:
        return Action(verb=Verb.CHOOSE, option_index=slot - CHOOSE_BASE)
    if slot == PROCEED_SLOT:
        return Action(verb=Verb.PROCEED)
    if slot == SKIP_SLOT:
        return Action(verb=Verb.SKIP)
    return Action(verb=Verb.NOOP)


def populate_flat_table(mask: ActionMask) -> None:
    """Fill flat[] and flat_to_action[] from the structured combat/meta bits."""
    mask.flat = [False] * FLAT_SIZE
    mask.flat_to_action = [Action() for _ in range(FLAT_SIZE)]
    for i in range(MAX_HAND):
        for t in range(MAX_TARGETS):
            slot = PLAY_CARD_BASE + i * MAX_TARGETS + t
            mask.flat_to_action[slot] = flat_to_action(slot)
            mask.flat[slot] = bool(mask.playable_hand[i] and mask.play_targets[i][t])
    for s in range(MAX_POTIONS):
        for t in range(MAX_TARGETS):
            slot = USE_POTION_BASE + s * MAX_TARGETS + t
            mask.flat_to_action[slot] = flat_to_action(slot)
            mask.flat[slot] = bool(mask.potion_usable[s] and mask.potion_targets[s][t])
        dslot = DISCARD_POTION_BASE + s
        mask.flat_to_action[dslot] = flat_to_action(dslot)
        mask.flat[dslot] = bool(mask.potion_discardable[s])
    mask.flat_to_action[END_TURN_SLOT] = Action(verb=Verb.END_TURN)
    mask.flat[END_TURN_SLOT] = bool(mask.verbs_allowed[int(Verb.END_TURN)])
    for i, allowed in enumerate(mask.map_nodes):
        slot = MAP_SELECT_BASE + i
        mask.flat_to_action[slot] = flat_to_action(slot)
        mask.flat[slot] = bool(allowed)
    for i, allowed in enumerate(mask.choose_options):
        slot = CHOOSE_BASE + i
        if slot >= FLAT_SIZE:
            break
        mask.flat_to_action[slot] = flat_to_action(slot)
        mask.flat[slot] = bool(allowed)
    if mask.verbs_allowed[int(Verb.PROCEED)]:
        mask.flat[PROCEED_SLOT] = True
        mask.flat_to_action[PROCEED_SLOT] = Action(verb=Verb.PROCEED)
    if mask.verbs_allowed[int(Verb.SKIP)]:
        mask.flat[SKIP_SLOT] = True
        mask.flat_to_action[SKIP_SLOT] = Action(verb=Verb.SKIP)
