namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsServerSession"/>'s answers to a client that misbehaves or loses records during
/// the handshake: each failure's alert (ADR-0087 decision 10), and the flights sent again.
/// </summary>
[TestClass]
public sealed class DtlsServerSessionFailureTests
{
    [TestMethod]
    public async Task HandshakeAsync_NoSuiteTheCertificateServes_SendsHandshakeFailure()
    {
        await AssertAlertSentAsync(new UnitTestDtlsClient { CipherSuites = [0xC02F] }, DtlsAlertDescription.HandshakeFailure);
    }

    [TestMethod]
    public async Task HandshakeAsync_ClientVersionBelowDtls12_SendsProtocolVersion()
    {
        await AssertAlertSentAsync(new UnitTestDtlsClient { ClientVersion = 0xFEFF }, DtlsAlertDescription.ProtocolVersion);
    }

    [TestMethod]
    public async Task HandshakeAsync_NoNullCompression_SendsIllegalParameter()
    {
        await AssertAlertSentAsync(new UnitTestDtlsClient { CompressionMethods = [1] }, DtlsAlertDescription.IllegalParameter);
    }

    [TestMethod]
    public async Task HandshakeAsync_ClientEcdhePointNotOnTheCurve_SendsIllegalParameter()
    {
        var client = new UnitTestDtlsClient { ReplacesFinalFlight = c => [c.HandshakeRecord(DtlsHandshakeType.ClientKeyExchange, [1, 4])] };

        await AssertAlertSentAsync(client, DtlsAlertDescription.IllegalParameter);
    }

    [TestMethod]
    public async Task HandshakeAsync_MalformedClientKeyExchange_SendsDecodeError()
    {
        var client = new UnitTestDtlsClient { ReplacesFinalFlight = c => [c.HandshakeRecord(DtlsHandshakeType.ClientKeyExchange, [])] };

        await AssertAlertSentAsync(client, DtlsAlertDescription.DecodeError);
    }

    [TestMethod]
    public async Task HandshakeAsync_FinishedInPlaceOfClientKeyExchange_SendsUnexpectedMessage()
    {
        var client = new UnitTestDtlsClient { ReplacesFinalFlight = c => [c.HandshakeRecord(DtlsHandshakeType.Finished, new byte[12])] };

        await AssertAlertSentAsync(client, DtlsAlertDescription.UnexpectedMessage);
    }

    [TestMethod]
    public async Task HandshakeAsync_MalformedHandshakeFragment_SendsDecodeError()
    {
        var client = new UnitTestDtlsClient { ReplacesFinalFlight = c => [c.PlaintextRecord(DtlsContentType.Handshake, [16, 0, 0])] };

        await AssertAlertSentAsync(client, DtlsAlertDescription.DecodeError);
    }

    [TestMethod]
    public async Task HandshakeAsync_ClientKeyExchangeOverTheMessageLimit_SendsHandshakeFailure()
    {
        await AssertAlertSentAsync(new UnitTestDtlsClient(), DtlsAlertDescription.HandshakeFailure, maximumMessageLength: 10);
    }

    [TestMethod]
    [DataRow(new byte[] { 2 })]
    [DataRow(new byte[] { })]
    [DataRow(new byte[] { 1, 1 })]
    public async Task HandshakeAsync_ChangeCipherSpecNotTheByteOne_SendsDecodeError(byte[] body)
    {
        await AssertAlertSentAsync(new UnitTestDtlsClient { ChangeCipherSpecBody = body }, DtlsAlertDescription.DecodeError);
    }

    [TestMethod]
    public async Task HandshakeAsync_CertificateChainLongerThanOneDatagram_IsFragmentedAcrossDatagramsOfAtMost1200Bytes()
    {
        var leaf = UnitTestCertificates.Rsa2048;
        var certificate = new DtlsServerCertificate([.. Enumerable.Repeat(leaf.Chain[0], 3)], UnitTestCertificates.Rsa2048Key);
        var client = new UnitTestDtlsClient { CipherSuites = [0xC02F] };
        var (session, flow) = DtlsServerSessionTests.Start(client, certificate);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.IsGreaterThan(3, flow.Sent.Count);
        Assert.IsTrue(flow.Sent.All(datagram => datagram.Length <= DtlsHandshakeFragmenter.MaximumDatagramLength));
    }

    [TestMethod]
    public async Task HandshakeAsync_ClientFatalAlertAfterIgnoredRecords_EndsWithAlertReceived()
    {
        var client = new UnitTestDtlsClient
        {
            ReplacesFinalFlight = c =>
            [
                c.PlaintextRecord(DtlsContentType.Alert, [1, 0]),
                c.PlaintextRecord(DtlsContentType.Alert, [2]),
                c.PlaintextRecord(DtlsContentType.ApplicationData, [9]),
                c.PlaintextRecord(DtlsContentType.Alert, [2, 40]),
            ],
        };
        var (session, flow) = DtlsServerSessionTests.Start(client);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(new DtlsHandshakeOutcome(DtlsHandshakeResult.AlertReceived, DtlsAlertDescription.HandshakeFailure), outcome);
        Assert.AreEqual(1, UnitTestRecords.ServerHelloFlights(flow.Sent));
    }

    [TestMethod]
    public async Task HandshakeAsync_ChangeCipherSpecBeforeClientKeyExchange_IsIgnored()
    {
        var client = new UnitTestDtlsClient { BeforeFinalFlight = c => [c.PlaintextRecord(DtlsContentType.ChangeCipherSpec, [1])] };
        var (session, flow) = DtlsServerSessionTests.Start(client);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.AreEqual(TimeSpan.Zero, flow.Elapsed);
    }

    [TestMethod]
    public async Task HandshakeAsync_ClientSendsItsHelloAgain_ServerSendsItsFlightAgain()
    {
        var client = new UnitTestDtlsClient { BeforeFinalFlight = c => [c.LastHelloDatagram] };
        var (session, flow) = DtlsServerSessionTests.Start(client);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.AreEqual(2, UnitTestRecords.ServerHelloFlights(flow.Sent));
        Assert.AreEqual(TimeSpan.Zero, flow.Elapsed);
    }

    [TestMethod]
    public async Task HandshakeAsync_FinishedArrivesBeforeChangeCipherSpec_IsDroppedThenCompletesOnTheClientsRetransmission()
    {
        var client = new UnitTestDtlsClient
        {
            SplitsFinalFlight = true,
            ReplacesFinalFlight = c => Enumerable.Reverse(c.BuildFinalFlight()),
        };
        var (session, flow) = DtlsServerSessionTests.Start(client);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.AreEqual(DtlsServerOptions.InitialRetransmissionTimeout, flow.Elapsed);
        Assert.IsTrue(client.IsEstablished);
    }

    [TestMethod]
    public async Task HandshakeAsync_CalledTwice_Throws()
    {
        var client = new UnitTestDtlsClient();
        var (session, flow) = DtlsServerSessionTests.Start(client);
        using var _ = session;
        await session.HandshakeAsync(flow.Exhausted.Token);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.HandshakeAsync(flow.Exhausted.Token));
    }

    private static async Task AssertAlertSentAsync(UnitTestDtlsClient client, DtlsAlertDescription expected, int maximumMessageLength = 65536)
    {
        var (session, flow) = DtlsServerSessionTests.Start(client, maximumMessageLength: maximumMessageLength);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(new DtlsHandshakeOutcome(DtlsHandshakeResult.AlertSent, expected), outcome);
        Assert.AreEqual(new DtlsAlert(true, expected), UnitTestRecords.PlaintextAlert(flow.Sent[^1]));
        Assert.AreEqual(new DtlsAlert(true, expected), client.ReceivedAlerts.Single());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await session.ReceiveAsync(flow.Exhausted.Token));
    }
}
