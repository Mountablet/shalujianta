"""JSON codec + SL/NoSL projection.

NoSL output must not contain draw order, rng_counters, or seed_i32.
Those keys are physically omitted, not left as empty values for a net to ignore.
"""

from __future__ import annotations

import copy
import json
from dataclasses import asdict, is_dataclass
from enum import IntEnum
from typing import Any

from shalujianta.protocol.action import Action, ActionMask, TargetRef, flat_to_action
from shalujianta.protocol.cards import CardInstanceFeat
from shalujianta.protocol.constants import FLAT_SIZE, SCHEMA_VERSION
from shalujianta.protocol.enums import InfoMode, ScreenType, Verb
from shalujianta.protocol.observation import (
    CombatState,
    DebugSidecar,
    MapNode,
    MapState,
    MonsterState,
    Observation,
    PlayerState,
    PotionState,
    PowerStack,
    RelicState,
    RunContext,
    SelectionOption,
    SelectionState,
    ShopItem,
    ShopState,
)

NOSL_FORBIDDEN_KEYS = frozenset(
    {
        "rng_counters",
        "seed_i32",
        "seed_string",
        "sl_peek_rewards",
    }
)


def _public_fields(obj: Any) -> dict[str, Any]:
    if is_dataclass(obj):
        return {k: getattr(obj, k) for k in obj.__dataclass_fields__}
    raise TypeError(type(obj))


def to_plain(obj: Any) -> Any:
    if obj is None or isinstance(obj, (bool, int, float, str)):
        return obj
    if isinstance(obj, IntEnum):
        return int(obj)
    if isinstance(obj, tuple):
        return [to_plain(x) for x in obj]
    if isinstance(obj, list):
        return [to_plain(x) for x in obj]
    if isinstance(obj, dict):
        return {str(k): to_plain(v) for k, v in obj.items()}
    if is_dataclass(obj):
        return {k: to_plain(v) for k, v in _public_fields(obj).items()}
    raise TypeError(f"cannot encode {type(obj)}")


def project_observation(obs: Observation, mode: InfoMode) -> Observation:
    """Return a new Observation projected to SL or NoSL."""
    out = copy.deepcopy(obs)
    out.mode = mode
    if mode == InfoMode.SL:
        if out.combat is not None:
            out.combat.next_shuffle_revealed = True
        return out

    out.debug = None
    if out.combat is not None:
        known = [c for c in out.combat.draw_pile if c.known_in_draw]
        known.sort(key=lambda c: c.uuid)
        out.combat.draw_pile = known
        out.combat.next_shuffle_revealed = False
        out.combat.rng_counters = {}
        bag: dict[int, int] = {}
        # bag is already on the object; keep it. If empty, rebuild from remaining unknown.
        if not out.combat.draw_bag_counts:
            for card in obs.combat.draw_pile if obs.combat else []:
                bag[card.catalog_id] = bag.get(card.catalog_id, 0) + 1
            out.combat.draw_bag_counts = sorted(bag.items())
        for monster in out.combat.monsters:
            if not monster.intent_known:
                monster.move_id = -1
    if out.run.map.nodes:
        for node in out.run.map.nodes:
            if not node.revealed:
                node.symbol = 0
    return out


def observation_to_dict(obs: Observation, *, mode: InfoMode | None = None) -> dict[str, Any]:
    projected = project_observation(obs, mode if mode is not None else obs.mode)
    payload = to_plain(projected)
    if projected.mode == InfoMode.NOSL:
        payload.pop("debug", None)
        combat = payload.get("combat")
        if isinstance(combat, dict):
            combat.pop("rng_counters", None)
            # Ordered secret pile is not serialized; only known cards remain.
            if not combat.get("draw_pile"):
                combat["draw_pile"] = []
    return payload


def observation_to_json(obs: Observation, *, mode: InfoMode | None = None) -> str:
    return json.dumps(observation_to_dict(obs, mode=mode), separators=(",", ":"), ensure_ascii=False)


def assert_nosl_json_clean(text: str) -> None:
    data = json.loads(text)
    _assert_nosl_obj(data)


def _assert_nosl_obj(obj: Any, path: str = "$") -> None:
    if isinstance(obj, dict):
        for key in NOSL_FORBIDDEN_KEYS:
            if key in obj:
                raise AssertionError(f"NoSL JSON contains forbidden key {key} at {path}")
        if "debug" in obj and obj["debug"] is not None:
            raise AssertionError(f"NoSL JSON must omit debug sidecar at {path}")
        for key, value in obj.items():
            _assert_nosl_obj(value, f"{path}.{key}")
    elif isinstance(obj, list):
        for i, value in enumerate(obj):
            _assert_nosl_obj(value, f"{path}[{i}]")


def action_to_dict(action: Action) -> dict[str, Any]:
    return {
        "verb": int(action.verb),
        "card_index": action.card_index,
        "card_uuid": action.card_uuid,
        "potion_slot": action.potion_slot,
        "target": {"kind": action.target.kind, "index": action.target.index},
        "option_index": action.option_index,
        "map_x": action.map_x,
        "map_y": action.map_y,
        "extra": action.extra,
    }


def action_from_dict(data: dict[str, Any]) -> Action:
    target = data.get("target") or {}
    return Action(
        verb=Verb(int(data["verb"])),
        card_index=int(data.get("card_index", -1)),
        card_uuid=int(data.get("card_uuid", -1)),
        potion_slot=int(data.get("potion_slot", -1)),
        target=TargetRef(kind=int(target.get("kind", 0)), index=int(target.get("index", -1))),
        option_index=int(data.get("option_index", -1)),
        map_x=int(data.get("map_x", -1)),
        map_y=int(data.get("map_y", -1)),
        extra=int(data.get("extra", 0)),
    )


def action_mask_to_dict(mask: ActionMask) -> dict[str, Any]:
    return {
        "n": mask.n,
        "verbs_allowed": list(mask.verbs_allowed),
        "playable_hand": list(mask.playable_hand),
        "play_targets": [list(row) for row in mask.play_targets],
        "potion_usable": list(mask.potion_usable),
        "potion_targets": [list(row) for row in mask.potion_targets],
        "potion_discardable": list(mask.potion_discardable),
        "choose_options": list(mask.choose_options),
        "map_nodes": list(mask.map_nodes),
        "flat": list(mask.flat),
        "flat_to_action": [action_to_dict(a) for a in mask.flat_to_action],
    }


def action_mask_from_dict(data: dict[str, Any]) -> ActionMask:
    actions = [action_from_dict(x) for x in data.get("flat_to_action", [])]
    if len(actions) != FLAT_SIZE:
        actions = [flat_to_action(i) for i in range(FLAT_SIZE)]
    return ActionMask(
        n=int(data.get("n", FLAT_SIZE)),
        verbs_allowed=list(data.get("verbs_allowed", [False] * 33)),
        playable_hand=list(data.get("playable_hand", [False] * 10)),
        play_targets=list(data.get("play_targets", [[False] * 9 for _ in range(10)])),
        potion_usable=list(data.get("potion_usable", [False] * 5)),
        potion_targets=list(data.get("potion_targets", [[False] * 9 for _ in range(5)])),
        potion_discardable=list(data.get("potion_discardable", [False] * 5)),
        choose_options=list(data.get("choose_options", [False] * 16)),
        map_nodes=list(data.get("map_nodes", [False] * 16)),
        flat=list(data.get("flat", [False] * FLAT_SIZE)),
        flat_to_action=actions,
    )


def observation_from_dict(data: dict[str, Any]) -> Observation:
    """Best-effort decode for tests and traces. Not a full schema validator."""
    run_raw = data.get("run") or {}
    combat_raw = data.get("combat")
    debug_raw = data.get("debug")
    return Observation(
        schema_version=int(data.get("schema_version", SCHEMA_VERSION)),
        catalog_version=str(data.get("catalog_version", "")),
        mode=InfoMode(int(data.get("mode", 0))),
        screen=ScreenType(int(data.get("screen", 0))),
        in_combat=bool(data.get("in_combat", False)),
        run=_run_from_dict(run_raw),
        combat=_combat_from_dict(combat_raw) if combat_raw else None,
        selection=_selection_from_dict(data["selection"]) if data.get("selection") else None,
        legal=action_mask_from_dict(data.get("legal") or {}),
        debug=(
            DebugSidecar(
                seed_string=str(debug_raw.get("seed_string", "")),
                seed_i32=int(debug_raw.get("seed_i32", 0)),
                game_version_string=str(debug_raw.get("game_version_string", "")),
            )
            if debug_raw
            else None
        ),
    )


def _cards(rows: Any) -> list[CardInstanceFeat]:
    out: list[CardInstanceFeat] = []
    for row in rows or []:
        if isinstance(row, CardInstanceFeat):
            out.append(row)
            continue
        out.append(
            CardInstanceFeat(
                catalog_id=int(row["catalog_id"]),
                uuid=int(row["uuid"]),
                upgrade=int(row.get("upgrade", 0)),
                known_in_draw=bool(row.get("known_in_draw", False)),
                current_cost=int(row.get("current_cost", 0)),
                base_cost=int(row.get("base_cost", 0)),
                is_playable=bool(row.get("is_playable", True)),
                has_target=bool(row.get("has_target", False)),
                is_x_cost=bool(row.get("is_x_cost", False)),
            )
        )
    return out


def _run_from_dict(raw: dict[str, Any]) -> RunContext:
    potions = [
        PotionState(
            slot=int(p["slot"]),
            catalog_id=int(p.get("catalog_id", 0)),
            can_use=bool(p.get("can_use", False)),
            can_discard=bool(p.get("can_discard", False)),
            requires_target=bool(p.get("requires_target", False)),
        )
        for p in raw.get("potions") or []
    ]
    relics = [
        RelicState(catalog_id=int(r["catalog_id"]), counter=int(r.get("counter", -1)))
        for r in raw.get("relics") or []
    ]
    map_raw = raw.get("map") or {}
    nodes = [
        MapNode(
            x=int(n["x"]),
            y=int(n["y"]),
            symbol=int(n.get("symbol", 0)),
            revealed=bool(n.get("revealed", True)),
            current=bool(n.get("current", False)),
            visited=bool(n.get("visited", False)),
        )
        for n in map_raw.get("nodes") or []
    ]
    shop = None
    if raw.get("shop"):
        s = raw["shop"]
        shop = ShopState(
            cards=[ShopItem(**item) if not isinstance(item, ShopItem) else item for item in s.get("cards") or []],
            remove_price=int(s.get("remove_price", 75)),
        )
    return RunContext(
        floor=int(raw.get("floor", 1)),
        act=int(raw.get("act", 1)),
        ascension=int(raw.get("ascension", 0)),
        character_id=int(raw.get("character_id", 0)),
        deck=_cards(raw.get("deck")),
        relics=relics,
        potions=potions,
        gold=int(raw.get("gold", 99)),
        hp=int(raw.get("hp", 80)),
        max_hp=int(raw.get("max_hp", 80)),
        map=MapState(
            act=int(map_raw.get("act", 1)),
            act_variant=int(map_raw.get("act_variant", 0)),
            nodes=nodes,
        ),
        shop=shop,
    )


def _combat_from_dict(raw: dict[str, Any]) -> CombatState:
    player_raw = raw.get("player") or {}
    player = PlayerState(
        character_id=int(player_raw.get("character_id", 0)),
        hp=int(player_raw.get("hp", 80)),
        max_hp=int(player_raw.get("max_hp", 80)),
        block=int(player_raw.get("block", 0)),
        energy=int(player_raw.get("energy", 3)),
        max_energy=int(player_raw.get("max_energy", 3)),
        powers=[
            PowerStack(catalog_id=int(p["catalog_id"]), amount=int(p.get("amount", 0)))
            for p in player_raw.get("powers") or []
        ],
    )
    monsters = [
        MonsterState(
            slot=int(m["slot"]),
            catalog_id=int(m["catalog_id"]),
            hp=int(m["hp"]),
            max_hp=int(m["max_hp"]),
            intent_known=bool(m.get("intent_known", True)),
            move_id=int(m.get("move_id", -1)),
            intent_id=int(m.get("intent_id", 0)),
        )
        for m in raw.get("monsters") or []
    ]
    bag = [tuple(x) for x in raw.get("draw_bag_counts") or []]
    return CombatState(
        turn=int(raw.get("turn", 1)),
        phase=int(raw.get("phase", 0)),
        player=player,
        monsters=monsters,
        hand=_cards(raw.get("hand")),
        draw_pile=_cards(raw.get("draw_pile")),
        draw_bag_counts=bag,  # type: ignore[arg-type]
        discard_pile=_cards(raw.get("discard_pile")),
        exhaust_pile=_cards(raw.get("exhaust_pile")),
        draw_size=int(raw.get("draw_size", 0)),
        next_shuffle_revealed=bool(raw.get("next_shuffle_revealed", False)),
        rng_counters=dict(raw["rng_counters"]) if "rng_counters" in raw else {},
    )


def _selection_from_dict(raw: dict[str, Any]) -> SelectionState:
    return SelectionState(
        prompt_id=int(raw.get("prompt_id", 0)),
        options=[
            SelectionOption(
                index=int(o["index"]),
                kind=int(o.get("kind", 0)),
                catalog_id=int(o.get("catalog_id", 0)),
            )
            for o in raw.get("options") or []
        ],
    )


# asdict imported for potential debug dumps
_ = asdict
