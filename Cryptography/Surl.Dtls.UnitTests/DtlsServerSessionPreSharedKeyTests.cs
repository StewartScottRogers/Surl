namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsServerSession"/>'s pre-shared-key handshakes (RFC 4279 section 2, RFC 6655,
/// RFC 7252 section 9.1.3.1, ADR-0087 decisions 2, 3 and 10), driven by
/// <see cref="UnitTestDtlsClient"/>, which writes its PSK key exchange from RFC 4279.
/// </summary>
[TestClass]
public sealed class DtlsServerSessionPreSharedKeyTests
{
    private static readonly byte[] AliceKey = "secret"u8.ToArray();

    [TestMethod]
    public async Task HandshakeAsync_PskGcmSuite_CompletesAndExchangesApplicationData()
    {
        await AssertPskHandshakeAndEchoAsync(0x00A8);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow((ushort)0xC0A8)]
    [DataRow((ushort)0xC0A4)]
    public async Task HandshakeAsync_PskCcmSuites_CompleteAndExchangeApplicationData(ushort suite)
    {
        await AssertPskHandshakeAndEchoAsync(suite);
    }

    [TestMethod]
    public async Task HandshakeAsync_PskWithACertificateToo_StillSendsNoCertificateOrServerKeyExchange()
    {
        var client = new UnitTestDtlsClient { CipherSuites = [0x00A8], OffersExtendedMasterSecret = false };
        var (session, flow) = DtlsServerSessionTests.Start(client, configure: options => options with { PreSharedKeys = Lookup });
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.IsTrue(client.IsEstablished);
        CollectionAssert.AreEqual(
            new[] { DtlsHandshakeType.ServerHello, DtlsHandshakeType.ServerHelloDone }, UnitTestRecords.HandshakeTypes(flow.Sent[1]));
    }

    [TestMethod]
    public async Task HandshakeAsync_UnknownPskIdentity_EndsWithDecryptErrorNeverUnknownPskIdentity()
    {
        var client = new UnitTestDtlsClient { CipherSuites = [0x00A8], PskIdentity = "mallory"u8.ToArray() };

        await AssertPskAlertAsync(client, Lookup, DtlsAlertDescription.DecryptError);
    }

    [TestMethod]
    public async Task HandshakeAsync_WrongPskKey_EndsWithDecryptError()
    {
        var client = new UnitTestDtlsClient { CipherSuites = [0x00A8], PskKey = "guess"u8.ToArray() };

        await AssertPskAlertAsync(client, Lookup, DtlsAlertDescription.DecryptError);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(ushort.MaxValue + 1)]
    public async Task HandshakeAsync_LookupKeyEmptyOrLongerThanAPskCanBe_IsTreatedAsAnUnknownIdentity(int keyLength)
    {
        var key = new byte[keyLength];
        var client = new UnitTestDtlsClient { CipherSuites = [0x00A8], PskKey = key };

        await AssertPskAlertAsync(client, _ => key, DtlsAlertDescription.DecryptError);
    }

    [TestMethod]
    public async Task HandshakeAsync_MalformedPskClientKeyExchange_SendsDecodeError()
    {
        var client = new UnitTestDtlsClient
        {
            CipherSuites = [0x00A8],
            ReplacesFinalFlight = c => [c.HandshakeRecord(DtlsHandshakeType.ClientKeyExchange, [0, 5, 1])],
        };

        await AssertPskAlertAsync(client, Lookup, DtlsAlertDescription.DecodeError);
    }

    [TestMethod]
    public async Task HandshakeAsync_CertificateSuiteOnAPskOnlyServer_SendsHandshakeFailure()
    {
        var client = new UnitTestDtlsClient { CipherSuites = [0xC02B] };

        await AssertPskAlertAsync(client, Lookup, DtlsAlertDescription.HandshakeFailure);
    }

    private static byte[]? Lookup(ReadOnlySpan<byte> identity) => identity.SequenceEqual("alice"u8) ? AliceKey : null;

    private static async Task AssertPskHandshakeAndEchoAsync(ushort suite)
    {
        var client = new UnitTestDtlsClient { CipherSuites = [suite] };
        var (session, flow) = StartPskOnly(client, Lookup);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);
        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.ApplicationData, [0x40, 0x01]));
        var request = await session.ReceiveAsync(flow.Exhausted.Token);
        await session.SendAsync(new byte[] { 0x60, 0x45 }, flow.Exhausted.Token);

        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        Assert.AreEqual(suite, session.CipherSuite!.Code);
        Assert.IsTrue(client.IsEstablished);
        Assert.AreEqual(TimeSpan.Zero, flow.Elapsed, "Nothing was retransmitted.");
        CollectionAssert.AreEqual(
            new[] { DtlsHandshakeType.ServerHello, DtlsHandshakeType.ServerHelloDone }, UnitTestRecords.HandshakeTypes(flow.Sent[1]));
        CollectionAssert.AreEqual(new byte[] { 0x40, 0x01 }, request);
        CollectionAssert.AreEqual(new byte[] { 0x60, 0x45 }, client.ReceivedApplicationData.Single());
    }

    private static async Task AssertPskAlertAsync(UnitTestDtlsClient client, DtlsPreSharedKeyLookup lookup, DtlsAlertDescription expected)
    {
        var (session, flow) = StartPskOnly(client, lookup);
        using var _ = session;

        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);

        Assert.AreEqual(new DtlsHandshakeOutcome(DtlsHandshakeResult.AlertSent, expected), outcome);
        Assert.AreEqual(new DtlsAlert(true, expected), client.ReceivedAlerts.Single());
        Assert.IsFalse(client.IsEstablished);
    }

    private static (DtlsServerSession Session, UnitTestDatagramFlow Flow) StartPskOnly(UnitTestDtlsClient client, DtlsPreSharedKeyLookup lookup) =>
        DtlsServerSessionTests.Start(client, configure: options => options with { Certificate = null, PreSharedKeys = lookup });
}
