namespace Surl.Protocol.Socks;

[TestClass]
public sealed class SocksBerHeaderTests
{
    [TestMethod]
    [DataRow(new byte[] { 0x04, 0x05 }, 2, 7L, DisplayName = "short form")]
    [DataRow(new byte[] { 0x04, 0x81, 0xC8 }, 3, 203L, DisplayName = "one length octet")]
    [DataRow(new byte[] { 0x04, 0x84, 0xFF, 0xFF, 0xFF, 0xFF }, 6, 4294967301L, DisplayName = "four length octets")]
    public void TryRead_AWholeHeader_GivesItsTagAndWhereItsValueLies(byte[] bytes, int valueStart, long end)
    {
        Assert.AreEqual(SocksBerRead.Read, SocksBerHeader.TryRead(bytes, 0, out var header));
        Assert.AreEqual(new SocksBerHeader(0x04, valueStart, end), header);
    }

    [TestMethod]
    [DataRow(new byte[] { }, nameof(SocksBerRead.CutShort))]
    [DataRow(new byte[] { 0x04 }, nameof(SocksBerRead.CutShort))]
    [DataRow(new byte[] { 0x04, 0x82, 0x01 }, nameof(SocksBerRead.CutShort))]
    [DataRow(new byte[] { 0x04, 0x80 }, nameof(SocksBerRead.Unreadable))]
    [DataRow(new byte[] { 0x04, 0x85 }, nameof(SocksBerRead.Unreadable))]
    public void TryRead_NoWholeReadableHeader_SaysWhy(byte[] bytes, string expected) =>
        Assert.AreEqual(expected, SocksBerHeader.TryRead(bytes, 0, out _).ToString());
}
