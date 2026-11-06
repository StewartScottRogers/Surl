namespace Surl.TlsSrp;

/// <summary>RFC 5246 section 6.2: records of at most 2^14 plaintext bytes, version <c>03 03</c>.</summary>
[TestClass]
public sealed class TlsRecordWriterTests
{
    [TestMethod]
    public async Task WriteAsync_Plaintext_IsTypeVersionLengthAndFragment()
    {
        using var stream = new MemoryStream();
        var writer = new TlsRecordWriter(stream);

        await writer.WriteAsync(TlsContentType.Handshake, new byte[] { 0x0E, 0, 0, 0 }, CancellationToken.None);

        Assert.AreEqual("16030300040E000000", Convert.ToHexString(stream.ToArray()));
        Assert.IsFalse(writer.IsProtected);
    }

    [TestMethod]
    public async Task WriteAsync_EmptyPayload_IsOneEmptyRecord()
    {
        using var stream = new MemoryStream();

        await new TlsRecordWriter(stream).WriteAsync(TlsContentType.ApplicationData, ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual("1703030000", Convert.ToHexString(stream.ToArray()));
    }

    [TestMethod]
    public async Task WriteAsync_PayloadOver2To14Bytes_IsFragmentedIntoRecordsOfAtMost2To14()
    {
        var payload = Enumerable.Range(0, (2 * TlsRecordFormat.MaxPlaintextLength) + 5).Select(value => (byte)value).ToArray();
        using var stream = new MemoryStream();

        await new TlsRecordWriter(stream).WriteAsync(TlsContentType.ApplicationData, payload, CancellationToken.None);

        var written = stream.ToArray();
        Assert.HasCount(payload.Length + (3 * TlsRecordFormat.HeaderLength), written);
        Assert.AreEqual("1703034000", Convert.ToHexString(written, 0, 5));
        Assert.AreEqual("1703034000", Convert.ToHexString(written, 16389, 5));
        Assert.AreEqual("1703030005", Convert.ToHexString(written, 32778, 5));
        CollectionAssert.AreEqual(payload[32768..], written[32783..]);
    }

    [TestMethod]
    public async Task WriteAsync_AfterChangeProtection_WritesProtectedRecordsTheReaderOpens()
    {
        var payload = Enumerable.Range(0, TlsRecordFormat.MaxPlaintextLength + 100).Select(value => (byte)value).ToArray();
        using var stream = new MemoryStream();
        var writer = new TlsRecordWriter(stream);
        using var writeProtection = UnitTestProtections.Aes256(encryptThenMac: true);
        writer.ChangeProtection(writeProtection);

        await writer.WriteAsync(TlsContentType.ApplicationData, payload, CancellationToken.None);

        stream.Position = 0;
        var reader = new TlsRecordReader(stream);
        using var readProtection = UnitTestProtections.Aes256(encryptThenMac: true);
        reader.ChangeProtection(readProtection);
        var first = await reader.ReadAsync(CancellationToken.None);
        var second = await reader.ReadAsync(CancellationToken.None);
        Assert.IsTrue(writer.IsProtected);
        CollectionAssert.AreEqual(payload, first!.Fragment.Concat(second!.Fragment).ToArray());
        Assert.AreEqual(TlsRecordFormat.Tls12Version, second.Version);
        Assert.IsNull(await reader.ReadAsync(CancellationToken.None));
    }

    [TestMethod]
    public void ChangeProtection_Null_Throws()
    {
        using var stream = new MemoryStream();

        Assert.ThrowsExactly<ArgumentNullException>(() => new TlsRecordWriter(stream).ChangeProtection(null!));
    }
}
