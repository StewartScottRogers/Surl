using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Fills each request for identifier bytes with one byte value, 1 the first time, 2 the next, so
/// each identifier drawn from it differs from the one before.
/// </summary>
internal sealed class CountingIdentifierRandom : IIdentifierRandom
{
    private int count;

    public void NextBytes(Span<byte> destination) => destination.Fill((byte)Interlocked.Increment(ref count));
}
