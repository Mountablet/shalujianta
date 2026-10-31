using Shalujianta.Sim.Core;

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
    // Shop region is serialized (§4.9): BUY items 0..13, then REMOVE and LEAVE.
    public const int ShopBuyCount = 14;
    public const int ShopRemoveSlot = 181;
    public const int ShopLeaveSlot = 182;
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

    /// <summary>
    /// Inverse of the 256-slot table; mirrors python flat_to_action including the
    /// serialized shop region and the event-screen context for slots 183-190.
    /// </summary>
    public static Action DecodeSlot(int slot, ScreenType eventScreen = ScreenType.Event)
    {
        if (slot is < 0 or >= Size) throw new ArgumentOutOfRangeException(nameof(slot));
        if (slot < UsePotionBase)
        {
            int cardIndex = slot / MaxTargets;
            return new Action(Verb.PlayCard, cardIndex, -1, -1, DecodeTarget(slot % MaxTargets));
        }

        if (slot < DiscardPotionBase)
        {
            int rel = slot - UsePotionBase;
            return new Action(Verb.UsePotion, -1, -1, rel / MaxTargets, DecodeTarget(rel % MaxTargets));
        }

        if (slot < EndTurnSlot)
        {
            return new Action(Verb.DiscardPotion, -1, -1, slot - DiscardPotionBase);
        }

        if (slot == EndTurnSlot)
        {
            return new Action(Verb.EndTurn);
        }

        if (slot < CardRewardBase)
        {
            return new Action(Verb.MapSelect, OptionIndex: slot - MapSelectBase);
        }

        if (slot < RestBase)
        {
            int rel = slot - CardRewardBase;
            return rel == 3
                ? new Action(Verb.CardRewardSkip)
                : new Action(Verb.CardRewardPick, OptionIndex: rel);
        }

        if (slot < ShopBase)
        {
            return new Action(Verb.RestChoose, OptionIndex: slot - RestBase);
        }

        if (slot == ShopRemoveSlot)
        {
            return new Action(Verb.ShopRemove);
        }

        if (slot == ShopLeaveSlot)
        {
            return new Action(Verb.ShopLeave);
        }

        if (slot < EventBase)
        {
            return new Action(Verb.ShopBuy, OptionIndex: slot - ShopBase);
        }

        if (slot < BossRelicBase)
        {
            var verb = eventScreen switch
            {
                ScreenType.Neow => Verb.NeowPick,
                ScreenType.Ancient => Verb.AncientPick,
                _ => Verb.EventOption,
            };
            return new Action(verb, OptionIndex: slot - EventBase);
        }

        if (slot < ChooseBase)
        {
            int rel = slot - BossRelicBase;
            return rel == 3
                ? new Action(Verb.BossRelicSkip)
                : new Action(Verb.BossRelicPick, OptionIndex: rel);
        }

        if (slot < ProceedSlot)
        {
            return new Action(Verb.Choose, OptionIndex: slot - ChooseBase);
        }

        if (slot == ProceedSlot)
        {
            return new Action(Verb.Proceed);
        }

        if (slot == SkipSlot)
        {
            return new Action(Verb.Skip);
        }

        return new Action(Verb.Noop);
    }

    private static TargetRef DecodeTarget(int targetSlot) => targetSlot switch
    {
        0 => new TargetRef(TargetKind.None, -1),
        >= 1 and <= 6 => new TargetRef(TargetKind.Monster, targetSlot - 1),
        7 => new TargetRef(TargetKind.Player, -1),
        8 => new TargetRef(TargetKind.AllEnemies, -1),
        _ => new TargetRef(TargetKind.None, -1),
    };
}
