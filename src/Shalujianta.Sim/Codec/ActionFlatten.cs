namespace Shalujianta.Sim.Codec;

/// <summary>
/// 256-slot flattening table. Numbers must match python/shalujianta/protocol/constants.py.
/// </summary>
public static class ActionFlatten
{
    public const int Size = 256;
    public const int PlayCardBase = 0;
    public const int PlayCardCount = 90;
    public const int UsePotionBase = 90;
    public const int UsePotionCount = 45;
    public const int DiscardPotionBase = 135;
    public const int DiscardPotionCount = 5;
    public const int EndTurnSlot = 140;
    public const int CombatFlatEnd = 141;
    public const int MapSelectBase = 141;
    public const int CardRewardBase = 157;
    public const int RestBase = 161;
    public const int ShopBase = 167;
    public const int EventBase = 183;
    public const int BossRelicBase = 191;
    public const int ChooseBase = 195;
    public const int ProceedSlot = 211;
    public const int SkipSlot = 212;
    public const int MaxHand = 10;
    public const int MaxTargets = 9;
    public const int MaxPotions = 5;

    public static int PlayCardSlot(int cardIndex, int targetSlot)
    {
        if (cardIndex is < 0 or >= MaxHand) throw new ArgumentOutOfRangeException(nameof(cardIndex));
        if (targetSlot is < 0 or >= MaxTargets) throw new ArgumentOutOfRangeException(nameof(targetSlot));
        return PlayCardBase + cardIndex * MaxTargets + targetSlot;
    }

    public static int UsePotionSlot(int potionSlot, int targetSlot)
    {
        if (potionSlot is < 0 or >= MaxPotions) throw new ArgumentOutOfRangeException(nameof(potionSlot));
        if (targetSlot is < 0 or >= MaxTargets) throw new ArgumentOutOfRangeException(nameof(targetSlot));
        return UsePotionBase + potionSlot * MaxTargets + targetSlot;
    }
}
