namespace Shalujianta.Sim.Core;

/// <summary>
/// Minimal PCG32 (XSH-RR). Escape hatch if live STS2 leaves System.Random.
/// Not the v0 default.
/// </summary>
public sealed class Pcg32
{
    private ulong _state;
    private ulong _inc = 1442695040888963407UL;

    public Pcg32(ulong seed) => Seed(seed);

    public void Seed(ulong seed)
    {
        _state = 0;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    public uint NextUInt()
    {
        ulong old = _state;
        _state = old * 6364136223846793005UL + _inc;
        uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
        int rot = (int)(old >> 59);
        return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
    }

    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        }

        return (int)(NextUInt() % (uint)maxExclusive);
    }

    public float NextFloat() => (NextUInt() >> 8) * (1f / (1 << 24));

    public void Export(out ulong state, out ulong inc)
    {
        state = _state;
        inc = _inc;
    }

    public void Restore(ulong state, ulong inc)
    {
        _state = state;
        _inc = inc;
    }
}
