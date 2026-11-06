namespace Surl.Quic;

/// <summary>A <c>RETIRE_CONNECTION_ID</c> frame (type <c>0x19</c>, RFC 9000 section 19.16): the sender stops using a connection ID it was given.</summary>
/// <param name="SequenceNumber">The sequence number of the connection ID being retired.</param>
public sealed record QuicRetireConnectionIdFrame(ulong SequenceNumber) : QuicFrame
{
    internal static QuicRetireConnectionIdFrame ReadFrom(QuicReader reader, byte frameType) => new(reader.ReadVariableLengthInteger());

    internal override void WriteTo(QuicWriter writer)
    {
        writer.WriteByte(QuicFrameTypes.RetireConnectionId);
        writer.WriteVariableLengthInteger(SequenceNumber);
    }
}
