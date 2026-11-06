namespace Surl.TlsSrp;

/// <summary>RFC 5246 sections 6.2.1 and 7.4: handshake messages across records, and the client's <c>ChangeCipherSpec</c>.</summary>
[TestClass]
public sealed class TlsHandshakeMessageReaderTests
{
    [TestMethod]
    public async Task ReadAsync_MessageSplitAcrossRecordsAndTwoInOneRecord_ReadsEach()
    {
        byte[] first = UnitTestSrpClient.Handshake(TlsHandshakeType.ClientHello, [1, 2, 3]);
        byte[] second = UnitTestSrpClient.Handshake(TlsHandshakeType.ClientKeyExchange, [4]);
        byte[] third = UnitTestSrpClient.Handshake(TlsHandshakeType.Finished, [5]);
        byte[] bytes =
        [
            .. UnitTestSrpClient.Record(TlsContentType.Handshake, first[..2]),
            .. UnitTestSrpClient.Record(TlsContentType.Handshake, first[2..5]),
            .. UnitTestSrpClient.Record(TlsContentType.Handshake, [.. first[5..], .. second, .. third]),
        ];
        var reader = Reader(bytes);

        var one = await reader.ReadAsync(TlsHandshakeType.ClientHello, CancellationToken.None);
        var two = await reader.ReadAsync(TlsHandshakeType.ClientKeyExchange, CancellationToken.None);
        var three = await reader.ReadAsync(TlsHandshakeType.Finished, CancellationToken.None);

        CollectionAssert.AreEqual(first, one.Framed);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, one.Body);
        Assert.AreEqual((byte)TlsHandshakeType.ClientHello, one.Type);
        CollectionAssert.AreEqual(second, two.Framed);
        CollectionAssert.AreEqual(third, three.Framed);
    }

    [TestMethod]
    public async Task ReadAsync_MessageOverTheLimit_IsDecodeError()
    {
        var reader = Reader(UnitTestSrpClient.Record(TlsContentType.Handshake, [1, 0x02, 0x00, 0x01]));

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => reader.ReadAsync(TlsHandshakeType.ClientHello, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert.Description);
    }

    [TestMethod]
    public async Task ReadAsync_MalformedAlertRecord_IsDecodeError()
    {
        var reader = Reader(UnitTestSrpClient.Record(TlsContentType.Alert, [2]));

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => reader.ReadAsync(TlsHandshakeType.ClientHello, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert.Description);
    }

    [TestMethod]
    public async Task ReadChangeCipherSpecAsync_ItsOneByte_IsRead()
    {
        var reader = Reader(UnitTestSrpClient.Record(TlsContentType.ChangeCipherSpec, [1]));

        await reader.ReadChangeCipherSpecAsync(CancellationToken.None);

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => reader.ReadChangeCipherSpecAsync(CancellationToken.None));
    }

    [TestMethod]
    [DataRow("02")]
    [DataRow("0101")]
    [DataRow("")]
    public async Task ReadChangeCipherSpecAsync_NotTheOneByte1_IsDecodeError(string fragment)
    {
        var reader = Reader(UnitTestSrpClient.Record(TlsContentType.ChangeCipherSpec, Convert.FromHexString(fragment)));

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => reader.ReadChangeCipherSpecAsync(CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert.Description);
    }

    [TestMethod]
    public async Task ReadChangeCipherSpecAsync_HandshakeBytesLeftOver_IsUnexpectedMessage()
    {
        byte[] message = UnitTestSrpClient.Handshake(TlsHandshakeType.ClientKeyExchange, [4]);
        var reader = Reader(UnitTestSrpClient.Record(TlsContentType.Handshake, [.. message, 20]));
        await reader.ReadAsync(TlsHandshakeType.ClientKeyExchange, CancellationToken.None);

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => reader.ReadChangeCipherSpecAsync(CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, alert.Description);
    }

    [TestMethod]
    public async Task ReadChangeCipherSpecAsync_HandshakeRecord_IsUnexpectedMessage()
    {
        var reader = Reader(UnitTestSrpClient.Record(TlsContentType.Handshake, UnitTestSrpClient.Handshake(TlsHandshakeType.Finished, [1])));

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => reader.ReadChangeCipherSpecAsync(CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, alert.Description);
    }

    [TestMethod]
    public async Task ReadChangeCipherSpecAsync_WarningAlert_ThrowsThePeersAlert()
    {
        var reader = Reader(UnitTestSrpClient.Record(TlsContentType.Alert, [1, 0]));

        var alert = await Assert.ThrowsExactlyAsync<TlsPeerAlertException>(() => reader.ReadChangeCipherSpecAsync(CancellationToken.None));

        Assert.IsFalse(alert.IsFatal);
        Assert.AreEqual((byte)0, alert.Description);
    }

    private static TlsHandshakeMessageReader Reader(byte[] bytes) => new(new TlsRecordReader(new MemoryStream(bytes)));
}
