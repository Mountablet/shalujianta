using Shalujianta.Sim.Core;
using Xunit;

namespace Shalujianta.Sim.Tests;

public sealed class RngTests
{
    [Fact]
    public void CompatMatchesSeededSystemRandom()
    {
        const int seed = 42;
        var compat = new SystemRandomCompat(seed);
        var bcl = new Random(seed);
        for (int i = 0; i < 64; i++)
        {
            Assert.Equal(bcl.Next(), compat.Next());
        }

        compat.Seed(seed);
        bcl = new Random(seed);
        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(bcl.Next(17), compat.Next(17));
            Assert.Equal(bcl.NextDouble(), compat.NextDouble(), 12);
        }
    }

    [Fact]
    public void DeterministicHashMatchesKnownBoundarySeed()
    {
        // Documented in tck.mn comments / sts2_rng_predictor:
        // GetDeterministicHashCode("0P2ENNHM") == int.MinValue
        Assert.Equal(int.MinValue, DeterministicHash.GetDeterministicHashCode("0P2ENNHM"));
        Assert.Equal("up_front", DeterministicHash.SnakeCase("UpFront"));
        Assert.Equal("combat_orbs", DeterministicHash.SnakeCase("CombatOrbs"));
    }

    [Fact]
    public void StreamSeedsUseHashStringsAndNeowExtraOffset()
    {
        var shuffle = RngStreamTable.All.Single(s => s.Stream == RngStream.Shuffle);
        var neow = RngStreamTable.All.Single(s => s.Stream == RngStream.Neow);
        Assert.Equal("shuffle", shuffle.HashString);
        Assert.Equal("NEOW", neow.HashString);
        Assert.Equal(1, neow.ExtraOffset);

        int run = 123456;
        int derivedShuffle = RngStreamTable.DerivedSeed(run, shuffle);
        int expected = unchecked((int)((uint)run + (uint)DeterministicHash.GetDeterministicHashCode("shuffle")));
        Assert.Equal(expected, derivedShuffle);
    }

    [Fact]
    public void CheckpointRoundTripPreservesGameVersionAndStream()
    {
        var rng = new MultiRng();
        rng.Seed(99);
        _ = rng.NextInt(RngStream.Shuffle, 10);
        _ = rng.NextFloat(RngStream.MonsterAi);

        var ckpt = SimCheckpoint.Capture(rng, catalogVersion: "synthetic-v0", gameVersion: "sts2-unverified");
        Assert.Equal("sts2-unverified", ckpt.GameVersionString);
        Assert.Equal((ushort)1, ckpt.SchemaVersion);
        Assert.Equal(99, ckpt.RunSeedI32);

        string json = ckpt.ToJson();
        Assert.Contains("game_version_string", json);

        var restored = new MultiRng();
        SimCheckpoint.FromJson(json).RestoreInto(restored);
        Assert.Equal(rng.NextInt(RngStream.Shuffle, 10), restored.NextInt(RngStream.Shuffle, 10));
        Assert.Equal(rng.Counter(RngStream.MonsterAi), restored.Counter(RngStream.MonsterAi));
        Assert.Equal(ckpt.Hash64(), SimCheckpoint.FromJson(json).Hash64());
    }

    [Fact]
    public void SetCounterReplaysFromSeed()
    {
        var rng = new MultiRng();
        rng.Seed(7);
        int a = rng.NextInt(RngStream.Niche, 100);
        int b = rng.NextInt(RngStream.Niche, 100);
        rng.SetCounter(RngStream.Niche, 1);
        Assert.Equal(b, rng.NextInt(RngStream.Niche, 100));
        rng.SetCounter(RngStream.Niche, 0);
        Assert.Equal(a, rng.NextInt(RngStream.Niche, 100));
    }

    [Fact]
    public void CrngStillLinearInCompatAlgorithm()
    {
        // First InternalSample is affine in abs(seed) mod (2^31-1).
        // Live game dll is not loaded in CI.
        int s0 = new SystemRandomCompat(1000).InternalSample();
        int s1 = new SystemRandomCompat(1001).InternalSample();
        int s2 = new SystemRandomCompat(1002).InternalSample();
        int m = SystemRandomCompat.Mbig;
        int step = (int)(((long)s1 - s0 + m) % m);
        int step2 = (int)(((long)s2 - s1 + m) % m);
        Assert.Equal(step, step2);
        Assert.NotEqual(0, step);
    }
}
