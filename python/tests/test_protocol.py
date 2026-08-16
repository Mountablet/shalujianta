from __future__ import annotations

import json
import re
from pathlib import Path

import pytest

from shalujianta.protocol.action import (
    Action,
    ActionMask,
    TargetRef,
    action_to_flat,
    flat_to_action,
    populate_flat_table,
    target_to_slot,
)
from shalujianta.protocol.cards import CardInstanceFeat
from shalujianta.protocol.codec import (
    action_from_dict,
    action_to_dict,
    assert_nosl_json_clean,
    observation_from_dict,
    observation_to_dict,
    observation_to_json,
    project_observation,
)
from shalujianta.protocol.constants import (
    COMBAT_FLAT_END,
    DISCARD_POTION_BASE,
    END_TURN_SLOT,
    FLAT_SIZE,
    PLAY_CARD_BASE,
    PLAY_CARD_COUNT,
    SCHEMA_VERSION,
    TARGET_ALL_ENEMIES,
    TARGET_MONSTER,
    TARGET_NONE,
    TARGET_PLAYER,
    USE_POTION_BASE,
    USE_POTION_COUNT,
)
from shalujianta.protocol.enums import InfoMode, ScreenType, Verb
from shalujianta.protocol.observation import (
    CombatState,
    DebugSidecar,
    MapNode,
    MapState,
    MonsterState,
    Observation,
    PlayerState,
    RunContext,
)

ROOT = Path(__file__).resolve().parents[2]
CS_FLAT = ROOT / "src" / "Shalujianta.Sim" / "Codec" / "ActionFlatten.cs"


def _card(catalog_id: int, uuid: int, **kwargs) -> CardInstanceFeat:
    return CardInstanceFeat(catalog_id=catalog_id, uuid=uuid, **kwargs)


def _leaky_sl_obs() -> Observation:
    secret = [
        _card(16777220, 11, known_in_draw=False),
        _card(16777218, 12, known_in_draw=False),
        _card(16777219, 13, known_in_draw=True),
    ]
    return Observation(
        schema_version=SCHEMA_VERSION,
        catalog_version="synthetic-v0",
        mode=InfoMode.SL,
        screen=ScreenType.COMBAT,
        in_combat=True,
        run=RunContext(
            floor=1,
            act=1,
            ascension=0,
            character_id=0,
            map=MapState(
                nodes=[
                    MapNode(x=0, y=1, symbol=7, revealed=False),
                    MapNode(x=0, y=0, symbol=1, revealed=True, current=True),
                ]
            ),
        ),
        combat=CombatState(
            turn=1,
            phase=0,
            player=PlayerState(character_id=0, hp=80, max_hp=80, energy=3),
            monsters=[
                MonsterState(
                    slot=0,
                    catalog_id=100,
                    hp=12,
                    max_hp=13,
                    intent_known=False,
                    move_id=4,
                )
            ],
            hand=[_card(16777220, 1, has_target=True)],
            draw_pile=list(secret),
            draw_size=3,
            rng_counters={"Shuffle": 3, "MonsterAi": 1},
            next_shuffle_revealed=True,
        ),
        debug=DebugSidecar(seed_string="TESTSEED", seed_i32=42, game_version_string="synthetic"),
    )


def test_play_card_flattening_table() -> None:
    assert PLAY_CARD_COUNT == 10 * 9
    assert USE_POTION_COUNT == 5 * 9
    assert COMBAT_FLAT_END == 141
    action = Action(
        verb=Verb.PLAY_CARD,
        card_index=2,
        card_uuid=99,
        target=TargetRef(TARGET_MONSTER, 1),
    )
    slot = action_to_flat(action)
    assert slot == PLAY_CARD_BASE + 2 * 9 + 2
    back = flat_to_action(slot)
    assert back.verb == Verb.PLAY_CARD
    assert back.card_index == 2
    assert back.target.kind == TARGET_MONSTER
    assert back.target.index == 1


@pytest.mark.parametrize(
    "kind,index,slot",
    [
        (TARGET_NONE, -1, 0),
        (TARGET_MONSTER, 0, 1),
        (TARGET_MONSTER, 5, 6),
        (TARGET_PLAYER, -1, 7),
        (TARGET_ALL_ENEMIES, -1, 8),
    ],
)
def test_nine_target_slots(kind: int, index: int, slot: int) -> None:
    assert target_to_slot(TargetRef(kind, index)) == slot


def test_potion_and_end_turn_slots() -> None:
    use = Action(verb=Verb.USE_POTION, potion_slot=1, target=TargetRef(TARGET_MONSTER, 0))
    assert action_to_flat(use) == USE_POTION_BASE + 1 * 9 + 1
    discard = Action(verb=Verb.DISCARD_POTION, potion_slot=4)
    assert action_to_flat(discard) == DISCARD_POTION_BASE + 4
    assert action_to_flat(Action(verb=Verb.END_TURN)) == END_TURN_SLOT


def test_256_slot_round_trip() -> None:
    for slot in range(FLAT_SIZE):
        action = flat_to_action(slot)
        if action.verb == Verb.NOOP:
            continue
        assert action_to_flat(action) == slot
        encoded = action_to_dict(action)
        assert action_from_dict(encoded).verb == action.verb


def test_action_mask_flat_length() -> None:
    mask = ActionMask()
    mask.playable_hand[0] = True
    mask.play_targets[0][0] = True
    mask.verbs_allowed[int(Verb.END_TURN)] = True
    populate_flat_table(mask)
    assert len(mask.flat) == 256
    assert len(mask.flat_to_action) == 256
    assert mask.flat[0] is True
    assert mask.flat[END_TURN_SLOT] is True
    assert mask.flat[1] is False


def test_nosl_json_omits_secrets() -> None:
    sl = _leaky_sl_obs()
    sl_text = observation_to_json(sl, mode=InfoMode.SL)
    sl_data = json.loads(sl_text)
    assert "rng_counters" in sl_data["combat"]
    assert sl_data["debug"]["seed_i32"] == 42
    assert [c["uuid"] for c in sl_data["combat"]["draw_pile"]] == [11, 12, 13]

    nosl_text = observation_to_json(sl, mode=InfoMode.NOSL)
    assert_nosl_json_clean(nosl_text)
    nosl = json.loads(nosl_text)
    assert "rng_counters" not in json.dumps(nosl)
    assert "seed_i32" not in nosl_text
    assert "seed_string" not in nosl_text
    assert nosl.get("debug") is None
    # secret order is gone; only the already-seen card remains
    assert [c["uuid"] for c in nosl["combat"]["draw_pile"]] == [13]
    assert nosl["combat"]["next_shuffle_revealed"] is False
    assert nosl["combat"]["monsters"][0]["move_id"] == -1
    assert nosl["mode"] == int(InfoMode.NOSL)


def test_project_nosl_does_not_mutate_original() -> None:
    sl = _leaky_sl_obs()
    project_observation(sl, InfoMode.NOSL)
    assert sl.mode == InfoMode.SL
    assert sl.combat is not None
    assert sl.combat.rng_counters["Shuffle"] == 3
    assert sl.debug is not None


def test_observation_dict_round_trip_public_fields() -> None:
    sl = _leaky_sl_obs()
    data = observation_to_dict(sl, mode=InfoMode.NOSL)
    back = observation_from_dict(data)
    assert back.mode == InfoMode.NOSL
    assert back.combat is not None
    assert back.combat.hand[0].catalog_id == 16777220
    assert "rng_counters" not in data["combat"]


def test_json_schemas_exist_and_mention_mode() -> None:
    obs_schema = json.loads((ROOT / "proto" / "observation.schema.json").read_text(encoding="utf-8"))
    act_schema = json.loads((ROOT / "proto" / "action.schema.json").read_text(encoding="utf-8"))
    assert obs_schema["title"] == "ObservationV1"
    assert "mode" in obs_schema["properties"]
    assert act_schema["title"] == "ActionV1"
    assert "verb" in act_schema["properties"]


def test_csharp_flatten_constants_match_python() -> None:
    text = CS_FLAT.read_text(encoding="utf-8")

    def const(name: str) -> int:
        match = re.search(rf"public const int {name} = (\d+);", text)
        assert match, name
        return int(match.group(1))

    assert const("Size") == FLAT_SIZE
    assert const("PlayCardCount") == PLAY_CARD_COUNT
    assert const("UsePotionCount") == USE_POTION_COUNT
    assert const("EndTurnSlot") == END_TURN_SLOT
    assert const("CombatFlatEnd") == COMBAT_FLAT_END
