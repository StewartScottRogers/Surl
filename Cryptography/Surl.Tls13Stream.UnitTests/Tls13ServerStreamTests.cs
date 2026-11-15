using Surl.Tls13;

namespace Surl.Tls13Stream;

[TestClass]
public sealed class Tls13ServerStreamTests
{
    private static async Task<int> ReadSomeAsync(Tls13ServerStream stream) => await stream.ReadAsync(new byte[4]);

    [TestMethod]
    public async Task ReadAsync_SmallBuffer_ReturnsTheRecordInPartsThenWaitsForTheNextRecord()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        await established.Client.SendAsync(TlsContentType.ApplicationData, 1, 2, 3);
        await established.Client.SendAsync(TlsContentType.ApplicationData, 4);
        var buffer = new byte[2];

        var first = await established.Stream.ReadAsync(buffer, 0, 2, CancellationToken.None);
        var second = await established.Stream.ReadAsync(buffer, 0, 2, CancellationToken.None);
        var third = await established.Stream.ReadAsync(buffer, 0, 2, CancellationToken.None);
        var none = await established.Stream.ReadAsync(Memory<byte>.Empty);

        Assert.AreEqual(2, first);
        Assert.AreEqual(1, second);
        Assert.AreEqual(4, buffer[0]);
        Assert.AreEqual(1, third);
        Assert.AreEqual(0, none);
    }

    [TestMethod]
    public async Task WriteAsync_MoreThanOneRecordOfData_SplitsItAtTwoToTheFourteenBytes()
    {
        await using var established = await UnitTestEstablished.CreateAsync();

        await established.Stream.WriteAsync(new byte[40000], 0, 40000, CancellationToken.None);
        await established.Stream.WriteAsync(ReadOnlyMemory<byte>.Empty);
        var lengths = new List<int>();
        for (var index = 0; index < 3; index++)
        {
            lengths.Add((await established.Client.ReadProtectedAsync())!.Content.Length);
        }

        CollectionAssert.AreEqual(new[] { 16384, 16384, 7232 }, lengths);
    }

    [TestMethod]
    public async Task ReadAsync_CloseNotify_EndsTheStreamAndAnswersWithOurs()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        await established.Client.SendAsync(TlsContentType.Alert, 1, 0);

        var count = await established.Stream.ReadAsync(new byte[8]);
        var answer = await established.Client.ReadProtectedAsync();
        await established.Stream.DisposeAsync();
        var afterDispose = await established.Client.ReadProtectedAsync();

        Assert.AreEqual(0, count);
        CollectionAssert.AreEqual(new byte[] { 1, 0 }, answer!.Content);
        Assert.IsNull(afterDispose, "Disposing must not send a second close_notify.");
    }

    [TestMethod]
    public async Task ReadAsync_UserCanceledAlert_IsIgnored()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        await established.Client.SendAsync(TlsContentType.Alert, 1, 90);
        await established.Client.SendAsync(TlsContentType.ApplicationData, 9);

        var buffer = new byte[4];
        var count = await established.Stream.ReadAsync(buffer);

        Assert.AreEqual(1, count);
        Assert.AreEqual(9, buffer[0]);
    }

    [TestMethod]
    public async Task ReadAsync_FatalAlert_ThrowsThePeerAlert()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        await established.Client.SendAsync(TlsContentType.Alert, 2, 40);

        var failure = await Assert.ThrowsExactlyAsync<Tls13PeerAlertException>(() => ReadSomeAsync(established.Stream));

        Assert.AreEqual(TlsAlert.HandshakeFailure, failure.Alert);
    }

    [TestMethod]
    public async Task ReadAsync_AlertOfOneByte_WritesDecodeErrorAndThrowsIt()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        await established.Client.SendAsync(TlsContentType.Alert, 1);

        var failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadSomeAsync(established.Stream));
        var alert = await established.Client.ReadProtectedAsync();

        Assert.AreEqual(TlsAlert.DecodeError, failure.Alert);
        CollectionAssert.AreEqual(new byte[] { 2, 50 }, alert!.Content);
    }

    [TestMethod]
    public async Task ReadAsync_KeyUpdateRequestingOne_AnswersItAndBothDirectionsCarryOnWithNewKeys()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        await established.Client.SendKeyUpdateAsync(1);
        await established.Client.SendAsync(TlsContentType.ApplicationData, 5, 6);
        var buffer = new byte[4];

        var count = await established.Stream.ReadAsync(buffer);
        var keyUpdate = await established.Client.ReadProtectedAsync();
        established.Client.UpdateReadKeys();
        await established.Stream.WriteAsync(new byte[] { 7 });
        var reply = await established.Client.ReadProtectedAsync();

        Assert.AreEqual(2, count);
        Assert.AreEqual(TlsContentType.Handshake, keyUpdate!.ContentType);
        CollectionAssert.AreEqual(new byte[] { 24, 0, 0, 1, 0 }, keyUpdate.Content);
        CollectionAssert.AreEqual(new byte[] { 7 }, reply!.Content);
    }

    [TestMethod]
    public async Task ReadAsync_KeyUpdateNotRequestingOne_SendsNoKeyUpdateBack()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        await established.Client.SendKeyUpdateAsync(0);
        await established.Client.SendAsync(TlsContentType.ApplicationData, 5);

        var count = await established.Stream.ReadAsync(new byte[4]);
        await established.Stream.WriteAsync(new byte[] { 7 });
        var reply = await established.Client.ReadProtectedAsync();

        Assert.AreEqual(1, count);
        Assert.AreEqual(TlsContentType.ApplicationData, reply!.ContentType);
    }

    [TestMethod]
    [DataRow(new byte[] { 24, 0, 0, 1, 2 }, TlsAlert.IllegalParameter)]
    [DataRow(new byte[] { 4, 0, 0, 1, 0 }, TlsAlert.UnexpectedMessage)]
    [DataRow(new byte[] { 24, 0, 0, 1 }, TlsAlert.UnexpectedMessage)]
    public async Task ReadAsync_HandshakeMessageThatIsNotAValidKeyUpdate_WritesTheAlertAndThrowsIt(byte[] message, TlsAlert expected)
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        await established.Client.SendAsync(TlsContentType.Handshake, message);

        var failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadSomeAsync(established.Stream));
        var alert = await established.Client.ReadProtectedAsync();

        Assert.AreEqual(expected, failure.Alert);
        CollectionAssert.AreEqual(new byte[] { 2, (byte)expected }, alert!.Content);
    }

    [TestMethod]
    public async Task ReadAsync_ProtectedChangeCipherSpec_WritesUnexpectedMessageAndThrowsIt()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        await established.Client.SendAsync(TlsContentType.ChangeCipherSpec, 1);

        var failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadSomeAsync(established.Stream));

        Assert.AreEqual(TlsAlert.UnexpectedMessage, failure.Alert);
    }

    [TestMethod]
    [DataRow(21)]
    [DataRow(22)]
    [DataRow(20)]
    public async Task ReadAsync_PlaintextRecordAfterTheHandshake_IsRefusedWithAProtectedUnexpectedMessage(int contentType)
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        await established.Client.SendRecordAsync(UnitTestClientHellos.Record((byte)contentType, [1]));

        var failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadSomeAsync(established.Stream));
        var alert = await established.Client.ReadProtectedAsync();

        Assert.AreEqual(TlsAlert.UnexpectedMessage, failure.Alert);
        CollectionAssert.AreEqual(new byte[] { 2, 10 }, alert!.Content);
    }

    [TestMethod]
    public async Task ReadAsync_RecordWithABadTag_WritesBadRecordMacAndThrowsIt()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        await established.Client.SendRecordAsync(established.Client.CorruptedRecord(TlsContentType.ApplicationData, 1, 2));

        var failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadSomeAsync(established.Stream));
        var alert = await established.Client.ReadProtectedAsync();

        Assert.AreEqual(TlsAlert.BadRecordMac, failure.Alert);
        CollectionAssert.AreEqual(new byte[] { 2, 20 }, alert!.Content);
    }

    [TestMethod]
    public async Task ReadAsync_BadRecordWhileTheConnectionIsGone_StillThrowsTheAlertException()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        await established.Client.SendRecordAsync(established.Client.CorruptedRecord(TlsContentType.ApplicationData, 1));
        established.ServerEnd.FailWrites = true;

        var failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadSomeAsync(established.Stream));

        Assert.AreEqual(TlsAlert.BadRecordMac, failure.Alert);
    }

    [TestMethod]
    public async Task ReadAsync_RecordTruncatedMidway_ThrowsEndOfStream()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        await established.ClientEnd.WriteAsync(new byte[] { 23, 3, 3, 0, 30, 1, 2 });
        established.ClientEnd.Dispose();

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => ReadSomeAsync(established.Stream));
    }

    [TestMethod]
    public async Task ReadAsync_TransportEndsBetweenRecords_EndsTheStream()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        established.ClientEnd.Dispose();

        var count = await established.Stream.ReadAsync(new byte[4]);

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task ShutdownAsync_CalledTwice_SendsCloseNotifyOnceAndDisposeSendsNoMore()
    {
        await using var established = await UnitTestEstablished.CreateAsync();

        await established.Stream.ShutdownAsync();
        await established.Stream.ShutdownAsync();
        await established.Stream.DisposeAsync();
        var first = await established.Client.ReadProtectedAsync();
        var second = await established.Client.ReadProtectedAsync();

        CollectionAssert.AreEqual(new byte[] { 1, 0 }, first!.Content);
        Assert.IsNull(second);
    }

    [TestMethod]
    public async Task ShutdownAsync_ConnectionGone_ThrowsIOException()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        established.ServerEnd.FailWrites = true;

        await Assert.ThrowsExactlyAsync<IOException>(() => established.Stream.ShutdownAsync());
    }

    [TestMethod]
    public async Task DisposeAsync_ConnectionGone_IsQuietAndASecondDisposeDoesNothing()
    {
        var established = await UnitTestEstablished.CreateAsync();
        established.ServerEnd.FailWrites = true;

        await established.Stream.DisposeAsync();
        await established.Stream.DisposeAsync();
        await established.DisposeAsync();
    }

    [TestMethod]
    public async Task ReadAsync_AnswerToCloseNotifyWhileTheConnectionIsGone_StillEndsTheStream()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        established.ServerEnd.FailWrites = true;
        await established.Client.SendAsync(TlsContentType.Alert, 1, 0);

        var count = await established.Stream.ReadAsync(new byte[4]);

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task ReadAsync_CloseNotifyAfterShutdown_SendsNoSecondCloseNotify()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        await established.Stream.ShutdownAsync();
        await established.Client.SendAsync(TlsContentType.Alert, 1, 0);

        var count = await established.Stream.ReadAsync(new byte[4]);
        await established.Stream.DisposeAsync();
        var first = await established.Client.ReadProtectedAsync();
        var second = await established.Client.ReadProtectedAsync();

        Assert.AreEqual(0, count);
        Assert.IsNotNull(first);
        Assert.IsNull(second);
    }

    [TestMethod]
    public async Task SynchronousMembers_AreNotSupportedAndFlushDoesNothing()
    {
        await using var established = await UnitTestEstablished.CreateAsync();
        var stream = established.Stream;

        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => _ = stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => _ = stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 1);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        stream.Flush();
        await stream.FlushAsync();
    }

    [TestMethod]
    public async Task Dispose_Synchronously_ClosesTheTransportWithoutCloseNotify()
    {
        var established = await UnitTestEstablished.CreateAsync();

        established.Stream.Dispose();
        var next = await established.Client.ReadProtectedAsync();

        Assert.IsNull(next);
        await established.DisposeAsync();
    }
}
