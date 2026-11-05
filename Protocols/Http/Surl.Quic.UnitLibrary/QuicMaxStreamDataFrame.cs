namespace Surl.Quic;

/// <summary>A <c>MAX_STREAM_DATA</c> frame (type <c>0x11</c>, RFC 9000 section 19.10): one stream's flow control limit.</summary>
/// <param name="StreamId">The stream the limit applies to.</param>
/// <param name="MaximumStreamData">The most data, in bytes, the peer may send on the stream.</param>
public sealed record QuicMaxStreamDataFrame(ulong StreamId, ulong MaximumStreamData) : QuicFrame
{
    internal static QuicMaxStreamDataFrame ReadFrom(QuicReader reader, byte frameType) =>
        new(reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger());

    internal override void WriteTo(QuicWriter writer)
    {
        writer.WriteByte(QuicFrameTypes.MaxStreamData);
        writer.WriteVariableLengthInteger(StreamId);
        writer.WriteVariableLengthInteger(MaximumStreamData);
    }
}
