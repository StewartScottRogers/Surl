namespace Surl.Quic;

/// <summary>Recovers and truncates packet numbers (RFC 9000 Appendix A.3).</summary>
[TestClass]
public sealed class QuicPacketNumberTests
{
    [TestMethod]
    public void Decode_Rfc9000AppendixA3Example_GivesItsPacketNumber()
    {
        var packetNumber = QuicPacketNumber.Decode(0xa82f30eb, 0x9b32, 2);

        Assert.AreEqual(0xa82f9b32UL, packetNumber);
    }

    [TestMethod]
    [DataRow(0UL, 0xffU, 1, 0xffUL, DisplayName = "Above the window but no window below it")]
    [DataRow(300UL, 0x01U, 1, 257UL, DisplayName = "Within half a window")]
    [DataRow(511UL, 0x00U, 1, 512UL, DisplayName = "Wrapped past the window")]
    [DataRow(257UL, 0xffU, 1, 255UL, DisplayName = "Belongs to the window below")]
    [DataRow((1UL << 62) - 1, 0x00U, 1, (1UL << 62) - 256, DisplayName = "No window above 2^62")]
    public void Decode_TruncatedPacketNumber_GivesTheClosestFullPacketNumber(ulong expected, uint truncated, int length, ulong decoded)
    {
        var packetNumber = QuicPacketNumber.Decode(expected, truncated, length);

        Assert.AreEqual(decoded, packetNumber);
    }

    [TestMethod]
    [DataRow(0UL, 0)]
    [DataRow(0UL, 5)]
    [DataRow((1UL << 62) + 1, 1)]
    public void Decode_LengthNotOneToFourOrExpectedAbove2To62_Throws(ulong expected, int length)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicPacketNumber.Decode(expected, 0, length));
    }

    [TestMethod]
    public void Truncate_PacketNumber_KeepsItsLowBytes()
    {
        var truncated = QuicPacketNumber.Truncate(Rfc9001AppendixA.ChaChaPacketNumber, 3);

        Assert.AreEqual(49140U, truncated);
    }
}
