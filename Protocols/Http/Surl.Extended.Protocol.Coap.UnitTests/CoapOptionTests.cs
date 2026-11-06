namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapOption"/>: option numbers and lengths, criticality, and uint and string values (RFC 7252 sections 3.1, 3.2 and 5.4.6).
/// </summary>
[TestClass]
public sealed class CoapOptionTests
{
    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(65536, 0)]
    [DataRow(1, CoapOption.MaxValueLength + 1)]
    public void OutOfRange_Throws(int number, int length) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CoapOption(number, new byte[length]));

    [TestMethod]
    [DataRow(11, true)]
    [DataRow(12, false)]
    public void IsCritical_WhenTheNumberIsOdd(int number, bool critical) =>
        Assert.AreEqual(critical, new CoapOption(number, ReadOnlyMemory<byte>.Empty).IsCritical);

    [TestMethod]
    [DataRow(0u, new byte[0])]
    [DataRow(1u, new byte[] { 0x01 })]
    [DataRow(256u, new byte[] { 0x01, 0x00 })]
    [DataRow(0x12345678u, new byte[] { 0x12, 0x34, 0x56, 0x78 })]
    public void FromUInt_WritesTheFewestBytes(uint value, byte[] expected)
    {
        var option = CoapOption.FromUInt(12, value);

        CollectionAssert.AreEqual(expected, option.Value.ToArray());
        Assert.AreEqual(value, option.ReadUInt());
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0x00, 0x01 }, 1u)]
    [DataRow(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF }, uint.MaxValue)]
    [DataRow(new byte[] { 0x00 }, 0u)]
    public void ReadUInt_ReadsLeadingZeroBytes(byte[] value, uint expected) =>
        Assert.AreEqual(expected, new CoapOption(14, value).ReadUInt());

    [TestMethod]
    public void ReadUInt_FiveSignificantBytes_Throws() =>
        Assert.ThrowsExactly<FormatException>(() => new CoapOption(14, new byte[] { 0x01, 0, 0, 0, 0 }).ReadUInt());

    [TestMethod]
    public void FromString_IsTheUtf8Bytes()
    {
        CollectionAssert.AreEqual(new byte[] { 0x63, 0xC3, 0xA9 }, CoapOption.FromString(11, "cé").Value.ToArray());
        Assert.ThrowsExactly<ArgumentNullException>(() => CoapOption.FromString(11, null!));
    }
}
