using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Combat;

/// <summary>
/// Hook insertion points (§4.11). The full HookId table grows with the decompile
/// index in PR-014; v0 only needs the combat-lifecycle hooks below.
/// </summary>
public enum HookId : byte
{
    OnCombatStart,
    OnTurnStart,
    OnTurnEnd,
    OnCombatEnd,
    OnCombatEndHeal,
    OnPlay,
    OnAfterPlay,
    OnDamageGiven,
    OnDamageTaken,
    OnDeath,
    OnKill,
    OnDraw,
    OnDiscard,
    OnExhaust,
    OnReshuffle,
}

/// <summary>
/// Relic passive hooks. v0 registers exactly one implementation: BurningBlood
/// (PR-005). Everything else logs NotImplementedHook instead of silently passing.
/// </summary>
public interface IRelicHooks
{
    void OnCombatStart(CombatWorld world);
    void OnTurnStart(CombatWorld world);
    void OnCombatEnd(CombatWorld world);
}

public static class RelicRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<int, IRelicHooks> Hooks = [];

    public static void Register(int catalogId, IRelicHooks hooks)
    {
        lock (Gate)
        {
            Hooks[catalogId] = hooks;
        }
    }

    public static bool TryGet(int catalogId, out IRelicHooks hooks)
    {
        lock (Gate)
        {
            return Hooks.TryGetValue(catalogId, out hooks!);
        }
    }
}

public static class HookLog
{
    /// <summary>Stub marker: the hook exists in the game but is not implemented yet.</summary>
    public static void NotImplemented(SimConfig config, string what) =>
        config.HookLog?.Invoke($"not_implemented_hook: {what}");
}
