using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>A test identifier source that gives the same bytes for the same seed.</summary>
internal sealed class SeededIdentifierRandom(int seed) : IIdentifierRandom
{
    private readonly Random random = new(seed);

    public void NextBytes(Span<byte> destination) => random.NextBytes(destination);
}

/// <summary>A test identifier source that fills every request with one byte.</summary>
internal sealed class FilledIdentifierRandom(byte value) : IIdentifierRandom
{
    public void NextBytes(Span<byte> destination) => destination.Fill(value);
}
