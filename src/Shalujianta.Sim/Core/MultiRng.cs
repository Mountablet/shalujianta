namespace Shalujianta.Sim.Core;

/// <summary>
/// One independent SystemRandomCompat (or Pcg32) per named stream.
/// Stream seed = runSeed + hash(hashString) + extraOffset (uint32 wrap).
/// </summary>
public sealed class MultiRng : IRngBackend
{
    public const int StreamCount = 16;

    private readonly RngBackendKind _kind;
    private readonly SystemRandomCompat[] _compat = new SystemRandomCompat[StreamCount];
    private readonly Pcg32[] _pcg = new Pcg32[StreamCount];
    private readonly int[] _counters = new int[StreamCount];
    private int _runSeed;

    public MultiRng(RngBackendKind kind = RngBackendKind.SystemRandomLegacy)
    {
        _kind = kind;
        for (int i = 0; i < StreamCount; i++)
        {
            _compat[i] = new SystemRandomCompat(0);
            _pcg[i] = new Pcg32(0);
        }
    }

    public RngBackendKind Kind => _kind;

    public int RunSeed => _runSeed;

    public void Seed(int runSeedI32)
    {
        _runSeed = runSeedI32;
        foreach (var spec in RngStreamTable.All)
        {
            int derived = RngStreamTable.DerivedSeed(runSeedI32, spec);
            int i = (int)spec.Stream;
            if (_kind == RngBackendKind.SystemRandomLegacy)
            {
                _compat[i].Seed(derived);
            }
            else
            {
                _pcg[i].Seed(unchecked((ulong)(uint)derived));
            }

            _counters[i] = 0;
        }
    }

    public int NextInt(RngStream stream, int maxExclusive)
    {
        int i = (int)stream;
        _counters[i]++;
        return _kind == RngBackendKind.SystemRandomLegacy
            ? _compat[i].Next(maxExclusive)
            : _pcg[i].Next(maxExclusive);
    }

    public float NextFloat(RngStream stream)
    {
        int i = (int)stream;
        _counters[i]++;
        return _kind == RngBackendKind.SystemRandomLegacy
            ? _compat[i].NextFloat()
            : _pcg[i].NextFloat();
    }

    public int Counter(RngStream stream) => _counters[(int)stream];

    public void SetCounter(RngStream stream, int n)
    {
        if (n < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(n));
        }

        var spec = RngStreamTable.All[(int)stream];
        int derived = RngStreamTable.DerivedSeed(_runSeed, spec);
        int i = (int)stream;
        if (_kind == RngBackendKind.SystemRandomLegacy)
        {
            _compat[i].Seed(derived);
            for (int k = 0; k < n; k++)
            {
                _compat[i].InternalSample();
            }
        }
        else
        {
            _pcg[i].Seed(unchecked((ulong)(uint)derived));
            for (int k = 0; k < n; k++)
            {
                _pcg[i].NextUInt();
            }
        }

        _counters[i] = n;
    }

    public IRngBackend Clone()
    {
        var copy = new MultiRng(_kind);
        copy.ImportState(ExportState());
        return copy;
    }

    public byte[] ExportState()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)_kind);
        w.Write(_runSeed);
        var tmp = new int[56];
        for (int i = 0; i < StreamCount; i++)
        {
            w.Write(_counters[i]);
            if (_kind == RngBackendKind.SystemRandomLegacy)
            {
                _compat[i].Export(tmp, out int inext, out int inextp);
                w.Write(inext);
                w.Write(inextp);
                foreach (int v in tmp)
                {
                    w.Write(v);
                }
            }
            else
            {
                _pcg[i].Export(out ulong state, out ulong inc);
                w.Write(state);
                w.Write(inc);
            }
        }

        return ms.ToArray();
    }

    public void ImportState(ReadOnlySpan<byte> data)
    {
        using var ms = new MemoryStream(data.ToArray());
        using var r = new BinaryReader(ms);
        var kind = (RngBackendKind)r.ReadByte();
        if (kind != _kind)
        {
            throw new InvalidOperationException($"RNG backend mismatch: {kind} vs {_kind}");
        }

        _runSeed = r.ReadInt32();
        var tmp = new int[56];
        for (int i = 0; i < StreamCount; i++)
        {
            _counters[i] = r.ReadInt32();
            if (_kind == RngBackendKind.SystemRandomLegacy)
            {
                int inext = r.ReadInt32();
                int inextp = r.ReadInt32();
                for (int j = 0; j < 56; j++)
                {
                    tmp[j] = r.ReadInt32();
                }

                _compat[i].Restore(tmp, inext, inextp);
            }
            else
            {
                ulong state = r.ReadUInt64();
                ulong inc = r.ReadUInt64();
                _pcg[i].Restore(state, inc);
            }
        }
    }
}
