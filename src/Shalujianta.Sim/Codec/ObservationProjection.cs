namespace Shalujianta.Sim.Codec;

public enum InfoMode : byte
{
    NoSl = 0,
    Sl = 1,
}

/// <summary>
/// Documents which Observation JSON keys a NoSL codec must physically omit.
/// Python codec is the tested implementation until PR-008 bindings land.
/// </summary>
public static class ObservationProjection
{
    public static readonly string[] NoslForbiddenKeys =
    {
        "rng_counters",
        "seed_i32",
        "seed_string",
        "sl_peek_rewards",
    };
}
