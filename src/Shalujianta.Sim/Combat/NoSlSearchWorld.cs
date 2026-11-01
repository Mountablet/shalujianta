using Shalujianta.Sim.Codec;
using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Combat;

/// <summary>
/// NoSL search handle (§4.10.1 / KD-16). Wraps a throwaway CombatWorld whose hidden
/// state was destroyed and resampled; the wrapper deliberately implements only
/// INoSlSearchWorld so search code cannot reach Clone/Checkpoint/Seed/Hash64.
/// </summary>
public sealed class NoSlSearchWorld : INoSlSearchWorld
{
    private readonly CombatWorld _world;

    internal NoSlSearchWorld(CombatWorld world) => _world = world;

    public ulong InfoSetHash64() => _world.InfoSetHash64();

    public ActionMask LegalActions() => _world.LegalActions();

    public StepResult Step(in Action action) => _world.Step(action);

    public CombatObservation Observe() => _world.Observe();

    public INoSlSearchWorld ResampleHidden(ulong resampleSeed) => _world.CloneForSearch(resampleSeed);
}
