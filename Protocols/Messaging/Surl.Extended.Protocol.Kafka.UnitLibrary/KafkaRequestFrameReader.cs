using System.Buffers.Binary;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Reads size-prefixed Kafka requests from one connection, one after another, however their
/// bytes are split across reads (the Kafka protocol guide, "Common Request and Response
/// Structure": an INT32 size, then that many bytes).
/// </summary>
/// <remarks>
/// A size of zero or less, or over <c>maxRequestBytes</c> (ADR-0105 decision 7: ADR-0006's
/// <c>--max-message</c>, Kafka's <c>socket.request.max.bytes</c>), is refused from the prefix
/// alone, the body never read. Reads never ask for more than what is left of the request, so
/// the reader never takes a byte of the next one. The buffer grows as bytes arrive, so a size
/// the client never sends costs at most 64 KiB and at most twice what did arrive. Not safe for
/// concurrent calls.
/// </remarks>
/// <param name="connection">The connection to read from.</param>
/// <param name="maxRequestBytes">The most bytes a request may hold after its prefix; 0 means no limit.</param>
public sealed class KafkaRequestFrameReader(IConnection connection, long maxRequestBytes)
{
    private const int SizePrefixBytes = 4;
    private const int InitialRequestBytes = 65536;

    private readonly byte[] sizePrefix = new byte[SizePrefixBytes];

    /// <summary>Reads the next request.</summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The request, or the named reason there is none.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<KafkaRequestFrameReadResult> ReadRequestAsync(CancellationToken cancellationToken)
    {
        var prefixRead = await FillAsync(sizePrefix, sizePrefix.Length, cancellationToken);
        if (prefixRead < SizePrefixBytes)
        {
            return new(prefixRead == 0 ? KafkaRequestFrameReadOutcome.ConnectionClosed : KafkaRequestFrameReadOutcome.ConnectionClosedMidRequest, 0, default);
        }

        var size = BinaryPrimitives.ReadInt32BigEndian(sizePrefix);
        if (size <= 0 || (maxRequestBytes > 0 && size > maxRequestBytes))
        {
            return new(KafkaRequestFrameReadOutcome.SizeRefused, size, default);
        }

        return await ReadBodyAsync(size, cancellationToken);
    }

    private async ValueTask<KafkaRequestFrameReadResult> ReadBodyAsync(int size, CancellationToken cancellationToken)
    {
        var body = new byte[Math.Min(size, InitialRequestBytes)];
        var filled = 0;
        while (filled < size)
        {
            if (filled == body.Length)
            {
                Array.Resize(ref body, (int)Math.Min(size, 2L * body.Length));
            }

            var read = await FillAsync(body.AsMemory(filled), body.Length - filled, cancellationToken);
            filled += read;
            if (filled < body.Length)
            {
                return new(KafkaRequestFrameReadOutcome.ConnectionClosedMidRequest, size, default);
            }
        }

        return new(KafkaRequestFrameReadOutcome.RequestRead, size, body);
    }

    // Reads until count bytes are in the buffer or the client closes; returns how many arrived.
    private async ValueTask<int> FillAsync(Memory<byte> buffer, int count, CancellationToken cancellationToken)
    {
        var filled = 0;
        while (filled < count)
        {
            var read = await connection.ReadAsync(buffer[filled..count], cancellationToken);
            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        return filled;
    }
}
