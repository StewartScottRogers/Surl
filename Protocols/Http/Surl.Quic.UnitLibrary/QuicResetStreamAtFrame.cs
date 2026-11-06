namespace Surl.Quic;

/// <summary>
/// A <c>RESET_STREAM_AT</c> frame (type <c>0x24</c>, draft-ietf-quic-reliable-stream-reset-09
/// section 4): the sender abandons a stream's sending part, but still delivers every byte below
/// <paramref name="ReliableSize"/>. A <c>RESET_STREAM</c> is one with a Reliable Size of 0.
/// </summary>
/// <param name="StreamId">The stream being reset.</param>
/// <param name="ApplicationErrorCode">Why, in the application protocol's error codes.</param>
/// <param name="FinalSize">The stream's final size, in bytes.</param>
/// <param name="ReliableSize">How many bytes from the start of the stream are still delivered; never above <paramref name="FinalSize"/>.</param>
public sealed record QuicResetStreamAtFrame(ulong StreamId, ulong ApplicationErrorCode, ulong FinalSize, ulong ReliableSize) : QuicFrame
{
    internal static QuicResetStreamAtFrame ReadFrom(QuicReader reader, byte frameType)
    {
        var frame = new QuicResetStreamAtFrame(
            reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger());
        return frame.ReliableSize > frame.FinalSize
            ? throw reader.Fault(
                QuicTransportErrorCode.FrameEncodingError,
                $"RESET_STREAM_AT's Reliable Size {frame.ReliableSize} is above its Final Size {frame.FinalSize} (draft-ietf-quic-reliable-stream-reset-09 section 4).")
            : frame;
    }

    internal override void WriteTo(QuicWriter writer)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ReliableSize, FinalSize, nameof(ReliableSize));
        writer.WriteByte(QuicFrameTypes.ResetStreamAt);
        writer.WriteVariableLengthInteger(StreamId);
        writer.WriteVariableLengthInteger(ApplicationErrorCode);
        writer.WriteVariableLengthInteger(FinalSize);
        writer.WriteVariableLengthInteger(ReliableSize);
    }
}
