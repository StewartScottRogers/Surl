namespace Surl.TlsSrp;

/// <summary>
/// The application data after the handshake (ADR-0122 decisions 3, 5 and 6): alerts, a ClientHello
/// after the handshake answered with <c>no_renegotiation</c>, and closing with <c>close_notify</c>.
/// </summary>
[TestClass]
public sealed class TlsSrpStreamTests
{
    [TestMethod]
    public async Task ReadAsync_WarningAlertThenData_IgnoresTheWarning()
    {
        var (session, _, _) = await OpenAsync(client => [Protected(client, TlsContentType.Alert, [1, 90]), Protected(client, TlsContentType.ApplicationData, [7, 8])]);
        var buffer = new byte[4];

        var count = await session.Stream.ReadAsync(buffer.AsMemory());

        Assert.AreEqual(2, count);
        CollectionAssert.AreEqual(new byte[] { 7, 8, 0, 0 }, buffer);
    }

    [TestMethod]
    public async Task ReadAsync_DataLongerThanTheBuffer_IsReadInPieces()
    {
        var (session, _, _) = await OpenAsync(client => [Protected(client, TlsContentType.ApplicationData, [1, 2, 3])]);
        var buffer = new byte[2];

        var first = await session.Stream.ReadAsync(buffer, 0, 2, CancellationToken.None);
        var second = await session.Stream.ReadAsync(buffer, 0, 2, CancellationToken.None);

        Assert.AreEqual(2, first);
        Assert.AreEqual(1, second);
        Assert.AreEqual((byte)3, buffer[0]);
    }

    [TestMethod]
    public async Task ReadAsync_CloseNotify_EndsTheStream()
    {
        var (session, _, _) = await OpenAsync(client => [Protected(client, TlsContentType.Alert, [1, 0]), Protected(client, TlsContentType.ApplicationData, [9])]);

        Assert.AreEqual(0, await session.Stream.ReadAsync(new byte[1].AsMemory()));
        Assert.AreEqual(0, await session.Stream.ReadAsync(new byte[1].AsMemory()));
    }

    [TestMethod]
    public async Task ReadAsync_ConnectionEndsWithoutCloseNotify_EndsTheStream()
    {
        var (session, _, _) = await OpenAsync(_ => []);

        Assert.AreEqual(0, await session.Stream.ReadAsync(new byte[1].AsMemory()));
    }

    [TestMethod]
    public async Task ReadAsync_EmptyBuffer_IsZeroWithoutReading()
    {
        var (session, _, _) = await OpenAsync(client => [Protected(client, TlsContentType.ApplicationData, [9])]);

        Assert.AreEqual(0, await session.Stream.ReadAsync(Memory<byte>.Empty));
        Assert.AreEqual(1, await session.Stream.ReadAsync(new byte[1].AsMemory()));
    }

    [TestMethod]
    public async Task ReadAsync_FatalAlert_ThrowsThePeersAlert()
    {
        var (session, transport, _) = await OpenAsync(client => [Protected(client, TlsContentType.Alert, [2, 80])]);
        var before = transport.Written.Length;

        var alert = await Assert.ThrowsExactlyAsync<TlsPeerAlertException>(() => session.Stream.ReadAsync(new byte[1].AsMemory()).AsTask());

        Assert.IsTrue(alert.IsFatal);
        Assert.AreEqual((byte)80, alert.Description);
        Assert.AreEqual(before, transport.Written.Length);
    }

    [TestMethod]
    public async Task ReadAsync_ClientHelloAfterTheHandshake_AnswersNoRenegotiationAndCarriesOn()
    {
        var (session, transport, handshake) = await OpenAsync(client =>
            [Protected(client, TlsContentType.Handshake, UnitTestSrpClient.ClientHello()), Protected(client, TlsContentType.ApplicationData, [5])]);

        var count = await session.Stream.ReadAsync(new byte[1].AsMemory());

        Assert.AreEqual(1, count);
        var serverRecords = handshake.ServerRecordsAfterChangeCipherSpec(transport.Written.ToArray());
        await serverRecords.ReadAsync(CancellationToken.None);
        var warning = await serverRecords.ReadAsync(CancellationToken.None);
        Assert.AreEqual(new TlsRecord(TlsContentType.Alert, 0x0303, warning!.Fragment), warning);
        CollectionAssert.AreEqual(new byte[] { 1, 100 }, warning.Fragment);
    }

    [TestMethod]
    public async Task ReadAsync_ChangeCipherSpecAfterTheHandshake_SendsUnexpectedMessage()
    {
        var (session, transport, handshake) = await OpenAsync(client => [Protected(client, TlsContentType.ChangeCipherSpec, [1])]);

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => session.Stream.ReadAsync(new byte[1].AsMemory()).AsTask());

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, alert.Description);
        var serverRecords = handshake.ServerRecordsAfterChangeCipherSpec(transport.Written.ToArray());
        await serverRecords.ReadAsync(CancellationToken.None);
        CollectionAssert.AreEqual(new byte[] { 2, 10 }, (await serverRecords.ReadAsync(CancellationToken.None))!.Fragment);
    }

    [TestMethod]
    public async Task ReadAsync_RecordWithABadMac_SendsBadRecordMac()
    {
        var (session, _, _) = await OpenAsync(client =>
        {
            var record = Protected(client, TlsContentType.ApplicationData, [1, 2, 3]);
            record[^1] ^= 1;
            return [record];
        });

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => session.Stream.ReadAsync(new byte[1].AsMemory()).AsTask());

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert.Description);
    }

    [TestMethod]
    public async Task WriteAsync_EmptyBuffer_WritesNothing()
    {
        var (session, transport, _) = await OpenAsync(_ => []);
        var before = transport.Written.Length;

        await session.Stream.WriteAsync(ReadOnlyMemory<byte>.Empty);
        await session.Stream.WriteAsync([9, 9, 9], 1, 1, CancellationToken.None);

        var handshake = new UnitTestSrpHandshake();
        Assert.IsGreaterThan(before, transport.Written.Length);
        var serverRecords = handshake.ServerRecordsAfterChangeCipherSpec(transport.Written.ToArray());
        await serverRecords.ReadAsync(CancellationToken.None);
        CollectionAssert.AreEqual(new byte[] { 9 }, (await serverRecords.ReadAsync(CancellationToken.None))!.Fragment);
        Assert.IsNull(await serverRecords.ReadAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task DisposeAsync_SendsCloseNotifyOnceAndClosesTheConnection()
    {
        var (session, transport, handshake) = await OpenAsync(_ => []);

        await session.Stream.DisposeAsync();
        await session.Stream.DisposeAsync();

        Assert.IsTrue(transport.IsDisposed);
        var serverRecords = handshake.ServerRecordsAfterChangeCipherSpec(transport.Written.ToArray());
        await serverRecords.ReadAsync(CancellationToken.None);
        CollectionAssert.AreEqual(new byte[] { 1, 0 }, (await serverRecords.ReadAsync(CancellationToken.None))!.Fragment);
        Assert.IsNull(await serverRecords.ReadAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task DisposeAsync_ConnectionAlreadyGone_StillCloses()
    {
        var (session, transport, _) = await OpenAsync(_ => []);
        transport.FailWrites = true;

        await session.Stream.DisposeAsync();

        Assert.IsTrue(transport.IsDisposed);
    }

    [TestMethod]
    public async Task Dispose_ClosesTheConnectionWithoutCloseNotify()
    {
        var (session, transport, _) = await OpenAsync(_ => []);
        var before = transport.Written.Length;

        session.Stream.Dispose();

        Assert.IsTrue(transport.IsDisposed);
        Assert.AreEqual(before, transport.Written.Length);
    }

    [TestMethod]
    public async Task StreamMembers_AreAsynchronousAndUnseekable()
    {
        var (session, _, _) = await OpenAsync(_ => []);
        var stream = session.Stream;

        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
        stream.Flush();
        await stream.FlushAsync(CancellationToken.None);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    private static byte[] Protected(UnitTestSrpHandshake handshake, TlsContentType type, byte[] content) =>
        UnitTestSrpClient.ProtectedRecord(handshake.ClientWrite, type, content);

    private static async Task<(TlsSrpSession Session, UnitTestScriptedStream Transport, UnitTestSrpHandshake Handshake)> OpenAsync(
        Func<UnitTestSrpHandshake, byte[][]> recordsAfterFinished)
    {
        var handshake = new UnitTestSrpHandshake();
        var handshakeBytes = handshake.ClientBytes();
        byte[] client = [.. handshakeBytes, .. recordsAfterFinished(handshake).SelectMany(record => record)];
        var transport = new UnitTestScriptedStream(client);
        var session = await UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None);
        return (session, transport, handshake);
    }
}
