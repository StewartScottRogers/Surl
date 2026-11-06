namespace Surl.Quic;

/// <summary>A <c>MAX_DATA</c> frame (type <c>0x10</c>, RFC 9000 section 19.9): the connection-level flow control limit.</summary>
/// <param name="MaximumData">The most data, in bytes, the peer may send on the connection.</param>
public sealed record QuicMaxDataFrame(ulong MaximumData) : QuicFrame
{
    internal static QuicMaxDataFrame ReadFrom(QuicReader reader, byte frameType) => new(reader.ReadVariableLengthInteger());

    internal override void WriteTo(QuicWriter writer)
    {
        writer.WriteByte(QuicFrameTypes.MaxData);
        writer.WriteVariableLengthInteger(MaximumData);
    }
}
