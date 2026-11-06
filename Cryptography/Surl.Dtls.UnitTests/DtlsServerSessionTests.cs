namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsServerSession"/> driven by <see cref="UnitTestDtlsClient"/>, written from
/// RFC 6347, over <see cref="UnitTestDatagramFlow"/> with a <see cref="ManualTimeProvider"/>.
/// </summary>
[TestClass]
public sealed class DtlsServerSessionTests
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task HandshakeAsync_CookielessHello_AnswersHelloVerifyRequestAndKeepsNothing()
    {
        var client = new UnitTestDtlsClient(initialRecordSequenceNumber: 7) { AnswersHelloVerifyRequest = false };
        var (session, flow) = Start(client);
        using var _ = session;

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => session.HandshakeAsync(flow.Exhausted.Token));

        Assert.HasCount(1, flow.Sent);
        var (header, fragment) = UnitTestRecords.Split(flow.Sent[0]).Single();
        Assert.AreEqual(7, header.SequenceNumber);
        Assert.IsTrue(DtlsHandshakeHeader.TryRead(fragment, out var handshake));
        Assert.AreEqual(DtlsHandshakeType.HelloVerifyRequest, handshake.MessageType);
        Assert.AreEqual((ushort)0, handshake.MessageSeq);
        Assert.AreEqual(DtlsHelloVerifyRequest.Dtls10Version, client.HelloVerifyRequest!.ServerVersion);
        Assert.HasCount(DtlsCookieSecrets.CookieLength, client.HelloVerifyRequest.Cookie);
        Assert.IsTrue(flow.Elapsed > HandshakeTimeout, "No handshake timer runs before the cookie returns.");
        Assert.IsNull(session.CipherSuite);
    }

    [TestMethod]
    [DataRow((ushort)0xC02B, DtlsCertificateKeyAlgorithm.Ecdsa)]
    [DataRow((ushort)0xC02C, DtlsCertificateKeyAlgorithm.Ecdsa)]
    [DataRow((ushort)0xC02F, DtlsCertificateKeyAlgorithm.Rsa)]
    [DataRow((ushort)0xC030, DtlsCertificateKeyAlgorithm.Rsa)]
    public async Task HandshakeAsync_EachGcmCertificateSuite_CompletesAndExchangesApplicationData(ushort suite, DtlsCertificateKeyAlgorithm keyAlgorithm)
    {
        await AssertHandshakeAndEchoAsync(suite, keyAlgorithm);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow((ushort)0xC0AE)]
    [DataRow((ushort)0xC0AC)]
    public async Task HandshakeAsync_EachCcmCertificateSuite_CompletesAndExchangesApplicationData(ushort suite)
    {
        await AssertHandshakeAndEchoAsync(suite, DtlsCertificateKeyAlgorithm.Ecdsa);
    }

    [TestMethod]
    [DataRow(DtlsNamedGroup.X25519, false)]
    [DataRow(DtlsNamedGroup.Secp384r1, true)]
    public async Task HandshakeAsync_OtherGroupsWithAndWithoutExtendedMasterSecret_Complete(DtlsNamedGroup group, bool extendedMasterSecret)
    {
        var client = new UnitTestDtlsClient { Group = group, OffersExtendedMasterSecret = extendedMasterSecret };
        var (session, flow) = Start(client, UnitTestCertificates.EcdsaP384);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.IsTrue(client.IsEstablished);
    }

    [TestMethod]
    public async Task HandshakeAsync_ServerFlightLost_RetransmitsItWhenTheTimerFires()
    {
        var client = new UnitTestDtlsClient();
        var (session, flow) = Start(client);
        using var _ = session;
        flow.DroppedSends.Add(1);

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.AreEqual(2, UnitTestRecords.ServerHelloFlights(flow.Sent));
        Assert.AreEqual(DtlsServerOptions.InitialRetransmissionTimeout, flow.Elapsed);
    }

    [TestMethod]
    public async Task HandshakeAsync_BadCookie_IsAnsweredWithAFreshHelloVerifyRequestAndNoAlert()
    {
        var client = new UnitTestDtlsClient(initialCookie: new byte[DtlsCookieSecrets.CookieLength]);
        var (session, flow) = Start(client);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        CollectionAssert.AreEqual(new[] { DtlsHandshakeType.HelloVerifyRequest }, UnitTestRecords.HandshakeTypes(flow.Sent[0]));
        Assert.IsEmpty(client.ReceivedAlerts);
    }

    [TestMethod]
    public async Task HandshakeAsync_BadFinished_EndsWithDecryptError()
    {
        var client = new UnitTestDtlsClient { CorruptsFinished = true };
        var (session, flow) = Start(client);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(new DtlsHandshakeOutcome(DtlsHandshakeResult.AlertSent, DtlsAlertDescription.DecryptError), outcome);
        Assert.AreEqual(new DtlsAlert(true, DtlsAlertDescription.DecryptError), UnitTestRecords.PlaintextAlert(flow.Sent[^1]));
    }

    [TestMethod]
    public async Task HandshakeAsync_OverTheTimeLimit_IsAbandonedWithoutAnAlert()
    {
        var client = new UnitTestDtlsClient { SendsFinalFlight = false };
        var (session, flow) = Start(client);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(new DtlsHandshakeOutcome(DtlsHandshakeResult.TimedOut, null), outcome);
        Assert.AreEqual(5, UnitTestRecords.ServerHelloFlights(flow.Sent), "Sent at 0 s, then again at 1, 3, 7 and 15 s.");
        Assert.AreEqual(HandshakeTimeout, flow.Elapsed);
        Assert.IsEmpty(client.ReceivedAlerts);
    }

    private async Task AssertHandshakeAndEchoAsync(ushort suite, DtlsCertificateKeyAlgorithm keyAlgorithm)
    {
        var client = new UnitTestDtlsClient { CipherSuites = [suite] };
        var certificate = keyAlgorithm == DtlsCertificateKeyAlgorithm.Rsa ? UnitTestCertificates.Rsa2048 : UnitTestCertificates.EcdsaP256;
        var (session, flow) = Start(client, certificate);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);
        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.ApplicationData, [0x40, 0x01]));
        var request = await session.ReceiveAsync(flow.Exhausted.Token);
        await session.SendAsync(new byte[] { 0x60, 0x45 }, flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.AreEqual(suite, session.CipherSuite!.Code);
        Assert.AreEqual(TimeSpan.Zero, flow.Elapsed, "Nothing was retransmitted.");
        Assert.IsTrue(client.IsEstablished);
        CollectionAssert.AreEqual(new byte[] { 0x40, 0x01 }, request);
        CollectionAssert.AreEqual(new byte[] { 0x60, 0x45 }, client.ReceivedApplicationData.Single());
    }

    internal static (DtlsServerSession Session, UnitTestDatagramFlow Flow) Start(
        UnitTestDtlsClient client,
        DtlsServerCertificate? certificate = null,
        int maximumMessageLength = 65536,
        Func<DtlsServerOptions, DtlsServerOptions>? configure = null)
    {
        var clock = new ManualTimeProvider();
        var flow = new UnitTestDatagramFlow(client, clock);
        var options = new DtlsServerOptions(
            certificate ?? UnitTestCertificates.EcdsaP256, new DtlsCookieSecrets(clock), clock, HandshakeTimeout, maximumMessageLength);
        return (new DtlsServerSession(flow, configure?.Invoke(options) ?? options), flow);
    }
}
