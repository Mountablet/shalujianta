namespace Shalujianta.Sim.Core;

public interface IRngBackend
{
    RngBackendKind Kind { get; }
    void Seed(int runSeedI32);
    int NextInt(RngStream stream, int maxExclusive);
    float NextFloat(RngStream stream);
    int Counter(RngStream stream);
    void SetCounter(RngStream stream, int n);
    IRngBackend Clone();
    byte[] ExportState();
    void ImportState(ReadOnlySpan<byte> data);
}
