namespace Surl.Dtls;

[TestClass]
public sealed class DtlsRecordWriterTests
{
    [TestMethod]
    public void Seal_PlaintextEpoch_WritesTheHeaderThenThePlaintext()
    {
        var writer = new DtlsRecordWriter(epoch: 0, protection: null);

        var record = writer.Seal(DtlsContentType.Handshake, [0x01, 0x02, 0x03]);

        CollectionAssert.AreEqual(Convert.FromHexString("16FEFD0000000000000000" + "0003" + "010203"), record);
    }

    [TestMethod]
    public void Seal_TwoRecords_NumbersThemFromZero()
    {
        var writer = new DtlsRecordWriter(epoch: 0, protection: null);

        writer.Seal(DtlsContentType.Handshake, []);
        var second = writer.Seal(DtlsContentType.Handshake, []);

        DtlsRecordHeader.TryRead(second, out var header);
        Assert.AreEqual(1, header.SequenceNumber);
        Assert.AreEqual(2, writer.NextSequenceNumber);
    }

    [TestMethod]
    public void Seal_ProtectedEpoch_WritesTheProtectedFragmentAndItsLength()
    {
        using var protection = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, new byte[16], new byte[4]);
        var writer = new DtlsRecordWriter(epoch: 1, protection);

        var record = writer.Seal(DtlsContentType.ApplicationData, [0x2A]);

        DtlsRecordHeader.TryRead(record, out var header);
        Assert.AreEqual(1, header.Epoch);
        Assert.AreEqual(1 + 8 + 16, header.Length);
        Assert.HasCount(DtlsRecordHeader.Size + header.Length, record);
        Assert.IsTrue(protection.TryOpen(header, record.AsSpan(DtlsRecordHeader.Size), out var plaintext));
        CollectionAssert.AreEqual(new byte[] { 0x2A }, plaintext);
    }

    [TestMethod]
    public void Seal_PlaintextLongerThanARecordCarries_Throws()
    {
        var writer = new DtlsRecordWriter(epoch: 0, protection: null);

        Assert.ThrowsExactly<ArgumentException>(
            () => writer.Seal(DtlsContentType.ApplicationData, new byte[DtlsRecordWriter.MaximumPlaintextLength + 1]));
    }

    [TestMethod]
    public void Seal_LastSequenceNumber_IsUsedOnceThenRefused()
    {
        var writer = new DtlsRecordWriter(epoch: 1, protection: null, DtlsRecordHeader.MaximumSequenceNumber);

        var last = writer.Seal(DtlsContentType.Alert, [0x01, 0x00]);
        var refusal = Assert.ThrowsExactly<DtlsSequenceNumberExhaustedException>(
            () => writer.Seal(DtlsContentType.Alert, [0x01, 0x00]));

        DtlsRecordHeader.TryRead(last, out var header);
        Assert.AreEqual(DtlsRecordHeader.MaximumSequenceNumber, header.SequenceNumber);
        Assert.AreEqual((ushort)1, refusal.Epoch);
    }
}
