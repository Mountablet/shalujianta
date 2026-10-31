namespace Shalujianta.Sim.Codec;

/// <summary>Mirrors python/shalujianta/protocol/enums.py ScreenType one-to-one.</summary>
public enum ScreenType : byte
{
    None = 0,
    MainMenu = 1,
    Combat = 2,
    HandSelect = 3,
    CardSelectGrid = 4,
    TargetSelect = 5,
    Map = 6,
    Rest = 7,
    Shop = 8,
    Event = 9,
    CardReward = 10,
    Chest = 11,
    BossRelic = 12,
    CardRemove = 13,
    CardUpgrade = 14,
    CardTransform = 15,
    CardDuplicate = 16,
    Neow = 17,
    Ancient = 18,
    Enchant = 19,
    GameOver = 20,
    Victory = 21,
    PotionReward = 22,
    RelicReward = 23,
    CombatReward = 24,
    Unknown = 63,
}
