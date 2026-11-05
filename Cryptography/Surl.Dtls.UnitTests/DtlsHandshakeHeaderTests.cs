namespace Surl.Dtls;

[TestClass]
public sealed class DtlsHandshakeHeaderTests
{
    // RFC 6347 section 4.2.2: msg_type, 24-bit length, message_seq, 24-bit fragment_offset, 24-bit fragment_length.
    private static readonly byte[] CertificateHeader = Convert.FromHexString("0B0012340005000100000200");

    [TestMethod]
    public void TryRead_TwelveBytes_ReadsEveryField()
    {
        var read = DtlsHandshakeHeader.TryRead(CertificateHeader, out var header);

        Assert.IsTrue(read);
        Assert.AreEqual(
            new DtlsHandshakeHeader(DtlsHandshakeType.Certificate, Length: 0x1234, MessageSeq: 5, FragmentOffset: 0x100, FragmentLength: 0x200),
            header);
    }

    [TestMethod]
    public void TryRead_ElevenBytes_ReadsNothing()
    {
        var read = DtlsHandshakeHeader.TryRead(CertificateHeader.AsSpan(0, 11), out var header);

        Assert.IsFalse(read);
        Assert.AreEqual(default, header);
    }

    [TestMethod]
    public void Write_Header_WritesTheTwelveBytes()
    {
        var header = new DtlsHandshakeHeader(DtlsHandshakeType.Certificate, 0x1234, 5, 0x100, 0x200);
        var written = new byte[DtlsHandshakeHeader.Size];

        header.Write(written);

        CollectionAssert.AreEqual(CertificateHeader, written);
    }

    [TestMethod]
    public void Write_DestinationShorterThanAHeader_Throws()
    {
        var header = new DtlsHandshakeHeader(DtlsHandshakeType.Finished, 12, 0, 0, 12);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => header.Write(new byte[11]));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(1 << 24)]
    public void Write_LengthOutside24Bits_Throws(int length)
    {
        var header = new DtlsHandshakeHeader(DtlsHandshakeType.Finished, length, 0, 0, 0);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => header.Write(new byte[DtlsHandshakeHeader.Size]));
    }

    [TestMethod]
    public void ToUnfragmentedBytes_Message_IsOneFragmentFromOffsetZeroThenTheBody()
    {
        var message = new DtlsHandshakeMessage(DtlsHandshakeType.Finished, 4, Convert.FromHexString("AABBCC"));

        var bytes = message.ToUnfragmentedBytes();

        CollectionAssert.AreEqual(Convert.FromHexString("14000003" + "0004" + "000000" + "000003" + "AABBCC"), bytes);
    }
}
