namespace Surl.Quic;

/// <summary>
/// An <c>ACK</c> frame (types <c>0x02</c> and <c>0x03</c>, RFC 9000 section 19.3) with its
/// ranges as packet numbers rather than the gaps and lengths on the wire. The ranges run from
/// the largest packet number down, each separated from the next by at least one unacknowledged
/// packet number.
/// </summary>
/// <param name="Ranges">The acknowledged ranges, largest first; at least one.</param>
/// <param name="AckDelay">The encoded acknowledgment delay, before the <c>ack_delay_exponent</c> is applied.</param>
/// <param name="EcnCounts">The ECN counts, present on type <c>0x03</c> only.</param>
public sealed record QuicAckFrame(IReadOnlyList<QuicAckRange> Ranges, ulong AckDelay, QuicEcnCounts? EcnCounts) : QuicFrame
{
    /// <summary>The largest packet number acknowledged.</summary>
    public ulong LargestAcknowledged => Ranges[0].Largest;

    internal static QuicAckFrame ReadFrom(QuicReader reader, byte frameType)
    {
        var largest = reader.ReadVariableLengthInteger();
        var ackDelay = reader.ReadVariableLengthInteger();
        var additionalRangeCount = reader.ReadVariableLengthInteger();
        var smallest = Below(reader, largest, reader.ReadVariableLengthInteger());
        var ranges = new List<QuicAckRange> { new(smallest, largest) };
        for (ulong index = 0; index < additionalRangeCount; index++)
        {
            largest = Below(reader, smallest, reader.ReadVariableLengthInteger() + 2);
            smallest = Below(reader, largest, reader.ReadVariableLengthInteger());
            ranges.Add(new QuicAckRange(smallest, largest));
        }

        var ecnCounts = frameType == QuicFrameTypes.AckWithEcn
            ? new QuicEcnCounts(reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger())
            : null;
        return new QuicAckFrame(ranges, ackDelay, ecnCounts);
    }

    internal override void WriteTo(QuicWriter writer)
    {
        ArgumentOutOfRangeException.ThrowIfZero(Ranges.Count, nameof(Ranges));
        writer.WriteByte(EcnCounts is null ? QuicFrameTypes.Ack : QuicFrameTypes.AckWithEcn);
        writer.WriteVariableLengthInteger(LargestAcknowledged);
        writer.WriteVariableLengthInteger(AckDelay);
        writer.WriteVariableLengthInteger((ulong)Ranges.Count - 1);
        writer.WriteVariableLengthInteger(RangeLength(Ranges[0]));
        for (var index = 1; index < Ranges.Count; index++)
        {
            writer.WriteVariableLengthInteger(Gap(Ranges[index - 1], Ranges[index]));
            writer.WriteVariableLengthInteger(RangeLength(Ranges[index]));
        }

        WriteEcnCounts(writer, EcnCounts);
    }

    /// <summary>Returns <paramref name="from"/> minus <paramref name="distance"/>, or throws when the packet number would be negative.</summary>
    private static ulong Below(QuicReader reader, ulong from, ulong distance) =>
        distance > from
            ? throw reader.Fault(QuicTransportErrorCode.FrameEncodingError, "An ACK range reaches below packet number 0.")
            : from - distance;

    private static ulong RangeLength(QuicAckRange range) =>
        range.Smallest > range.Largest
            ? throw new ArgumentException($"The ACK range {range} has its smallest packet number above its largest.", nameof(Ranges))
            : range.Largest - range.Smallest;

    private static ulong Gap(QuicAckRange above, QuicAckRange below) =>
        below.Largest + 2 > above.Smallest
            ? throw new ArgumentException($"The ACK ranges {above} and {below} are not in descending order with a gap between them.", nameof(Ranges))
            : above.Smallest - below.Largest - 2;

    private static void WriteEcnCounts(QuicWriter writer, QuicEcnCounts? ecnCounts)
    {
        if (ecnCounts is not null)
        {
            writer.WriteVariableLengthInteger(ecnCounts.Ect0);
            writer.WriteVariableLengthInteger(ecnCounts.Ect1);
            writer.WriteVariableLengthInteger(ecnCounts.EcnCe);
        }
    }
}
