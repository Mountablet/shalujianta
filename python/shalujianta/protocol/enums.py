from __future__ import annotations

from enum import IntEnum


class ScreenType(IntEnum):
    NONE = 0
    MAIN_MENU = 1
    COMBAT = 2
    HAND_SELECT = 3
    CARD_SELECT_GRID = 4
    TARGET_SELECT = 5
    MAP = 6
    REST = 7
    SHOP = 8
    EVENT = 9
    CARD_REWARD = 10
    CHEST = 11
    BOSS_RELIC = 12
    CARD_REMOVE = 13
    CARD_UPGRADE = 14
    CARD_TRANSFORM = 15
    CARD_DUPLICATE = 16
    NEOW = 17
    ANCIENT = 18
    ENCHANT = 19
    GAME_OVER = 20
    VICTORY = 21
    POTION_REWARD = 22
    RELIC_REWARD = 23
    COMBAT_REWARD = 24
    UNKNOWN = 63


class InfoMode(IntEnum):
    NOSL = 0
    SL = 1


class Verb(IntEnum):
    NOOP = 0
    PLAY_CARD = 1
    END_TURN = 2
    USE_POTION = 3
    DISCARD_POTION = 4
    CHOOSE = 5
    PROCEED = 6
    SKIP = 7
    CANCEL = 8
    MAP_SELECT = 9
    REST_CHOOSE = 10
    SHOP_BUY = 11
    SHOP_REMOVE = 12
    SHOP_LEAVE = 13
    EVENT_OPTION = 14
    CARD_REWARD_PICK = 15
    CARD_REWARD_SKIP = 16
    BOSS_RELIC_PICK = 17
    BOSS_RELIC_SKIP = 18
    CARD_REMOVE_PICK = 19
    CARD_UPGRADE_PICK = 20
    TRANSFORM_PICK = 21
    DUPLICATE_PICK = 22
    CHEST_OPEN = 23
    CHEST_SKIP = 24
    NEOW_PICK = 25
    ANCIENT_PICK = 26
    ENCHANT_PICK = 27
    HAND_SELECT_TOGGLE = 28
    HAND_SELECT_CONFIRM = 29
    WAIT = 30
    STATE = 31
    RESIGN = 32


class CombatPhase(IntEnum):
    PLAYER = 0
    ENEMY = 1
    EXEC = 2
