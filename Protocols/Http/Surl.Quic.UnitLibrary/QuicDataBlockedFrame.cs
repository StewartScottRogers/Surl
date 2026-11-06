namespace Surl.Quic;

/// <summary>A <c>DATA_BLOCKED</c> frame (type <c>0x14</c>, RFC 9000 section 19.12): the sender has data to send and is held by the connection limit.</summary>
/// <param name="MaximumData">The connection limit the sender is held at.</param>
public sealed record QuicDataBlockedFrame(ulong MaximumData) : QuicFrame
{
    internal static QuicDataBlockedFrame ReadFrom(QuicReader reader, byte frameType) => new(reader.ReadVariableLengthInteger());

    internal override void WriteTo(QuicWriter writer)
    {
        writer.WriteByte(QuicFrameTypes.DataBlocked);
        writer.WriteVariableLengthInteger(MaximumData);
    }
}
