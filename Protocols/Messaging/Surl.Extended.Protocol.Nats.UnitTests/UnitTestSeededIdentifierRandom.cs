using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats.UnitTests;

/// <summary>An identifier source that gives the same bytes for the same seed.</summary>
internal sealed class UnitTestSeededIdentifierRandom(int seed) : IIdentifierRandom
{
    private readonly Random random = new(seed);

    public void NextBytes(Span<byte> destination) => random.NextBytes(destination);
}
