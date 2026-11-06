namespace Surl.Quic;

/// <summary>
/// A <c>STREAMS_BLOCKED</c> frame (type <c>0x16</c> for bidirectional streams, <c>0x17</c> for
/// unidirectional, RFC 9000 section 19.14): the sender wants to open a stream of that kind and is
/// held by the limit. A count above 2^60 is <see cref="QuicTransportErrorCode.FrameEncodingError"/>.
/// </summary>
/// <param name="Bidirectional">Whether the sender is held on bidirectional streams.</param>
/// <param name="MaximumStreams">The stream limit the sender is held at.</param>
public sealed record QuicStreamsBlockedFrame(bool Bidirectional, ulong MaximumStreams) : QuicFrame
{
    internal static QuicStreamsBlockedFrame ReadFrom(QuicReader reader, byte frameType) =>
        new(frameType == QuicFrameTypes.StreamsBlockedBidirectional, QuicMaxStreamsFrame.ReadStreamCount(reader));

    internal override void WriteTo(QuicWriter writer)
    {
        writer.WriteByte(Bidirectional ? QuicFrameTypes.StreamsBlockedBidirectional : QuicFrameTypes.StreamsBlockedUnidirectional);
        writer.WriteVariableLengthInteger(MaximumStreams);
    }
}
