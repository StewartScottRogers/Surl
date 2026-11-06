namespace Surl.Quic;

/// <summary>A <c>PING</c> frame (type <c>0x01</c>, RFC 9000 section 19.2): it carries nothing and asks for an acknowledgment.</summary>
public sealed record QuicPingFrame : QuicFrame
{
    internal static QuicPingFrame ReadFrom(QuicReader reader, byte frameType) => new();

    internal override void WriteTo(QuicWriter writer) => writer.WriteByte(QuicFrameTypes.Ping);
}
