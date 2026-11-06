namespace Surl.Quic;

/// <summary>
/// A <c>DATAGRAM</c> frame (RFC 9221 section 4): application data sent unreliably. Type
/// <c>0x30</c> has no Length field and runs to the end of its packet; type <c>0x31</c> carries
/// its length. It is ack-eliciting and counted by congestion control, but never sent again when
/// lost (section 5).
/// </summary>
/// <param name="Data">The application data; when read, a slice of the payload.</param>
/// <param name="HasLength">Whether the frame is type <c>0x31</c>, carrying its length, rather than <c>0x30</c>, running to the end of the packet.</param>
public sealed record QuicDatagramFrame(ReadOnlyMemory<byte> Data, bool HasLength = true) : QuicFrame
{
    internal static QuicDatagramFrame ReadFrom(QuicReader reader, byte frameType) =>
        frameType == QuicFrameTypes.DatagramWithLength
            ? new QuicDatagramFrame(reader.ReadLengthPrefixedBytes())
            : new QuicDatagramFrame(reader.ReadRemaining(), false);

    internal override void WriteTo(QuicWriter writer)
    {
        if (HasLength)
        {
            writer.WriteByte(QuicFrameTypes.DatagramWithLength);
            writer.WriteLengthPrefixedBytes(Data.Span);
        }
        else
        {
            writer.WriteByte(QuicFrameTypes.Datagram);
            writer.WriteBytes(Data.Span);
        }
    }
}
