namespace Shalujianta.Sim.Core;

/// <summary>
/// Pinned copy of the .NET Framework / seeded-compat System.Random algorithm
/// (SeedArray[56], inext, inextp, InternalSample). Do not call BCL Random
/// in the hot path — later runtimes may change the unseeded generator.
/// </summary>
public sealed class SystemRandomCompat
{
    public const int Mbig = int.MaxValue;
    private const int Mseed = 161803398;
    private const int StateSize = 56;

    private int _inext;
    private int _inextp;
    private readonly int[] _seedArray = new int[StateSize];

    public SystemRandomCompat() : this(0) { }

    public SystemRandomCompat(int seed) => Seed(seed);

    public void Seed(int seed)
    {
        int subtraction = seed == int.MinValue ? int.MaxValue : Math.Abs(seed);
        int mj = Mseed - subtraction;
        _seedArray[55] = mj;
        int mk = 1;
        for (int i = 1; i < 55; i++)
        {
            int ii = 21 * i % 55;
            _seedArray[ii] = mk;
            mk = mj - mk;
            if (mk < 0)
            {
                mk += Mbig;
            }

            mj = _seedArray[ii];
        }

        for (int k = 1; k < 5; k++)
        {
            for (int i = 1; i < StateSize; i++)
            {
                _seedArray[i] -= _seedArray[1 + (i + 30) % 55];
                if (_seedArray[i] < 0)
                {
                    _seedArray[i] += Mbig;
                }
            }
        }

        _inext = 0;
        _inextp = 21;
    }

    public int InternalSample()
    {
        int locINext = _inext;
        int locINextp = _inextp;
        if (++locINext >= StateSize)
        {
            locINext = 1;
        }

        if (++locINextp >= StateSize)
        {
            locINextp = 1;
        }

        int retVal = _seedArray[locINext] - _seedArray[locINextp];
        if (retVal == Mbig)
        {
            retVal--;
        }

        if (retVal < 0)
        {
            retVal += Mbig;
        }

        _seedArray[locINext] = retVal;
        _inext = locINext;
        _inextp = locINextp;
        return retVal;
    }

    public int Next() => InternalSample();

    public int Next(int maxExclusive)
    {
        if (maxExclusive < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        }

        return (int)(Sample() * maxExclusive);
    }

    public int Next(int minInclusive, int maxExclusive)
    {
        if (minInclusive > maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(minInclusive));
        }

        long range = (long)maxExclusive - minInclusive;
        if (range <= Mbig)
        {
            return (int)(Sample() * range) + minInclusive;
        }

        throw new NotSupportedException("range larger than int.MaxValue");
    }

    public double NextDouble() => Sample();

    public float NextFloat() => (float)Sample();

    public double Sample() => InternalSample() * (1.0 / Mbig);

    public SystemRandomCompat Clone()
    {
        var copy = new SystemRandomCompat();
        copy.Restore(_seedArray, _inext, _inextp);
        return copy;
    }

    public void Restore(ReadOnlySpan<int> seedArray, int inext, int inextp)
    {
        if (seedArray.Length != StateSize)
        {
            throw new ArgumentException("SeedArray must have 56 entries.", nameof(seedArray));
        }

        seedArray.CopyTo(_seedArray);
        _inext = inext;
        _inextp = inextp;
    }

    public void Export(Span<int> seedArray, out int inext, out int inextp)
    {
        if (seedArray.Length != StateSize)
        {
            throw new ArgumentException("SeedArray must have 56 entries.", nameof(seedArray));
        }

        _seedArray.AsSpan().CopyTo(seedArray);
        inext = _inext;
        inextp = _inextp;
    }

    public int[] ExportSeedArray()
    {
        var copy = new int[StateSize];
        _seedArray.CopyTo(copy, 0);
        return copy;
    }

    public (int INext, int INextP) ExportIndex() => (_inext, _inextp);
}
