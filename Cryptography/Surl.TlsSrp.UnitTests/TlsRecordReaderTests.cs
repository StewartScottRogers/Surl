namespace Surl.TlsSrp;

/// <summary>
/// RFC 5246 section 6.2: a plaintext record is at most 2^14 bytes, a protected one at most
/// 2^14 + 2048, and either over its limit is <c>record_overflow</c>.
/// </summary>
[TestClass]
public sealed class TlsRecordReaderTests
{
    [TestMethod]
    public async Task ReadAsync_PlaintextClientHelloRecord_KeepsItsTypeVersionAndFragment()
    {
        using var stream = new MemoryStream(Convert.FromHexString("1603010003010203"));
        var reader = new TlsRecordReader(stream);

        var record = await reader.ReadAsync(CancellationToken.None);

        Assert.AreEqual(new TlsRecord(TlsContentType.Handshake, 0x0301, record!.Fragment), record);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, record.Fragment);
        Assert.IsFalse(reader.IsProtected);
    }

    [TestMethod]
    public async Task ReadAsync_StreamEndsBetweenRecords_IsNull()
    {
        using var stream = new MemoryStream();

        Assert.IsNull(await new TlsRecordReader(stream).ReadAsync(CancellationToken.None));
    }

    [TestMethod]
    [DataRow("1603")]
    [DataRow("1603030004010203")]
    public async Task ReadAsync_StreamEndsInsideARecord_Throws(string bytes)
    {
        using var stream = new MemoryStream(Convert.FromHexString(bytes));

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => new TlsRecordReader(stream).ReadAsync(CancellationToken.None));
    }

    [TestMethod]
    [DataRow("13")]
    [DataRow("18")]
    [DataRow("00")]
    public async Task ReadAsync_UnknownContentType_IsUnexpectedMessage(string type)
    {
        using var stream = new MemoryStream(Convert.FromHexString(type + "0303000100"));

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => new TlsRecordReader(stream).ReadAsync(CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, alert.Description);
    }

    [TestMethod]
    public async Task ReadAsync_PlaintextRecordOf2To14Bytes_IsRead()
    {
        using var stream = new MemoryStream([.. Convert.FromHexString("1703034000"), .. new byte[TlsRecordFormat.MaxPlaintextLength]]);

        var record = await new TlsRecordReader(stream).ReadAsync(CancellationToken.None);

        Assert.HasCount(TlsRecordFormat.MaxPlaintextLength, record!.Fragment);
    }

    [TestMethod]
    public async Task ReadAsync_PlaintextRecordOver2To14Bytes_IsRecordOverflow()
    {
        using var stream = new MemoryStream([.. Convert.FromHexString("1703034001"), .. new byte[TlsRecordFormat.MaxPlaintextLength + 1]]);

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => new TlsRecordReader(stream).ReadAsync(CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.RecordOverflow, alert.Description);
    }

    [TestMethod]
    public async Task ReadAsync_ProtectedRecordOver2To14Plus2048Bytes_IsRecordOverflow()
    {
        using var stream = new MemoryStream([.. Convert.FromHexString("1703034801"), .. new byte[TlsRecordFormat.MaxCiphertextLength + 1]]);
        var reader = new TlsRecordReader(stream);
        using var protection = UnitTestProtections.Aes256(encryptThenMac: false);
        reader.ChangeProtection(protection);

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => reader.ReadAsync(CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.RecordOverflow, alert.Description);
        Assert.IsTrue(reader.IsProtected);
    }

    [TestMethod]
    public async Task ReadAsync_ProtectedRecordOf2To14Plus2048Bytes_IsOpenedNotRefusedForLength()
    {
        using var stream = new MemoryStream([.. Convert.FromHexString("1703034800"), .. new byte[TlsRecordFormat.MaxCiphertextLength]]);
        var reader = new TlsRecordReader(stream);
        using var protection = UnitTestProtections.Aes256(encryptThenMac: false);
        reader.ChangeProtection(protection);

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => reader.ReadAsync(CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert.Description);
    }

    [TestMethod]
    public async Task ReadAsync_ReplayedProtectedRecord_IsBadRecordMac()
    {
        using var written = new MemoryStream();
        using var writeProtection = UnitTestProtections.Aes256(encryptThenMac: false);
        var writer = new TlsRecordWriter(written);
        writer.ChangeProtection(writeProtection);
        await writer.WriteAsync(TlsContentType.ApplicationData, new byte[] { 1, 2, 3 }, CancellationToken.None);
        var record = written.ToArray();
        using var stream = new MemoryStream([.. record, .. record]);
        var reader = new TlsRecordReader(stream);
        using var readProtection = UnitTestProtections.Aes256(encryptThenMac: false);
        reader.ChangeProtection(readProtection);
        await reader.ReadAsync(CancellationToken.None);

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => reader.ReadAsync(CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert.Description);
    }

    [TestMethod]
    public void ChangeProtection_Null_Throws()
    {
        using var stream = new MemoryStream();

        Assert.ThrowsExactly<ArgumentNullException>(() => new TlsRecordReader(stream).ChangeProtection(null!));
    }
}
