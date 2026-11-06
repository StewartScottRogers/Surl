namespace Surl.Quic;

/// <summary>
/// The packet numbers received in one packet number space, as the ranges an <c>ACK</c> frame
/// reports (RFC 9000 section 13.2), largest first. At most <see cref="MaxRanges"/> ranges are
/// kept; when one more would be needed the lowest is forgotten, and every packet number below the
/// lowest range kept then counts as received, so a packet that late is dropped as a duplicate.
/// </summary>
internal sealed class QuicReceivedPacketNumbers
{
    /// <summary>The most ranges kept, and so the most an <c>ACK</c> frame reports.</summary>
    public const int MaxRanges = 32;

    private readonly List<QuicAckRange> ranges = [];
    private ulong forgottenBelow;

    /// <summary>The ranges received, largest first.</summary>
    public IReadOnlyList<QuicAckRange> Ranges => ranges;

    /// <summary>The largest packet number received, or null before any.</summary>
    public ulong? Largest => ranges.Count == 0 ? null : ranges[0].Largest;

    /// <summary>Whether <paramref name="packetNumber"/> was received already, or is below every range kept.</summary>
    public bool Contains(ulong packetNumber) =>
        packetNumber < forgottenBelow || ranges.Exists(range => range.Smallest <= packetNumber && packetNumber <= range.Largest);

    /// <summary>Records a packet number not received before.</summary>
    public void Add(ulong packetNumber)
    {
        var below = ranges.FindIndex(range => range.Largest < packetNumber);
        Insert(below < 0 ? ranges.Count : below, packetNumber);
        ForgetBeyondMaxRanges();
    }

    /// <summary>Puts a packet number in at <paramref name="below"/>, the index of the first range below it, joining the ranges it touches.</summary>
    private void Insert(int below, ulong packetNumber)
    {
        var joinsAbove = below > 0 && ranges[below - 1].Smallest == packetNumber + 1;
        var joinsBelow = below < ranges.Count && ranges[below].Largest + 1 == packetNumber;
        if (joinsAbove && joinsBelow)
        {
            ranges[below - 1] = new QuicAckRange(ranges[below].Smallest, ranges[below - 1].Largest);
            ranges.RemoveAt(below);
        }
        else if (joinsAbove)
        {
            ranges[below - 1] = ranges[below - 1] with { Smallest = packetNumber };
        }
        else if (joinsBelow)
        {
            ranges[below] = ranges[below] with { Largest = packetNumber };
        }
        else
        {
            ranges.Insert(below, new QuicAckRange(packetNumber, packetNumber));
        }
    }

    private void ForgetBeyondMaxRanges()
    {
        if (ranges.Count > MaxRanges)
        {
            ranges.RemoveAt(ranges.Count - 1);
            forgottenBelow = ranges[^1].Smallest;
        }
    }
}
