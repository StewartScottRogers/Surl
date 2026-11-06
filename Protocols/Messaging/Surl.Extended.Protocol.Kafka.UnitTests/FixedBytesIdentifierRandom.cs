using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>An identifier source that fills every request with the same bytes, repeated, and counts the requests.</summary>
internal sealed class FixedBytesIdentifierRandom(params byte[] bytes) : IIdentifierRandom
{
    public int Calls { get; private set; }

    public void NextBytes(Span<byte> destination)
    {
        Calls++;
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = bytes[index % bytes.Length];
        }
    }
}
