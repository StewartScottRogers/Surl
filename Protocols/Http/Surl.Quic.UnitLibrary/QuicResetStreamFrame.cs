namespace Surl.Quic;

/// <summary>A <c>RESET_STREAM</c> frame (type <c>0x04</c>, RFC 9000 section 19.4): the sender abandons a stream's sending part.</summary>
/// <param name="StreamId">The stream being reset.</param>
/// <param name="ApplicationErrorCode">Why, in the application protocol's error codes.</param>
/// <param name="FinalSize">The stream's final size, in bytes.</param>
public sealed record QuicResetStreamFrame(ulong StreamId, ulong ApplicationErrorCode, ulong FinalSize) : QuicFrame
{
    internal static QuicResetStreamFrame ReadFrom(QuicReader reader, byte frameType) =>
        new(reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger());

    internal override void WriteTo(QuicWriter writer)
    {
        writer.WriteByte(QuicFrameTypes.ResetStream);
        writer.WriteVariableLengthInteger(StreamId);
        writer.WriteVariableLengthInteger(ApplicationErrorCode);
        writer.WriteVariableLengthInteger(FinalSize);
    }
}
