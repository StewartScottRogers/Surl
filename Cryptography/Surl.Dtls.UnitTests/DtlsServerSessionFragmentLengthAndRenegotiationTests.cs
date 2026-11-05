namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsServerSession"/> under the client's <c>max_fragment_length</c> (RFC 6066 section 4)
/// and answering a renegotiating <c>ClientHello</c> (ADR-0087 decisions 5 and 6).
/// </summary>
[TestClass]
public sealed class DtlsServerSessionFragmentLengthAndRenegotiationTests
{
    [TestMethod]
    public async Task HandshakeAsync_MaxFragmentLength512_IsEchoedAndEveryServerRecordFitsIt()
    {
        var client = new UnitTestDtlsClient { CipherSuites = [0xC02F], MaxFragmentLength = [1] };
        var (session, flow) = DtlsServerSessionTests.Start(client, UnitTestCertificates.Rsa2048);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);
        await session.SendAsync(new byte[512], flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.IsTrue(client.IsEstablished);
        Assert.AreEqual(512, session.MaximumApplicationDataLength);
        CollectionAssert.AreEqual(
            new byte[] { 1 },
            client.ServerHello!.Extensions.Single(extension => extension.Type == DtlsExtensionType.MaxFragmentLength).Data);
        Assert.HasCount(512, client.ReceivedApplicationData.Single());
        const int overhead = 8 + 16; // AES-GCM's explicit nonce and tag (RFC 5288 section 3).
        var records = flow.Sent.SelectMany(UnitTestRecords.Split).ToList();
        Assert.IsGreaterThan(5, records.Count(record => record.Header.ContentType == DtlsContentType.Handshake), "The certificate was fragmented.");
        foreach (var (header, _) in records)
        {
            Assert.IsLessThanOrEqualTo(512, header.Length - (header.Epoch == 1 ? overhead : 0));
        }

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () => await session.SendAsync(new byte[513], flow.Exhausted.Token));
    }

    [TestMethod]
    public async Task HandshakeAsync_NoMaxFragmentLength_LeavesTheRecordLimitAt16384()
    {
        var client = new UnitTestDtlsClient();
        var (session, flow) = DtlsServerSessionTests.Start(client);
        using var _ = session;

        await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(1 << 14, session.MaximumApplicationDataLength);
        Assert.IsFalse(client.ServerHello!.Extensions.Any(extension => extension.Type == DtlsExtensionType.MaxFragmentLength));
    }

    [TestMethod]
    [DataRow(new byte[] { 0 }, DtlsAlertDescription.IllegalParameter)]
    [DataRow(new byte[] { 5 }, DtlsAlertDescription.IllegalParameter)]
    [DataRow(new byte[] { }, DtlsAlertDescription.DecodeError)]
    [DataRow(new byte[] { 1, 1 }, DtlsAlertDescription.DecodeError)]
    public async Task HandshakeAsync_MaxFragmentLengthNotACodeFrom1To4_SendsItsAlert(byte[] data, DtlsAlertDescription expected)
    {
        var client = new UnitTestDtlsClient { MaxFragmentLength = data };
        var (session, flow) = DtlsServerSessionTests.Start(client);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(new DtlsHandshakeOutcome(DtlsHandshakeResult.AlertSent, expected), outcome);
        Assert.AreEqual(new DtlsAlert(true, expected), client.ReceivedAlerts.Single());
    }

    [TestMethod]
    public async Task ReceiveAsync_ClientHelloInEpoch1_IsAnsweredWithAWarningNoRenegotiationAndDataStillFlows()
    {
        var client = new UnitTestDtlsClient();
        var (session, flow) = DtlsServerSessionTests.Start(client);
        using var _ = session;
        await session.HandshakeAsync(flow.Exhausted.Token);
        var hello = new DtlsHandshakeMessage(DtlsHandshakeType.ClientHello, client.NextMessageSeq, client.BuildHello().Write());
        var sentBefore = flow.Sent.Count;

        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.Handshake, hello.ToUnfragmentedBytes()));
        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.ApplicationData, [0x40]));
        var received = await session.ReceiveAsync(flow.Exhausted.Token);
        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.Handshake, [1]));
        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.ApplicationData, [0x41]));
        var receivedAgain = await session.ReceiveAsync(flow.Exhausted.Token);
        await session.SendAsync(new byte[] { 0x60 }, flow.Exhausted.Token);

        CollectionAssert.AreEqual(new byte[] { 0x40 }, received);
        CollectionAssert.AreEqual(new byte[] { 0x41 }, receivedAgain);
        Assert.AreEqual(new DtlsAlert(false, DtlsAlertDescription.NoRenegotiation), client.ReceivedAlerts.Single());
        Assert.AreEqual(sentBefore + 2, flow.Sent.Count, "One alert, one application data record, and no final flight again.");
        CollectionAssert.AreEqual(new byte[] { 0x60 }, client.ReceivedApplicationData.Single());
    }
}
