namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapBlockOption"/>: NUM, M and SZX in one uint option value (RFC 7959 section 2.2).
/// </summary>
[TestClass]
public sealed class CoapBlockOptionTests
{
    [TestMethod]
    [DataRow(new byte[0], 0u, false, 0, "0/0/16")]
    [DataRow(new byte[] { 0x0E }, 0u, true, 6, "0/1/1024")]
    [DataRow(new byte[] { 0x23 }, 2u, false, 3, "2/0/128")]
    [DataRow(new byte[] { 0x01, 0x0A }, 16u, true, 2, "16/1/64")]
    [DataRow(new byte[] { 0xFF, 0xFF, 0xF6 }, 0xFFFFFu, false, 6, "1048575/0/1024")]
    public void Read_AndToOption_RoundTripTheValue(byte[] value, uint number, bool more, int sizeExponent, string text)
    {
        var block = CoapBlockOption.Read(new CoapOption(CoapOptionNumber.Block2, value));

        Assert.AreEqual(new CoapBlockOption(number, more, sizeExponent), block);
        Assert.AreEqual(text, block.ToString());
        CollectionAssert.AreEqual(value, block.ToOption(CoapOptionNumber.Block2).Value.ToArray());
        Assert.AreEqual(CoapOptionNumber.Block1, block.ToOption(CoapOptionNumber.Block1).Number);
    }

    [TestMethod]
    public void SizeAndOffset_AreTheBlockSizeAndNumTimesIt()
    {
        var block = new CoapBlockOption(3, false, 2);

        Assert.AreEqual(64, block.Size);
        Assert.AreEqual(192L, block.Offset);
        Assert.AreEqual(new CoapBlockOption(6, true, 1), CoapBlockOption.At(192, true, 1));
    }
}
