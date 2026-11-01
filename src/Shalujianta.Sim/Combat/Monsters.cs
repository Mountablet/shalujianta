namespace Shalujianta.Sim.Combat;

/// <summary>INTENT entity ids in the catalog are 0x0800000X; combat logic uses this compact enum.</summary>
public enum IntentId : byte
{
    Unknown = 0,
    Attack = 1,
    Defend = 2,
    Buff = 3,
    Debuff = 4,
}

/// <summary>Optional non-damage effect of a monster move (buff/debuff moves).</summary>
public delegate void MoveEffect(CombatWorld world, int slot);

public readonly record struct MonsterMove(
    int MoveId,
    IntentId Intent,
    int BaseDamage,
    int Hits,
    MoveEffect? Effect = null);

/// <summary>
/// Monster behaviour (§4.11: Monsters/&lt;Name&gt;.cs -> IMonsterAi).
/// ChooseNextMove rolls the intent shown to the player; MoveFor resolves a stored
/// move id back to the executable move (needed after Checkpoint restore).
/// MonsterInstance.Misc is the AI's serialized scratch: the world bumps it after
/// every roll, so scripted AIs can use it as a cursor that survives checkpoints.
/// </summary>
public interface IMonsterAi
{
    MonsterMove ChooseNextMove(CombatWorld world, int slot);
    MonsterMove MoveFor(CombatWorld world, int slot, int moveId);
}

/// <summary>
/// Deterministic test/golden double: plays moves in order, repeats the last one.
/// Never touches the MonsterAi stream, so kernel tests are seed-independent.
/// </summary>
public sealed class ScriptedMonsterAi : IMonsterAi
{
    private readonly MonsterMove[] _moves;

    public ScriptedMonsterAi(params MonsterMove[] moves)
    {
        _moves = moves.Length > 0
            ? moves
            : [new MonsterMove(0, IntentId.Unknown, 0, 1)];
    }

    public MonsterMove ChooseNextMove(CombatWorld world, int slot)
    {
        int idx = Math.Min(world.Monsters[slot].Misc, _moves.Length - 1);
        return _moves[idx];
    }

    public MonsterMove MoveFor(CombatWorld world, int slot, int moveId) =>
        _moves[Math.Clamp(moveId, 0, _moves.Length - 1)];
}

/// <summary>
/// Mutable monster row (struct-in-list, slot == list index). Powers is a reference
/// list owned by the row; copy the struct back after mutating fields.
/// </summary>
public struct MonsterInstance
{
    public int Slot;
    public int CatalogId;
    public int Hp;
    public int MaxHp;
    public int Block;
    public bool Alive;
    public bool HalfDead;
    public IntentId Intent;
    public bool IntentKnown;
    public int MoveId;
    public int MoveBaseDamage;
    public int MoveHits;
    public List<PowerStack> Powers;
    public int Misc;

    /// <summary>AI-private scratch that survives checkpoints (used-Roar flag, phase counters).</summary>
    public int AiState;
}
