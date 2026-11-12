using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>An <see cref="IIdentifierRandom"/> that repeats for one seed, standing in for <c>--random-seed</c>.</summary>
internal sealed class SeededTestIdentifierRandom(int seed) : IIdentifierRandom
{
    private readonly Random random = new(seed);

    public void NextBytes(Span<byte> destination) => random.NextBytes(destination);
}
