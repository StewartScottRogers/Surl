using System.Buffers.Binary;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// The seeded identifier sources of one instance (ADR-0124, decision 6): the same seed gives the same
/// non-secret identifiers every run. Each exchange's bytes are derived from the seed and the exchange
/// ID only, and each server-life source's from the seed and a name only, so drawing for other
/// exchanges, in any order, never changes them. Never draw anything secret from them; see
/// <see cref="IIdentifierRandom"/>.
/// </summary>
/// <remarks>
/// Each source is SHA-256 in counter mode: block <c>n</c> is SHA-256 of a domain label (<c>exchange</c>
/// or <c>server</c>), the seed, the exchange ID or the name, and <c>n</c>. Instances share nothing.
/// </remarks>
public sealed class SeededIdentifierRandom
{
    private readonly long seed;

    /// <summary>
    /// Creates the sources for <paramref name="seed"/>.
    /// </summary>
    /// <param name="seed">The seed, 0 to 2^63-1 (<c>--random-seed</c>).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is negative.</exception>
    public SeededIdentifierRandom(long seed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seed);
        this.seed = seed;
    }

    /// <summary>
    /// The seed the sources are derived from.
    /// </summary>
    public long Seed => seed;

    /// <summary>
    /// A fresh source for one exchange, starting at the first byte of the sequence the seed and
    /// <paramref name="exchangeId"/> give.
    /// </summary>
    /// <param name="exchangeId">The exchange's <see cref="ExchangeContext.ExchangeId"/>.</param>
    /// <returns>A source that is not safe to share between threads; one exchange draws from it.</returns>
    public IIdentifierRandom ForExchange(long exchangeId)
    {
        Span<byte> id = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(id, exchangeId);
        return new Sha256CounterIdentifierRandom(Prefix("exchange", id));
    }

    /// <summary>
    /// A fresh source for identifiers a server draws once, when it is created, starting at the first
    /// byte of the sequence the seed and <paramref name="name"/> give.
    /// </summary>
    /// <param name="name">Names the server or what it draws for, such as <c>nats</c>; compared ordinally.</param>
    /// <returns>A source that is not safe to share between threads.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public IIdentifierRandom ForServer(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return new Sha256CounterIdentifierRandom(Prefix("server", Encoding.UTF8.GetBytes(name)));
    }

    private byte[] Prefix(string label, ReadOnlySpan<byte> scope)
    {
        var labelBytes = Encoding.ASCII.GetBytes(label);
        var prefix = new byte[labelBytes.Length + 1 + sizeof(long) + sizeof(int) + scope.Length];
        var span = prefix.AsSpan();
        labelBytes.CopyTo(span);
        span = span[(labelBytes.Length + 1)..];
        BinaryPrimitives.WriteInt64BigEndian(span, seed);
        BinaryPrimitives.WriteInt32BigEndian(span[sizeof(long)..], scope.Length);
        scope.CopyTo(span[(sizeof(long) + sizeof(int))..]);
        return prefix;
    }
}
