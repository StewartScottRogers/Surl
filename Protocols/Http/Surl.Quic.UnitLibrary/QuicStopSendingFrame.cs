namespace Surl.Quic;

/// <summary>A <c>STOP_SENDING</c> frame (type <c>0x05</c>, RFC 9000 section 19.5): the sender asks its peer to stop sending on a stream.</summary>
/// <param name="StreamId">The stream to stop.</param>
/// <param name="ApplicationErrorCode">Why, in the application protocol's error codes.</param>
public sealed record QuicStopSendingFrame(ulong StreamId, ulong ApplicationErrorCode) : QuicFrame
{
    internal static QuicStopSendingFrame ReadFrom(QuicReader reader, byte frameType) =>
        new(reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger());

    internal override void WriteTo(QuicWriter writer)
    {
        writer.WriteByte(QuicFrameTypes.StopSending);
        writer.WriteVariableLengthInteger(StreamId);
        writer.WriteVariableLengthInteger(ApplicationErrorCode);
    }
}
