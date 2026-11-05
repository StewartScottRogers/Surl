namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsServerSession"/> once established: application data, the final flight sent
/// again until the client's first application data (RFC 6347 section 4.2.4), and the alerts that
/// end the association.
/// </summary>
[TestClass]
public sealed class DtlsServerSessionApplicationDataTests
{
    [TestMethod]
    public async Task ReceiveAndSend_BeforeTheHandshake_Throw()
    {
        var (session, flow) = DtlsServerSessionTests.Start(new UnitTestDtlsClient());
        using var _ = session;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await session.ReceiveAsync(flow.Exhausted.Token));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await session.SendAsync(new byte[1], flow.Exhausted.Token));
    }

    [TestMethod]
    public async Task ReceiveAsync_ClientFinalFlightAgain_IsAnsweredWithTheFinalFlightUntilApplicationDataArrives()
    {
        var (session, flow, client) = await EstablishAsync();
        var sentAfterHandshake = flow.Sent.Count;

        flow.DeliverFromClient(FinalFlightAgain(client));
        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.ApplicationData, [7]));
        var first = await session.ReceiveAsync(flow.Exhausted.Token);
        var resentFinalFlights = flow.Sent.Count - sentAfterHandshake;
        flow.DeliverFromClient(FinalFlightAgain(client));
        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.ApplicationData, [8]));
        var second = await session.ReceiveAsync(flow.Exhausted.Token);

        CollectionAssert.AreEqual(new byte[] { 7 }, first);
        CollectionAssert.AreEqual(new byte[] { 8 }, second);
        Assert.AreEqual(1, resentFinalFlights);
        Assert.AreEqual(sentAfterHandshake + 1, flow.Sent.Count);
        session.Dispose();
    }

    [TestMethod]
    public async Task ReceiveAsync_IgnoredRecordsThenCloseNotify_ReturnsNullFromThenOn()
    {
        var (session, flow, client) = await EstablishAsync();
        byte[] truncated = [.. client.ProtectedRecord(DtlsContentType.ApplicationData, [1])[..^1]];

        flow.DeliverFromClient(client.PlaintextRecord(DtlsContentType.ApplicationData, [1]));
        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.Alert, [1, (byte)DtlsAlertDescription.NoRenegotiation]));
        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.Alert, [1]));
        flow.DeliverFromClient(truncated);
        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.Alert, [1, 0]));
        var closed = await session.ReceiveAsync(flow.Exhausted.Token);
        var again = await session.ReceiveAsync(flow.Exhausted.Token);

        Assert.IsNull(closed);
        Assert.IsNull(again);
        session.Dispose();
    }

    [TestMethod]
    public async Task ReceiveAsync_FatalAlert_ReturnsNull()
    {
        var (session, flow, client) = await EstablishAsync();

        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.Alert, [2, 10]));

        Assert.IsNull(await session.ReceiveAsync(flow.Exhausted.Token));
        session.Dispose();
    }

    /// <summary>
    /// The epoch 0 part of the client's final flight sent again with fresh sequence numbers: a
    /// handshake record and a <c>ChangeCipherSpec</c>, in one datagram.
    /// </summary>
    private static byte[] FinalFlightAgain(UnitTestDtlsClient client) =>
    [
        .. client.PlaintextRecord(DtlsContentType.Handshake, new DtlsHandshakeMessage(DtlsHandshakeType.ClientKeyExchange, 2, [1, 4]).ToUnfragmentedBytes()),
        .. client.PlaintextRecord(DtlsContentType.ChangeCipherSpec, [1]),
    ];

    private static async Task<(DtlsServerSession Session, UnitTestDatagramFlow Flow, UnitTestDtlsClient Client)> EstablishAsync()
    {
        var client = new UnitTestDtlsClient();
        var (session, flow) = DtlsServerSessionTests.Start(client);
        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);
        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        return (session, flow, client);
    }
}
