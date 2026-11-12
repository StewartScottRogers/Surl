using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>A test identifier source: the same seed gives the same bytes in the same order, as a seeded instance's does.</summary>
internal sealed class UnitTestSeededIdentifierRandom(int seed) : IIdentifierRandom
{
    private readonly Random random = new(seed);

    public void NextBytes(Span<byte> destination) => random.NextBytes(destination);
}
