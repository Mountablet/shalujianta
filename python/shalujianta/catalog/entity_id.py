"""32-bit EntityId: namespace in the high 8 bits, local_id in the low 24."""

from __future__ import annotations

from enum import IntEnum

NS_SHIFT = 24
LOCAL_MASK = 0x00FFFFFF
RESERVED_CAPACITIES: dict[str, int] = {
    "UNK": 8,
    "CARD": 8192,
    "RELIC": 2048,
    "POTION": 512,
    "POWER": 2048,
    "ORB": 64,
    "STANCE": 32,
    "MONSTER": 512,
    "INTENT": 64,
    "EVENT": 256,
    "MAP_NODE": 32,
    "STATUS_CARD": 64,
    "KEYWORD": 256,
    "ENCHANTMENT": 512,
    "AFFLICTION": 128,
    "ANCIENT": 16,
    "COMPANION": 16,
    "ENCOUNTER": 512,
    "SCREEN": 64,
    "MODIFIER": 64,
    "GENERATED": 65536,
}


class EntityNamespace(IntEnum):
    UNK = 0
    CARD = 1
    RELIC = 2
    POTION = 3
    POWER = 4
    ORB = 5
    STANCE = 6
    MONSTER = 7
    INTENT = 8
    EVENT = 9
    MAP_NODE = 10
    STATUS_CARD = 11  # unused: Status/Curse live in CARD
    KEYWORD = 12
    ENCHANTMENT = 13
    AFFLICTION = 14
    ANCIENT = 15
    COMPANION = 16
    ENCOUNTER = 17
    SCREEN = 18
    MODIFIER = 19
    GENERATED = 20


class EntityId(int):
    """Packed 32-bit id. local_id 0 is UNK in every namespace."""

    @staticmethod
    def pack(namespace: int | EntityNamespace, local_id: int) -> EntityId:
        ns = int(namespace)
        if not 0 <= ns <= 255:
            raise ValueError(f"namespace out of range: {ns}")
        if not 0 <= local_id <= LOCAL_MASK:
            raise ValueError(f"local_id out of range: {local_id}")
        return EntityId((ns << NS_SHIFT) | local_id)

    @property
    def namespace(self) -> int:
        return int(self) >> NS_SHIFT

    @property
    def local_id(self) -> int:
        return int(self) & LOCAL_MASK

    def unpack(self) -> tuple[int, int]:
        return self.namespace, self.local_id


def capacity(namespace: str | EntityNamespace) -> int:
    name = namespace.name if isinstance(namespace, EntityNamespace) else namespace
    if name not in RESERVED_CAPACITIES:
        raise KeyError(name)
    return RESERVED_CAPACITIES[name]
