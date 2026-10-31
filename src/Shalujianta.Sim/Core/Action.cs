namespace Shalujianta.Sim.Core;

/// <summary>
/// Verb set mirrors python/shalujianta/protocol/enums.py Verb (0..32) one-to-one.
/// </summary>
public enum Verb : byte
{
    Noop = 0,
    PlayCard = 1,
    EndTurn = 2,
    UsePotion = 3,
    DiscardPotion = 4,
    Choose = 5,
    Proceed = 6,
    Skip = 7,
    Cancel = 8,
    MapSelect = 9,
    RestChoose = 10,
    ShopBuy = 11,
    ShopRemove = 12,
    ShopLeave = 13,
    EventOption = 14,
    CardRewardPick = 15,
    CardRewardSkip = 16,
    BossRelicPick = 17,
    BossRelicSkip = 18,
    CardRemovePick = 19,
    CardUpgradePick = 20,
    TransformPick = 21,
    DuplicatePick = 22,
    ChestOpen = 23,
    ChestSkip = 24,
    NeowPick = 25,
    AncientPick = 26,
    EnchantPick = 27,
    HandSelectToggle = 28,
    HandSelectConfirm = 29,
    Wait = 30,
    State = 31,
    Resign = 32,
}

/// <summary>Target kind mirrors python target_to_slot encoding (§4.9).</summary>
public enum TargetKind : byte
{
    None = 0,
    Monster = 1,
    Player = 2,
    AllEnemies = 3,
    RandomEnemy = 4,
    Companion = 5,
}

public readonly record struct TargetRef(TargetKind Kind = TargetKind.None, int Index = -1);

/// <summary>
/// One legal primitive. Field-for-field mirror of python Action; card_uuid is the
/// identity the network must point at, card_index is only a locator (§3.8 lesson).
/// </summary>
public readonly record struct Action(
    Verb Verb = Verb.Noop,
    int CardIndex = -1,
    int CardUuid = -1,
    int PotionSlot = -1,
    TargetRef Target = default,
    int OptionIndex = -1,
    int MapX = -1,
    int MapY = -1,
    int Extra = 0);
