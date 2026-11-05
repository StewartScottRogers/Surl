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

    private readonly QuicOrderedBytes ordered = new();

    /// <summary>Takes one <c>CRYPTO</c> frame's data and returns the bytes that are now in order and new.</summary>
    /// <exception cref="QuicTransportException"><c>CRYPTO_BUFFER_EXCEEDED</c>: the frame reaches too far ahead.</exception>
    public byte[] Add(ulong offset, ReadOnlySpan<byte> data)
    {
        var end = offset + (ulong)data.Length;
        if (end > ordered.Delivered && end - ordered.Delivered > MaxBufferedBytes)
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.CryptoBufferExceeded,
                $"CRYPTO data reaches {end - ordered.Delivered} bytes past what has been delivered; the most buffered is {MaxBufferedBytes}.",
                QuicFrameTypes.Crypto);
        }

        return ordered.Add(offset, data);
    }
}
