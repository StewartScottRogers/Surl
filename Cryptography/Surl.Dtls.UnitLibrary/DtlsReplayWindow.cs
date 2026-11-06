namespace Surl.Dtls;

/// <summary>
/// The anti-replay window of one epoch (RFC 6347 section 4.1.2.6): a 64-record sliding bitmap
/// anchored at the highest sequence number accepted, as RFC 4303 section 3.4.3 describes
/// (ADR-0087 decision 8).
/// </summary>
public sealed class DtlsReplayWindow
{
    /// <summary>How many records the window spans: 64.</summary>
    public const int Size = 64;

    private long highestAccepted = -1;
    private ulong acceptedBitmap;

    /// <summary>
    /// Says whether a record with <paramref name="sequenceNumber"/> is new: higher than every
    /// record accepted, or inside the window and not yet accepted.
    /// </summary>
    /// <param name="sequenceNumber">The record's sequence number.</param>
    /// <returns><see langword="false"/> for a replay or a record older than the window.</returns>
    public bool IsNew(long sequenceNumber)
    {
        if (sequenceNumber > highestAccepted)
        {
            return true;
        }

        var age = highestAccepted - sequenceNumber;
        return age < Size && (acceptedBitmap & (1UL << (int)age)) == 0;
    }

    /// <summary>
    /// Records that a record with <paramref name="sequenceNumber"/> was accepted. Call it only
    /// after <see cref="IsNew"/> said so and the record's tag verified, as section 4.1.2.6 requires.
    /// </summary>
    /// <param name="sequenceNumber">The accepted record's sequence number.</param>
    public void MarkAccepted(long sequenceNumber)
    {
        if (sequenceNumber > highestAccepted)
        {
            var advance = sequenceNumber - highestAccepted;
            acceptedBitmap = advance >= Size ? 0 : acceptedBitmap << (int)advance;
            acceptedBitmap |= 1;
            highestAccepted = sequenceNumber;
            return;
        }

        acceptedBitmap |= 1UL << (int)(highestAccepted - sequenceNumber);
    }
}
