namespace Surl.Quic;

/// <summary>A <c>STREAM_DATA_BLOCKED</c> frame (type <c>0x15</c>, RFC 9000 section 19.13): the sender has data for a stream and is held by its limit.</summary>
/// <param name="StreamId">The stream that is held.</param>
/// <param name="MaximumStreamData">The stream limit the sender is held at.</param>
public sealed record QuicStreamDataBlockedFrame(ulong StreamId, ulong MaximumStreamData) : QuicFrame
{
    internal static QuicStreamDataBlockedFrame ReadFrom(QuicReader reader, byte frameType) =>
        new(reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger());

    internal override void WriteTo(QuicWriter writer)
    {
        writer.WriteByte(QuicFrameTypes.StreamDataBlocked);
        writer.WriteVariableLengthInteger(StreamId);
        writer.WriteVariableLengthInteger(MaximumStreamData);
    }
}
