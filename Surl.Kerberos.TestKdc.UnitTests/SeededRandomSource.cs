namespace Surl.Kerberos.TestKdc;

/// <summary>
/// A random source that replays <see cref="Random" />'s sequence for a fixed seed, so every key
/// and confounder the KDC draws is the same on every run and no two keys are equal.
/// </summary>
/// <param name="seed">The seed; 266 unless a test needs another sequence.</param>
internal sealed class SeededRandomSource(int seed = 266) : IKerberosRandomSource
{
#pragma warning disable CA5394 // Test keys must be reproducible, not secret.
    private readonly Random random = new(seed);

    public void Fill(Span<byte> destination) => random.NextBytes(destination);
#pragma warning restore CA5394
}
