namespace Surl.Dtls;

[TestClass]
public sealed class DtlsRecordReaderTests
{
    private static readonly byte[] Key = new byte[16];
    private static readonly byte[] Salt = [1, 2, 3, 4];

    [TestMethod]
    public void ReadDatagram_TwoPlaintextRecords_ReadsBothInOrder()
    {
        var writer = new DtlsRecordWriter(epoch: 0, protection: null);
        byte[] datagram = [.. writer.Seal(DtlsContentType.Handshake, [0x0A]), .. writer.Seal(DtlsContentType.Alert, [0x0B])];
        var reader = new DtlsRecordReader(epoch: 0, protection: null);

        var read = reader.ReadDatagram(datagram);

        Assert.HasCount(2, read.Records);
        Assert.IsEmpty(read.Drops);
        Assert.AreEqual(DtlsContentType.Handshake, read.Records[0].Header.ContentType);
        CollectionAssert.AreEqual(new byte[] { 0x0B }, read.Records[1].Plaintext);
        Assert.AreEqual(1, read.Records[1].Header.SequenceNumber);
    }

    [TestMethod]
    public void ReadDatagram_ProtectedRecord_ReturnsItsPlaintext()
    {
        using var sending = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt);
        using var receiving = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt);
        var record = new DtlsRecordWriter(epoch: 1, sending).Seal(DtlsContentType.ApplicationData, [0x2A]);
        var reader = new DtlsRecordReader(epoch: 1, receiving);

        var read = reader.ReadDatagram(record);

        Assert.HasCount(1, read.Records);
        CollectionAssert.AreEqual(new byte[] { 0x2A }, read.Records[0].Plaintext);
    }

    [TestMethod]
    public void ReadDatagram_ReplayedRecord_IsDropped()
    {
        var record = new DtlsRecordWriter(epoch: 0, protection: null).Seal(DtlsContentType.Handshake, [0x01]);
        var reader = new DtlsRecordReader(epoch: 0, protection: null);
        reader.ReadDatagram(record);

        var replay = reader.ReadDatagram(record);

        Assert.IsEmpty(replay.Records);
        CollectionAssert.AreEqual(new[] { DtlsRecordDropReason.Replayed }, replay.Drops.ToArray());
    }

    [TestMethod]
    public void ReadDatagram_RecordOlderThanTheWindow_IsDropped()
    {
        var writer = new DtlsRecordWriter(epoch: 0, protection: null);
        var oldest = writer.Seal(DtlsContentType.Handshake, [0x01]);
        var newer = new DtlsRecordWriter(epoch: 0, protection: null, nextSequenceNumber: DtlsReplayWindow.Size)
            .Seal(DtlsContentType.Handshake, [0x02]);
        var reader = new DtlsRecordReader(epoch: 0, protection: null);
        reader.ReadDatagram(newer);

        var late = reader.ReadDatagram(oldest);

        Assert.IsEmpty(late.Records);
        CollectionAssert.AreEqual(new[] { DtlsRecordDropReason.Replayed }, late.Drops.ToArray());
    }

    [TestMethod]
    public void ReadDatagram_RecordWithACorruptedTag_IsDroppedAndTheNextRecordStillRead()
    {
        using var sending = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt);
        using var receiving = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt);
        var writer = new DtlsRecordWriter(epoch: 1, sending);
        var corrupted = writer.Seal(DtlsContentType.ApplicationData, [0x01]);
        corrupted[^1] ^= 0x80;
        var intact = writer.Seal(DtlsContentType.ApplicationData, [0x02]);
        var reader = new DtlsRecordReader(epoch: 1, receiving);

        var read = reader.ReadDatagram([.. corrupted, .. intact]);

        CollectionAssert.AreEqual(new[] { DtlsRecordDropReason.FailedAuthentication }, read.Drops.ToArray());
        Assert.HasCount(1, read.Records);
        CollectionAssert.AreEqual(new byte[] { 0x02 }, read.Records[0].Plaintext);
    }

    [TestMethod]
    public void ReadDatagram_CorruptedRecord_LeavesItsSequenceNumberOpen()
    {
        using var sending = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt);
        using var receiving = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt);
        var record = new DtlsRecordWriter(epoch: 1, sending).Seal(DtlsContentType.ApplicationData, [0x01]);
        var corrupted = (byte[])record.Clone();
        corrupted[^1] ^= 0x80;
        var reader = new DtlsRecordReader(epoch: 1, receiving);
        reader.ReadDatagram(corrupted);

        var read = reader.ReadDatagram(record);

        Assert.HasCount(1, read.Records);
    }

    [TestMethod]
    public void ReadDatagram_RecordOfAnotherEpoch_IsDropped()
    {
        var record = new DtlsRecordWriter(epoch: 0, protection: null).Seal(DtlsContentType.Handshake, [0x01]);
        using var receiving = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt);
        var reader = new DtlsRecordReader(epoch: 1, receiving);

        var read = reader.ReadDatagram(record);

        Assert.IsEmpty(read.Records);
        CollectionAssert.AreEqual(new[] { DtlsRecordDropReason.WrongEpoch }, read.Drops.ToArray());
    }

    [TestMethod]
    public void ReadDatagram_DatagramEndingInsideAHeader_DropsTheTail()
    {
        var record = new DtlsRecordWriter(epoch: 0, protection: null).Seal(DtlsContentType.Handshake, [0x01]);
        var reader = new DtlsRecordReader(epoch: 0, protection: null);

        var read = reader.ReadDatagram([.. record, 0x16, 0xFE]);

        Assert.HasCount(1, read.Records);
        CollectionAssert.AreEqual(new[] { DtlsRecordDropReason.Truncated }, read.Drops.ToArray());
    }

    [TestMethod]
    public void ReadDatagram_DatagramShorterThanTheLengthItsHeaderGives_DropsTheRecord()
    {
        var record = new DtlsRecordWriter(epoch: 0, protection: null).Seal(DtlsContentType.Handshake, [0x01, 0x02]);
        var reader = new DtlsRecordReader(epoch: 0, protection: null);

        var read = reader.ReadDatagram(record.AsSpan(0, record.Length - 1));

        Assert.IsEmpty(read.Records);
        CollectionAssert.AreEqual(new[] { DtlsRecordDropReason.Truncated }, read.Drops.ToArray());
    }

    [TestMethod]
    public void ReadDatagram_PlaintextRecordLongerThanTwoToTheFourteen_IsDropped()
    {
        var length = DtlsRecordWriter.MaximumPlaintextLength + 1;
        var datagram = new byte[DtlsRecordHeader.Size + length];
        new DtlsRecordHeader(DtlsContentType.ApplicationData, 0xFEFD, 0, 0, (ushort)length).Write(datagram);
        var reader = new DtlsRecordReader(epoch: 0, protection: null);

        var read = reader.ReadDatagram(datagram);

        Assert.IsEmpty(read.Records);
        CollectionAssert.AreEqual(new[] { DtlsRecordDropReason.TooLong }, read.Drops.ToArray());
    }

    [TestMethod]
    public void ReadDatagram_ProtectedRecordWhosePlaintextIsLongerThanTwoToTheFourteen_IsDropped()
    {
        using var sending = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt);
        using var receiving = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt);
        var header = new DtlsRecordHeader(DtlsContentType.ApplicationData, 0xFEFD, Epoch: 1, SequenceNumber: 0, Length: 0);
        var fragment = sending.Seal(header, new byte[DtlsRecordWriter.MaximumPlaintextLength + 1]);
        var datagram = new byte[DtlsRecordHeader.Size + fragment.Length];
        (header with { Length = (ushort)fragment.Length }).Write(datagram);
        fragment.CopyTo(datagram, DtlsRecordHeader.Size);
        var reader = new DtlsRecordReader(epoch: 1, receiving);

        var read = reader.ReadDatagram(datagram);

        Assert.IsEmpty(read.Records);
        CollectionAssert.AreEqual(new[] { DtlsRecordDropReason.TooLong }, read.Drops.ToArray());
    }
}
