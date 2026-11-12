namespace Surl.Content;

[TestClass]
public sealed class Crc32cChecksumTests
{
    [TestMethod]
    public void Append_NoBytes_KeepsTheChecksum()
    {
        Assert.AreEqual(0u, Crc32cChecksum.Append(0, []));
        Assert.AreEqual(7u, Crc32cChecksum.Append(7, []));
    }

    [TestMethod]
    public void Append_TheCheckString_IsTheCastagnoliCheckValue()
    {
        Assert.AreEqual(0xE3069283u, Crc32cChecksum.Append(0, "123456789"u8));
    }

    [TestMethod]
    public void Append_Hello_IsKnown()
    {
        Assert.AreEqual(0x9a71bb4cu, Crc32cChecksum.Append(0, "hello"u8));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(8)]
    [DataRow(11)]
    [DataRow(25)]
    public void Append_InTwoPieces_IsTheChecksumOfTheWhole(int split)
    {
        byte[] bytes = [.. "The quick brown fox jumps over the lazy dog"u8];

        var pieces = Crc32cChecksum.Append(Crc32cChecksum.Append(0, bytes.AsSpan(0, split)), bytes.AsSpan(split));

        Assert.AreEqual(Crc32cChecksum.Append(0, bytes), pieces);
    }
}
