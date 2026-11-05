namespace Surl.Quic;

/// <summary>
/// Puts one packet number space's <c>CRYPTO</c> data back in order (RFC 9000 section 7.5): frames
/// may arrive out of order, overlap and repeat, and the bytes come out once each, in order. Data
/// more than <see cref="MaxBufferedBytes"/> past what has come out is
/// <c>CRYPTO_BUFFER_EXCEEDED</c>.
/// </summary>
internal sealed class QuicCryptoReassembly
{
    /// <summary>How far past the bytes delivered so far a <c>CRYPTO</c> frame may reach, 64 KiB.</summary>
    public const int MaxBufferedBytes = 1 << 16;

    private readonly SortedDictionary<ulong, byte[]> pending = [];
    private ulong delivered;

    /// <summary>Takes one <c>CRYPTO</c> frame's data and returns the bytes that are now in order and new.</summary>
    /// <exception cref="QuicTransportException"><c>CRYPTO_BUFFER_EXCEEDED</c>: the frame reaches too far ahead.</exception>
    public byte[] Add(ulong offset, ReadOnlySpan<byte> data)
    {
        var end = offset + (ulong)data.Length;
        if (end <= delivered)
        {
            return [];
        }

        if (end - delivered > MaxBufferedBytes)
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.CryptoBufferExceeded,
                $"CRYPTO data reaches {end - delivered} bytes past what has been delivered; the most buffered is {MaxBufferedBytes}.",
                QuicFrameTypes.Crypto);
        }

        if (!pending.TryGetValue(offset, out var existing) || existing.Length < data.Length)
        {
            pending[offset] = data.ToArray();
        }

        return TakeContiguous();
    }

    private byte[] TakeContiguous()
    {
        var output = new List<byte>();
        while (pending.Count > 0 && pending.First() is var (start, bytes) && start <= delivered)
        {
            pending.Remove(start);
            var alreadyDelivered = delivered - start;
            if (alreadyDelivered < (ulong)bytes.Length)
            {
                output.AddRange(bytes.AsSpan((int)alreadyDelivered));
                delivered = start + (ulong)bytes.Length;
            }
        }

        return [.. output];
    }
}
