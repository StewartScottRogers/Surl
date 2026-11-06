namespace Surl.Quic;

/// <summary>A <c>HANDSHAKE_DONE</c> frame (type <c>0x1e</c>, RFC 9000 section 19.20): the server's confirmation of the handshake.</summary>
public sealed record QuicHandshakeDoneFrame : QuicFrame
{
    internal static QuicHandshakeDoneFrame ReadFrom(QuicReader reader, byte frameType) => new();

    internal override void WriteTo(QuicWriter writer) => writer.WriteByte(QuicFrameTypes.HandshakeDone);
}
