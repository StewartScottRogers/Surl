using System.Buffers.Binary;
using Surl.Protocol.Abstractions;

namespace Surl.Kerberos.TestKdc;

[TestClass]
public sealed class KerberosTestKdcServerTests
{
    private readonly SettableTimeProvider clock = new(KdcClient.Now);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeDatagramAsync_AsRequest_SendsTheAnswerInOneDatagram()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] request = KdcClient.AsRequest(KdcClient.Aes);
        FakeDatagramFlow flow = new(request);

        await new KerberosTestKdcServer(kdc).ServeDatagramAsync(flow, TestContext.CancellationToken);

        Assert.AreEqual(KerberosErrorCode.PreAuthenticationRequired, KdcError.Read(flow.Sent.Single()).ErrorCode);
    }

    [TestMethod]
    public async Task ServeDatagramAsync_AnswerLongerThan4096Bytes_SendsResponseTooBig()
    {
        string longHost = new('w', 3000);
        KerberosTestKdc kdc = KdcClient.NewKdc(clock, "HTTP/" + longHost);
        byte[] key = kdc.GetUserKey(KerberosEncryptionType.Aes256CtsHmacSha196).ToArray();
        byte[] request = KdcClient.AsRequest(
            KdcClient.Aes, [KdcClient.EncryptedTimestamp(KerberosEncryptionType.Aes256CtsHmacSha196, key, KdcClient.Now)], serverName: ["HTTP", longHost]);
        FakeDatagramFlow flow = new(request);

        await new KerberosTestKdcServer(kdc).ServeDatagramAsync(flow, TestContext.CancellationToken);

        Assert.IsGreaterThan(KerberosTestKdcServer.MaximumDatagramAnswerLength, kdc.Answer(request).Length);
        Assert.AreEqual(KerberosErrorCode.ResponseTooBig, KdcError.Read(flow.Sent.Single()).ErrorCode);
    }

    [TestMethod]
    public async Task ServeDatagramAsync_DatagramLongerThan64KiB_SendsFieldTooLong()
    {
        FakeDatagramFlow flow = new(new byte[KerberosTestKdc.MaximumRequestLength + 1]);

        await new KerberosTestKdcServer(KdcClient.NewKdc(clock)).ServeDatagramAsync(flow, TestContext.CancellationToken);

        Assert.AreEqual(KerberosErrorCode.FieldTooLong, KdcError.Read(flow.Sent.Single()).ErrorCode);
    }

    [TestMethod]
    public async Task ServeConnectionAsync_LengthFramedRequestSplitAcrossReads_WritesTheLengthFramedAnswerAndHalfCloses()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] framed = Frame(KdcClient.AsRequest(KdcClient.Aes));
        InMemoryConnection connection = new([framed[..2], framed[2..9], framed[9..]]);

        await new KerberosTestKdcServer(kdc).ServeConnectionAsync(connection, TestContext.CancellationToken);

        byte[] written = connection.WrittenBytes;
        Assert.AreEqual(written.Length - 4, BinaryPrimitives.ReadInt32BigEndian(written));
        Assert.AreEqual(KerberosErrorCode.PreAuthenticationRequired, KdcError.Read(written[4..]).ErrorCode);
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    [DataRow(0x00010001u)]
    [DataRow(0x80000010u)]
    public async Task ServeConnectionAsync_DeclaredLengthOver64KiB_AnswersFieldTooLongWithoutReadingIt(uint declaredLength)
    {
        byte[] prefix = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(prefix, declaredLength);
        InMemoryConnection connection = new([prefix], peerHalfClosesWhenExhausted: false);

        await new KerberosTestKdcServer(KdcClient.NewKdc(clock)).ServeConnectionAsync(connection, TestContext.CancellationToken);

        Assert.AreEqual(KerberosErrorCode.FieldTooLong, KdcError.Read(connection.WrittenBytes[4..]).ErrorCode);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("0000")]
    [DataRow("0000001030")]
    public async Task ServeConnectionAsync_ConnectionEndingBeforeTheWholeRequest_IsLeftUnanswered(string sentHex)
    {
        InMemoryConnection connection = new([Convert.FromHexString(sentHex)]);

        await new KerberosTestKdcServer(KdcClient.NewKdc(clock)).ServeConnectionAsync(connection, TestContext.CancellationToken);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeListenersAsync_FlowsAndConnections_AreEachAnsweredAndDisposedUntilCancelled()
    {
        KerberosTestKdc kdc = KdcClient.NewKdc(clock);
        byte[] request = KdcClient.AsRequest(KdcClient.Aes);
        FakeDatagramListener datagramListener = new();
        FakeConnectionListener connectionListener = new();
        FakeDatagramFlow flow = new(request);
        DisposalWatchingConnection connection = new(new InMemoryConnection([Frame(request)]));
        FakeDatagramFlow failingFlow = new(request, new IOException("gone"));
        DisposalWatchingConnection abortedConnection = new(new InMemoryConnection([Frame(request)]));
        abortedConnection.Abort();
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        Task serving = new KerberosTestKdcServer(kdc).ServeListenersAsync(datagramListener, connectionListener, stop.Token);
        datagramListener.Open(failingFlow);
        datagramListener.Open(flow);
        connectionListener.Connect(abortedConnection);
        connectionListener.Connect(connection);
        await Task.WhenAll(flow.WhenDisposed, connection.WhenDisposed, failingFlow.WhenDisposed, abortedConnection.WhenDisposed);
        DisposalWatchingConnection waitingConnection = new(new InMemoryConnection([], peerHalfClosesWhenExhausted: false));
        connectionListener.Connect(waitingConnection);
        await waitingConnection.WhenReadStarted;
        await stop.CancelAsync();
        await waitingConnection.WhenDisposed;

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual(KerberosErrorCode.PreAuthenticationRequired, KdcError.Read(flow.Sent.Single()).ErrorCode);
        Assert.AreEqual(KerberosErrorCode.PreAuthenticationRequired, KdcError.Read(connection.Inner.WrittenBytes[4..]).ErrorCode);
        Assert.IsEmpty(failingFlow.Sent);
        Assert.IsEmpty(waitingConnection.Inner.WrittenBytes);
    }

    private static byte[] Frame(byte[] request)
    {
        byte[] framed = new byte[4 + request.Length];
        BinaryPrimitives.WriteInt32BigEndian(framed, request.Length);
        request.CopyTo(framed, 4);
        return framed;
    }
}
