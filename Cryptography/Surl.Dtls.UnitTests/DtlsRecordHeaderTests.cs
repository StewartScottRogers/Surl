namespace Surl.Dtls;

[TestClass]
public sealed class DtlsRecordHeaderTests
{
    // RFC 6347 section 4.1: type, version {254, 253}, epoch, 48-bit sequence number, length.
    private static readonly byte[] HandshakeHeader = Convert.FromHexString("16FEFD000100000000ABCD0123");

    [TestMethod]
    public void TryRead_ThirteenBytes_ReadsEveryField()
    {
        var read = DtlsRecordHeader.TryRead(HandshakeHeader, out var header);

        Assert.IsTrue(read);
        Assert.AreEqual(
            new DtlsRecordHeader(DtlsContentType.Handshake, 0xFEFD, Epoch: 1, SequenceNumber: 0xABCD, Length: 0x0123),
            header);
    }

    [TestMethod]
    public void TryRead_HighestEpochAndSequenceNumber_KeepsThemApart()
    {
        var bytes = Convert.FromHexString("17FEFDFFFFFFFFFFFFFFFF0000");

        DtlsRecordHeader.TryRead(bytes, out var header);

        Assert.AreEqual(ushort.MaxValue, header.Epoch);
        Assert.AreEqual(DtlsRecordHeader.MaximumSequenceNumber, header.SequenceNumber);
    }

    [TestMethod]
    public void TryRead_TwelveBytes_ReadsNothing()
    {
        var read = DtlsRecordHeader.TryRead(HandshakeHeader.AsSpan(0, 12), out var header);

        Assert.IsFalse(read);
        Assert.AreEqual(default, header);
    }

    [TestMethod]
    public void Write_Header_WritesTheThirteenBytes()
    {
        var header = new DtlsRecordHeader(DtlsContentType.Handshake, 0xFEFD, Epoch: 1, SequenceNumber: 0xABCD, Length: 0x0123);
        var written = new byte[DtlsRecordHeader.Size];

        header.Write(written);

        CollectionAssert.AreEqual(HandshakeHeader, written);
    }

    [TestMethod]
    [DataRow(-1L)]
    [DataRow(1L << 48)]
    public void Write_SequenceNumberOutside48Bits_Throws(long sequenceNumber)
    {
        var header = new DtlsRecordHeader(DtlsContentType.Alert, 0xFEFD, Epoch: 0, sequenceNumber, Length: 0);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => header.Write(new byte[DtlsRecordHeader.Size]));
    }

    [TestMethod]
    public void Write_DestinationShorterThanAHeader_Throws()
    {
        var header = new DtlsRecordHeader(DtlsContentType.Alert, 0xFEFD, Epoch: 0, SequenceNumber: 0, Length: 0);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => header.Write(new byte[12]));
    }
}
