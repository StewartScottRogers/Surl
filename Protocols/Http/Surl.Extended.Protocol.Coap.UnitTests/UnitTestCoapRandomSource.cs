namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// A fixed <see cref="ICoapRandomSource"/>: the first Message ID 0xFFFF, every fraction 0, so the
/// first retransmission timeout is exactly ACK_TIMEOUT, and every random byte 0xAB.
/// </summary>
internal sealed class UnitTestCoapRandomSource : ICoapRandomSource
{
    public ushort DrawMessageId() => 0xFFFF;

    public double DrawFraction() => 0;

    public void Fill(Span<byte> bytes) => bytes.Fill(0xAB);
}
