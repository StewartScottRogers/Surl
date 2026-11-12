using System.Buffers.Binary;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The production <see cref="ICoapRandomSource"/>: every draw from the exchange's
/// <see cref="IIdentifierRandom"/>, so a seeded instance repeats Message IDs, retransmission jitter and
/// posted resource names every run (ADR-0124 decision 6). None of them is secret.
/// </summary>
/// <param name="identifiers">The exchange's identifier source.</param>
internal sealed class IdentifierCoapRandomSource(IIdentifierRandom identifiers) : ICoapRandomSource
{
    private const int FractionSteps = 1 << 30;

    // A flow's separate responses, observers and requests may draw at once.
    private readonly object gate = new();

    /// <inheritdoc/>
    public ushort DrawMessageId()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        Fill(bytes);
        return BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    /// <inheritdoc/>
    public double DrawFraction()
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        Fill(bytes);
        return (BinaryPrimitives.ReadUInt32BigEndian(bytes) & (FractionSteps - 1)) / (double)FractionSteps;
    }

    /// <inheritdoc/>
    public void Fill(Span<byte> bytes)
    {
        lock (gate)
        {
            identifiers.NextBytes(bytes);
        }
    }
}
