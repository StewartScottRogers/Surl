namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsServerSession"/> taking an epoch 0 <c>ClientHello</c> on an established association,
/// as when the client restarts on the same endpoint (ADR-0087 decision 6, RFC 6347 section 4.2.8).
/// </summary>
[TestClass]
public sealed class DtlsServerSessionRestartedClientTests
{
    [TestMethod]
    public async Task ReceiveAsync_EpochZeroHelloWithoutCookie_IsAnsweredWithHelloVerifyRequestAndTheOldAssociationCarriesOn()
    {
        var (session, flow, client) = await EstablishAsync();
        using var _ = session;
        var restarted = new UnitTestDtlsClient { AnswersHelloVerifyRequest = false };
        flow.Client = restarted;
        var sentBefore = flow.Sent.Count;

        flow.DeliverFromClient(restarted.FirstHelloDatagram());
        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.ApplicationData, [0x40]));
        var received = await session.ReceiveAsync(flow.Exhausted.Token);
        flow.Client = client;
        await session.SendAsync(new byte[] { 0x60 }, flow.Exhausted.Token);

        Assert.IsNotNull(restarted.HelloVerifyRequest);
        CollectionAssert.AreEqual(new byte[] { 0x40 }, received);
        Assert.AreEqual(sentBefore + 2, flow.Sent.Count, "One HelloVerifyRequest, then the application data.");
        CollectionAssert.AreEqual(new byte[] { 0x60 }, client.ReceivedApplicationData.Single());
        Assert.IsEmpty(client.ReceivedAlerts);
    }

    [TestMethod]
    public async Task ReceiveAsync_EpochZeroHelloWithValidCookie_CompletesANewHandshakeWhoseKeysReplaceTheOldOnes()
    {
        var (session, flow, client) = await EstablishAsync();
        using var _ = session;
        var restarted = new UnitTestDtlsClient
        {
            ReplacesFinalFlight = self => [.. self.BuildFinalFlight(), self.ProtectedRecord(DtlsContentType.ApplicationData, [0x41])],
        };
        flow.Client = restarted;

        flow.DeliverFromClient(restarted.FirstHelloDatagram());
        var received = await session.ReceiveAsync(flow.Exhausted.Token);
        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.ApplicationData, [0x40]));
        flow.DeliverFromClient(restarted.ProtectedRecord(DtlsContentType.ApplicationData, [0x42]));
        var receivedAgain = await session.ReceiveAsync(flow.Exhausted.Token);
        await session.SendAsync(new byte[] { 0x60 }, flow.Exhausted.Token);

        Assert.IsNotNull(restarted.HelloVerifyRequest, "The new hello went through the cookie exchange.");
        Assert.IsTrue(restarted.IsEstablished);
        CollectionAssert.AreEqual(new byte[] { 0x41 }, received);
        CollectionAssert.AreEqual(new byte[] { 0x42 }, receivedAgain, "The old association's epoch 1 keys are no longer accepted.");
        CollectionAssert.AreEqual(new byte[] { 0x60 }, restarted.ReceivedApplicationData.Single());
        Assert.IsFalse(
            flow.Sent.SelectMany(UnitTestRecords.Split).Any(record => record.Header.ContentType == DtlsContentType.Alert),
            "No alert is sent on the old association.");
    }

    [TestMethod]
    public async Task ReceiveAsync_OldAssociationRecordsDuringTheNewHandshake_AreAcceptedAndKeptFromIt()
    {
        var (session, flow, client) = await EstablishAsync();
        using var _ = session;
        var restarted = new UnitTestDtlsClient
        {
            BeforeFinalFlight = _ =>
            [
                [
                    .. client.ProtectedRecord(DtlsContentType.ApplicationData, [0x40]),
                    .. client.ProtectedRecord(DtlsContentType.Alert, new DtlsAlert(IsFatal: true, DtlsAlertDescription.HandshakeFailure).Write()),
                ],
            ],
            ReplacesFinalFlight = self => [.. self.BuildFinalFlight(), self.ProtectedRecord(DtlsContentType.ApplicationData, [0x41])],
        };
        flow.Client = restarted;

        flow.DeliverFromClient(restarted.FirstHelloDatagram());
        var received = await session.ReceiveAsync(flow.Exhausted.Token);
        var receivedAgain = await session.ReceiveAsync(flow.Exhausted.Token);

        Assert.IsTrue(restarted.IsEstablished);
        CollectionAssert.AreEqual(new byte[] { 0x40 }, received, "The old association's data was accepted during the new handshake.");
        CollectionAssert.AreEqual(new byte[] { 0x41 }, receivedAgain);
        Assert.IsEmpty(restarted.ReceivedAlerts, "The old association's alert did not reach the new handshake.");
    }

    [TestMethod]
    public async Task ReceiveAsync_NewHandshakeFails_LeavesTheOldAssociationAsItWas()
    {
        var (session, flow, client) = await EstablishAsync();
        using var _ = session;
        var restarted = new UnitTestDtlsClient
        {
            CorruptsFinished = true,
            ReplacesFinalFlight = self => [.. self.BuildFinalFlight(), client.ProtectedRecord(DtlsContentType.ApplicationData, [0x40])],
        };
        flow.Client = restarted;

        flow.DeliverFromClient(restarted.FirstHelloDatagram());
        var received = await session.ReceiveAsync(flow.Exhausted.Token);
        flow.Client = client;
        await session.SendAsync(new byte[] { 0x60 }, flow.Exhausted.Token);

        Assert.AreEqual(new DtlsAlert(true, DtlsAlertDescription.DecryptError), restarted.ReceivedAlerts.Single());
        CollectionAssert.AreEqual(new byte[] { 0x40 }, received);
        CollectionAssert.AreEqual(new byte[] { 0x60 }, client.ReceivedApplicationData.Single());
        Assert.IsEmpty(client.ReceivedAlerts);
        Assert.AreEqual(UnitTestDtlsClient.DefaultSuite, session.CipherSuite!.Code);
    }

    private static async Task<(DtlsServerSession Session, UnitTestDatagramFlow Flow, UnitTestDtlsClient Client)> EstablishAsync()
    {
        var client = new UnitTestDtlsClient();
        var (session, flow) = DtlsServerSessionTests.Start(client);
        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);
        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        return (session, flow, client);
    }
}
