using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Relics;

/// <summary>
/// Burning Blood: heal 6 HP at the end of (won) combat, capped at max HP.
/// Wiki value (STS2:Ironclad, 2026-10-31). The only v0 relic (design 4.1.1).
/// </summary>
public sealed class BurningBlood : IRelicHooks
{
    public const int HealOnCombatEnd = 6;

    public void OnCombatStart(CombatWorld world) { }

    public void OnTurnStart(CombatWorld world) { }

    public void OnCombatEnd(CombatWorld world)
    {
        // Healing matters only on a win: a loss means the player is at 0 HP.
        if (world.IsTerminal && world.TerminalStatus == StepStatus.TerminalWin)
        {
            world.HealPlayer(HealOnCombatEnd);
        }
    }
}
