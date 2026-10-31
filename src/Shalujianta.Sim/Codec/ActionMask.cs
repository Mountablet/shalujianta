using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Codec;

/// <summary>
/// Structured + flat legality (§4.9). Mirrors python ActionMask; Sim.LegalActions is
/// the only legality source. PopulateFlat must match python populate_flat_table.
/// </summary>
public sealed class ActionMask
{
    public const int Size = ActionFlatten.Size;

    public bool[] VerbsAllowed { get; } = new bool[33];
    public bool[] PlayableHand { get; } = new bool[ActionFlatten.MaxHand];
    public bool[][] PlayTargets { get; } = MakeMatrix(ActionFlatten.MaxHand, ActionFlatten.MaxTargets);
    public bool[] PotionUsable { get; } = new bool[ActionFlatten.MaxPotions];
    public bool[][] PotionTargets { get; } = MakeMatrix(ActionFlatten.MaxPotions, ActionFlatten.MaxTargets);
    public bool[] PotionDiscardable { get; } = new bool[ActionFlatten.MaxPotions];
    public bool[] ChooseOptions { get; } = new bool[16];
    public bool[] MapNodes { get; } = new bool[16];
    public bool[] Flat { get; } = new bool[Size];
    public Action[] FlatToAction { get; } = new Action[Size];

    private static bool[][] MakeMatrix(int rows, int cols)
    {
        var matrix = new bool[rows][];
        for (int i = 0; i < rows; i++)
        {
            matrix[i] = new bool[cols];
        }

        return matrix;
    }

    /// <summary>Fills FlatToAction for every slot and projects the structured bits onto Flat.</summary>
    public void PopulateFlat()
    {
        Array.Clear(Flat);
        for (int slot = 0; slot < Size; slot++)
        {
            FlatToAction[slot] = ActionFlatten.DecodeSlot(slot);
        }

        for (int i = 0; i < ActionFlatten.MaxHand; i++)
        {
            for (int t = 0; t < ActionFlatten.MaxTargets; t++)
            {
                Flat[ActionFlatten.PlayCardSlot(i, t)] = PlayableHand[i] && PlayTargets[i][t];
            }
        }

        for (int s = 0; s < ActionFlatten.MaxPotions; s++)
        {
            for (int t = 0; t < ActionFlatten.MaxTargets; t++)
            {
                Flat[ActionFlatten.UsePotionSlot(s, t)] = PotionUsable[s] && PotionTargets[s][t];
            }

            Flat[ActionFlatten.DiscardPotionBase + s] = PotionDiscardable[s];
        }

        Flat[ActionFlatten.EndTurnSlot] = VerbsAllowed[(int)Verb.EndTurn];

        for (int i = 0; i < MapNodes.Length; i++)
        {
            Flat[ActionFlatten.MapSelectBase + i] = MapNodes[i];
        }

        for (int i = 0; i < ChooseOptions.Length && ActionFlatten.ChooseBase + i < Size; i++)
        {
            Flat[ActionFlatten.ChooseBase + i] = ChooseOptions[i];
        }

        if (VerbsAllowed[(int)Verb.Proceed])
        {
            Flat[ActionFlatten.ProceedSlot] = true;
        }

        if (VerbsAllowed[(int)Verb.Skip])
        {
            Flat[ActionFlatten.SkipSlot] = true;
        }
    }
}
