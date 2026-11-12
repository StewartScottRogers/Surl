using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats.UnitTests;

/// <summary>An identifier source that hands out the given bytes in order, starting over at the end.</summary>
internal sealed class UnitTestByteSequenceIdentifierRandom(byte[] bytes) : IIdentifierRandom
{
    private int next;

    public void NextBytes(Span<byte> destination)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = bytes[next];
            next = (next + 1) % bytes.Length;
        }
    }
}
