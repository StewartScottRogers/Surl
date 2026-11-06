namespace Surl.Quic;

/// <summary>
/// A <c>MAX_STREAMS</c> frame (type <c>0x12</c> for bidirectional streams, <c>0x13</c> for
/// unidirectional, RFC 9000 section 19.11): how many streams of that kind the peer may open. A
/// count above 2^60 is <see cref="QuicTransportErrorCode.FrameEncodingError"/>.
/// </summary>
/// <param name="Bidirectional">Whether the limit is on bidirectional streams.</param>
/// <param name="MaximumStreams">The cumulative number of streams the peer may open.</param>
public sealed record QuicMaxStreamsFrame(bool Bidirectional, ulong MaximumStreams) : QuicFrame
{
    internal static QuicMaxStreamsFrame ReadFrom(QuicReader reader, byte frameType) =>
        new(frameType == QuicFrameTypes.MaxStreamsBidirectional, ReadStreamCount(reader));

    /// <summary>Reads a stream count, refusing one above 2^60 (RFC 9000 sections 19.11 and 19.14).</summary>
    internal static ulong ReadStreamCount(QuicReader reader)
    {
        var count = reader.ReadVariableLengthInteger();
        return count > QuicFrameTypes.MaxStreamCount
            ? throw reader.Fault(QuicTransportErrorCode.FrameEncodingError, "A stream count is above 2^60.")
            : count;
    }

    internal override void WriteTo(QuicWriter writer)
    {
        writer.WriteByte(Bidirectional ? QuicFrameTypes.MaxStreamsBidirectional : QuicFrameTypes.MaxStreamsUnidirectional);
        writer.WriteVariableLengthInteger(MaximumStreams);
    }
}
