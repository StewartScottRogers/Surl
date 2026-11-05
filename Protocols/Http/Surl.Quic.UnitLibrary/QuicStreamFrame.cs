namespace Surl.Quic;

/// <summary>
/// A <c>STREAM</c> frame (types <c>0x08</c> to <c>0x0f</c>, RFC 9000 section 19.8): stream
/// data at an offset. The type's OFF bit is written when <see cref="Offset"/> is not 0, its LEN
/// bit when <see cref="HasLength"/> is set, and its FIN bit when <see cref="Fin"/> is set. A frame
/// without a length runs to the end of the packet, so it must be the packet's last frame. An
/// offset plus length above 2^62-1 is <see cref="QuicTransportErrorCode.FrameEncodingError"/>.
/// </summary>
/// <param name="StreamId">The stream the data belongs to.</param>
/// <param name="Offset">Where <paramref name="Data"/> starts in the stream.</param>
/// <param name="Data">The stream bytes; when read, a slice of the payload.</param>
/// <param name="Fin">Whether the stream ends with this frame's data.</param>
/// <param name="HasLength">Whether the frame carries its length rather than running to the end of the packet.</param>
public sealed record QuicStreamFrame(ulong StreamId, ulong Offset, ReadOnlyMemory<byte> Data, bool Fin, bool HasLength = true) : QuicFrame
{
    internal static QuicStreamFrame ReadFrom(QuicReader reader, byte frameType)
    {
        var streamId = reader.ReadVariableLengthInteger();
        var offset = HasBit(frameType, QuicFrameTypes.StreamOffsetBit) ? reader.ReadVariableLengthInteger() : 0;
        var hasLength = HasBit(frameType, QuicFrameTypes.StreamLengthBit);
        var data = hasLength ? reader.ReadLengthPrefixedBytes() : reader.ReadRemaining();
        return offset + (ulong)data.Length > QuicVariableLengthInteger.MaxValue
            ? throw reader.Fault(QuicTransportErrorCode.FrameEncodingError, "A STREAM frame ends beyond offset 2^62-1.")
            : new QuicStreamFrame(streamId, offset, data, HasBit(frameType, QuicFrameTypes.StreamFinBit), hasLength);
    }

    internal override void WriteTo(QuicWriter writer)
    {
        writer.WriteByte(FrameType());
        writer.WriteVariableLengthInteger(StreamId);
        WriteOffsetAndData(writer);
    }

    private byte FrameType() => (byte)(QuicFrameTypes.Stream
        | (Offset != 0 ? QuicFrameTypes.StreamOffsetBit : 0)
        | (HasLength ? QuicFrameTypes.StreamLengthBit : 0)
        | (Fin ? QuicFrameTypes.StreamFinBit : 0));

    private void WriteOffsetAndData(QuicWriter writer)
    {
        if (Offset != 0)
        {
            writer.WriteVariableLengthInteger(Offset);
        }

        if (HasLength)
        {
            writer.WriteVariableLengthInteger((ulong)Data.Length);
        }

        writer.WriteBytes(Data.Span);
    }

    private static bool HasBit(byte frameType, byte bit) => (frameType & bit) != 0;
}
