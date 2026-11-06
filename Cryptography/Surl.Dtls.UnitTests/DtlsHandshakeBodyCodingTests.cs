namespace Surl.Dtls;

[TestClass]
public sealed class DtlsHandshakeBodyCodingTests
{
    [TestMethod]
    public void ReadUInt16_OneByteLeft_ThrowsDecodeError()
    {
        Assert.ThrowsExactly<DtlsDecodeErrorException>(() => new DtlsHandshakeBodyReader([0x01]).ReadUInt16());
    }

    [TestMethod]
    [DataRow("00", 1, 4)]
    [DataRow("05AABBCCDDEE", 1, 4)]
    public void ReadVector8_LengthOutsideItsBounds_ThrowsDecodeError(string hex, int minimum, int maximum)
    {
        var bytes = Convert.FromHexString(hex);

        Assert.ThrowsExactly<DtlsDecodeErrorException>(() => new DtlsHandshakeBodyReader(bytes).ReadVector8(minimum, maximum));
    }

    [TestMethod]
    public void ReadVector24_LengthPastTheEnd_ThrowsDecodeError()
    {
        var bytes = Convert.FromHexString("000004AABB");

        Assert.ThrowsExactly<DtlsDecodeErrorException>(() => new DtlsHandshakeBodyReader(bytes).ReadVector24(0, 100));
    }

    [TestMethod]
    public void ReadUInt16Vector16_OddLength_ThrowsDecodeError()
    {
        var bytes = Convert.FromHexString("0003C02FC0");

        Assert.ThrowsExactly<DtlsDecodeErrorException>(() => new DtlsHandshakeBodyReader(bytes).ReadUInt16Vector16(0, 100));
    }

    [TestMethod]
    public void ExpectEnd_BytesLeft_ThrowsDecodeError()
    {
        Assert.ThrowsExactly<DtlsDecodeErrorException>(() => new DtlsHandshakeBodyReader([0x00]).ExpectEnd());
    }

    [TestMethod]
    public void WriteFixed_WrongLength_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DtlsHandshakeBodyWriter().WriteFixed([1, 2], 3));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(5)]
    public void WriteVector8_LengthOutsideItsBounds_Throws(int length)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DtlsHandshakeBodyWriter().WriteVector8(new byte[length], 1, 4));
    }

    [TestMethod]
    public void WriteVector24_Value_WritesAThreeByteLengthThenTheBytes()
    {
        var writer = new DtlsHandshakeBodyWriter();

        writer.WriteVector24([0xAA, 0xBB], 0, 100);

        CollectionAssert.AreEqual(Convert.FromHexString("000002AABB"), writer.ToArray());
    }
}
