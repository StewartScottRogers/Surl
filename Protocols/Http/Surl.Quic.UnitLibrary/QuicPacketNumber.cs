namespace Surl.Quic;

/// <summary>QUIC packet numbers (RFC 9000 sections 12.3 and 17.1).</summary>
public static class QuicPacketNumber
{
    /// <summary>The largest packet number QUIC allows, 2^62-1.</summary>
    public const ulong MaxValue = (1UL << 62) - 1;

    /// <summary>
    /// Recovers a full packet number from the low bytes a header carries (RFC 9000 Appendix A.3):
    /// the value with those low bytes closest to <paramref name="expectedPacketNumber"/>.
    /// </summary>
    /// <param name="expectedPacketNumber">One more than the largest packet number received in this packet number space, or 0 before any.</param>
    /// <param name="truncatedPacketNumber">The packet number's low bytes as sent.</param>
    /// <param name="packetNumberLength">How many bytes were sent, 1 to 4.</param>
    /// <returns>The full packet number.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="packetNumberLength"/> is not 1 to 4, or <paramref name="expectedPacketNumber"/> is above 2^62.</exception>
    public static ulong Decode(ulong expectedPacketNumber, uint truncatedPacketNumber, int packetNumberLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(packetNumberLength, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(packetNumberLength, 4);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(expectedPacketNumber, MaxValue + 1);
        var expected = (long)expectedPacketNumber;
        var window = 1L << (8 * packetNumberLength);
        var halfWindow = window / 2;
        var candidate = (expected & ~(window - 1)) | truncatedPacketNumber;
        if (candidate <= expected - halfWindow && candidate < (1L << 62) - window)
        {
            return (ulong)(candidate + window);
        }

        return candidate > expected + halfWindow && candidate >= window
            ? (ulong)(candidate - window)
            : (ulong)candidate;
    }

    /// <summary>Returns a packet number's low <paramref name="packetNumberLength"/> bytes: what its header carries.</summary>
    /// <param name="packetNumber">The full packet number.</param>
    /// <param name="packetNumberLength">How many bytes are sent, 1 to 4.</param>
    /// <returns>The truncated packet number.</returns>
    internal static uint Truncate(ulong packetNumber, int packetNumberLength) =>
        (uint)(packetNumber & ((1UL << (8 * packetNumberLength)) - 1));
}
