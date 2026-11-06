using System.Net;
using System.Security.Authentication;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// STARTTLS and the plain-text phase (BL-537; ADR-0114 decision 2), every client stream and every
/// expected answer written by hand from RFC 6120 sections 5.3, 5.4, 6.5.4 and 9.1.1, over the
/// in-memory TLS upgrade the mail servers' tests use: after the upgrade the script goes on as the
/// plaintext inside TLS. Stream IDs come from <see cref="CountingRandomSource"/>.
/// </summary>
[TestClass]
public sealed class XmppServerStartTlsTests
{
    private const string Streams = "xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'";

    // RFC 6120 section 9.1.1, steps 1 and 7, on one line each.
    private const string ClientHeader =
        "<?xml version='1.0'?><stream:stream from='juliet@im.example.com' to='im.example.com' version='1.0' xml:lang='en' " + Streams + ">";

    private const string StartTls = "<starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>";

    private const string Proceed = "<proceed xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>";

    private const string TlsFailureAndClose = "<failure xmlns='urn:ietf:params:xml:ns:xmpp-tls'/></stream:stream>";

    private const string RequiredStartTlsFeatures =
        "<stream:features><starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'><required/></starttls></stream:features>";

    // The policy below offers these under TLS, ordered and filtered to ADR-0114 decision 3's four.
    private const string TlsFeatures =
        "<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'>" +
        "<mechanism>SCRAM-SHA-256</mechanism><mechanism>SCRAM-SHA-1</mechanism><mechanism>PLAIN</mechanism>" +
        "</mechanisms></stream:features>";

    private const string Close = "</stream:stream>";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_CertificateConfigured_OffersRequiredStartTlsAlone()
    {
        var (answers, _, _) = await ServeAsync(Options(), ClientHeader);

        Assert.AreEqual(ResponseHeader(1) + RequiredStartTlsFeatures, answers);
    }

    [TestMethod]
    public async Task ServeAsync_StartTls_IsAnsweredProceedThenTheUpgrade()
    {
        var (answers, connection, log) = await ServeAsync(Options(), ClientHeader, StartTls);

        Assert.AreEqual(ResponseHeader(1) + RequiredStartTlsFeatures + Proceed, answers);
        Assert.IsTrue(connection.UpgradeRequested);
        Assert.AreEqual(SslProtocols.Tls13, connection.TlsSession?.Protocol);
        CollectionAssert.Contains(log.Notes.ToList(), "Answered <starttls/> with <proceed/>.");
        CollectionAssert.Contains(log.Notes.ToList(), "Upgraded to Tls13.");
    }

    [TestMethod]
    public async Task ServeAsync_StreamRestartedAfterTls_HasANewIdAndOffersSaslAndNotStartTls()
    {
        var (answers, _, _) = await ServeAsync(Options(), ClientHeader, StartTls, ClientHeader, Close);

        Assert.AreEqual(ResponseHeader(1) + RequiredStartTlsFeatures + Proceed + ResponseHeader(2) + TlsFeatures + Close, answers);
    }

    [TestMethod]
    public async Task ServeAsync_BytesPipelinedAfterStartTls_AreDiscardedBeforeTheUpgrade()
    {
        var (answers, _, _) = await ServeAsync(Options(), ClientHeader, StartTls + "<message/>", ClientHeader, Close);

        Assert.AreEqual(ResponseHeader(1) + RequiredStartTlsFeatures + Proceed + ResponseHeader(2) + TlsFeatures + Close, answers);
    }

    [TestMethod]
    public async Task ServeAsync_SaslAuthBeforeTlsWhileRequired_IsEncryptionRequiredAndTheStreamKept()
    {
        var (answers, _, log) = await ServeAsync(
            Options(), ClientHeader, "<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism='PLAIN'>AGp1bGlldAByMG0zMG15cjBtMzA=</auth>", Close);

        Assert.AreEqual(
            ResponseHeader(1) + RequiredStartTlsFeatures + "<failure xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><encryption-required/></failure>" + Close,
            answers);
        CollectionAssert.Contains(log.Notes.ToList(), "A SASL <auth/> arrived before TLS while TLS is required; answered encryption-required.");
    }

    [TestMethod]
    [DataRow("<message to='romeo@example.net'><body>hi</body></message>", "not-authorized")]
    [DataRow("<iq type='get' id='1'><ping xmlns='urn:xmpp:ping'/></iq>", "not-authorized")]
    [DataRow("<proceed xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>", "policy-violation")]
    [DataRow("<thing xmlns='urn:example'/>", "policy-violation")]
    public async Task ServeAsync_OtherElementBeforeTls_EndsTheStreamAsSection5Says(string element, string condition)
    {
        var (answers, connection, _) = await ServeAsync(Options(), ClientHeader, element);

        Assert.AreEqual(ResponseHeader(1) + RequiredStartTlsFeatures + ErrorAndClose(condition), answers);
        Assert.IsFalse(connection.UpgradeRequested);
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsWithoutACertificate_IsTheTlsFailureAndClose()
    {
        var options = Options() with { IsTlsUpgradeAvailable = false };

        var (answers, connection, log) = await ServeAsync(options, ClientHeader, StartTls, "<message/>");

        Assert.AreEqual(
            ResponseHeader(1) + "<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>ANONYMOUS</mechanism></mechanisms></stream:features>" +
            TlsFailureAndClose,
            answers);
        Assert.IsFalse(connection.UpgradeRequested);
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.Contains(log.Notes.ToList(), "A <starttls/> arrived and no certificate is configured; answered the TLS failure and closed.");
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsRepeatedAfterTls_IsTheTlsFailureAndClose()
    {
        var (answers, _, log) = await ServeAsync(Options(), ClientHeader, StartTls, ClientHeader, StartTls);

        Assert.AreEqual(ResponseHeader(1) + RequiredStartTlsFeatures + Proceed + ResponseHeader(2) + TlsFeatures + TlsFailureAndClose, answers);
        CollectionAssert.Contains(log.Notes.ToList(), "A <starttls/> arrived on a connection already TLS; answered the TLS failure and closed.");
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionTlsFromItsFirstByte_OffersSaslAndRefusesStartTls()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(
            Chunks(ClientHeader, StartTls), new IPEndPoint(IPAddress.Loopback, 5223), initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        await new XmppServer(Options()).ServeAsync(connection, Context(new ManualTimeProvider(), log, "xmpps"));

        Assert.AreEqual(ResponseHeader(1) + TlsFeatures + TlsFailureAndClose, Encoding.UTF8.GetString(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_HandshakeFails_ClosesWithNoMoreBytes()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(Chunks(ClientHeader, StartTls, ClientHeader), new IPEndPoint(IPAddress.Loopback, 5222), upgradeFails: true);

        await new XmppServer(Options()).ServeAsync(connection, Context(new ManualTimeProvider(), log));

        Assert.AreEqual(ResponseHeader(1) + RequiredStartTlsFeatures + Proceed, Encoding.UTF8.GetString(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        CollectionAssert.Contains(log.Notes.ToList(), "The TLS handshake failed (The TLS handshake failed.); closed.");
    }

    [TestMethod]
    public async Task ServeAsync_HandshakeNotFinishedWithinTheHeadTimeout_ClosesWithNoMoreBytes()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new UnitTestHangingHandshakeConnection(Chunks(ClientHeader, StartTls));
        var serving = new XmppServer(Options()).ServeAsync(connection, Context(clock, log));
        await WaitUntilAsync(() => connection.UpgradeRequested);

        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        Assert.AreEqual(ResponseHeader(1) + RequiredStartTlsFeatures + Proceed, Encoding.UTF8.GetString(connection.WrittenBytes));
        CollectionAssert.Contains(log.Notes.ToList(), "The TLS handshake did not finish; closed.");
    }

    [TestMethod]
    public async Task ServeAsync_NoHeaderAfterTlsWithinTheHeadTimeout_IsConnectionTimeout()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Chunks(ClientHeader, StartTls), new IPEndPoint(IPAddress.Loopback, 5222), peerHalfClosesWhenExhausted: false);
        var serving = new XmppServer(Options()).ServeAsync(connection, Context(clock, new RecordingExchangeLog()));
        await WaitUntilAsync(() => connection.UpgradeRequested);

        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        Assert.AreEqual(
            ResponseHeader(1) + RequiredStartTlsFeatures + Proceed + ErrorAndClose("connection-timeout"), Encoding.UTF8.GetString(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_AllowPlaintextAuthentication_OffersVoluntaryStartTlsBesideThePlainTextMechanisms()
    {
        var options = Options() with { AllowPlaintextAuthentication = true };

        var (answers, _, _) = await ServeAsync(options, ClientHeader, StartTls, ClientHeader, Close);

        Assert.AreEqual(
            ResponseHeader(1) +
            "<stream:features><starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>" +
            "<mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>ANONYMOUS</mechanism></mechanisms></stream:features>" +
            Proceed + ResponseHeader(2) + TlsFeatures + Close,
            answers);
    }

    [TestMethod]
    public async Task ServeAsync_AllowPlaintextAuthentication_DoesNotRefuseAuthForEncryption()
    {
        var options = Options() with { AllowPlaintextAuthentication = true };

        var (answers, _, _) = await ServeAsync(options, ClientHeader, "<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism='ANONYMOUS'>=</auth>");

        Assert.DoesNotContain("encryption-required", answers);
    }

    [TestMethod]
    public async Task ServeAsync_NoCertificateAndNoPlainTextMechanism_OffersNoFeatures()
    {
        var options = Options() with { IsTlsUpgradeAvailable = false, AuthenticationPolicy = new UnitTestXmppSaslPolicy([], ["PLAIN"]) };

        var (answers, _, _) = await ServeAsync(options, ClientHeader, Close);

        Assert.AreEqual(ResponseHeader(1) + "<stream:features/>" + Close, answers);
    }

    [TestMethod]
    public async Task ServeAsync_Features_AskThePolicyWithTheSchemeAndTheTlsState()
    {
        var policy = new UnitTestXmppSaslPolicy(["ANONYMOUS"], ["PLAIN"]);
        var options = Options() with { AllowPlaintextAuthentication = true, AuthenticationPolicy = policy };

        await ServeAsync(options, ClientHeader, StartTls, ClientHeader, Close);

        Assert.HasCount(2, policy.OfferRequests);
        Assert.AreEqual(new SaslOfferRequest("xmpp", null), policy.OfferRequests[0]);
        Assert.AreEqual(new SaslOfferRequest("xmpp", InMemoryConnection.DefaultUpgradeTlsSession), policy.OfferRequests[1]);
    }

    private static XmppServerOptions Options() => new()
    {
        RandomSource = new CountingRandomSource(),
        IsTlsUpgradeAvailable = true,
        AuthenticationPolicy = new UnitTestXmppSaslPolicy(["ANONYMOUS"], ["PLAIN", "DIGEST-MD5", "SCRAM-SHA-1", "SCRAM-SHA-256"]),
    };

    private static string ResponseHeader(int streamNumber) =>
        $"<?xml version='1.0'?><stream:stream from='im.example.com' id='{streamNumber.ToString("x32", System.Globalization.CultureInfo.InvariantCulture)}' " +
        "version='1.0' xml:lang='en' " + Streams + ">";

    private static string ErrorAndClose(string condition) =>
        $"<stream:error><{condition} xmlns='urn:ietf:params:xml:ns:xmpp-streams'/></stream:error></stream:stream>";

    private static IEnumerable<ReadOnlyMemory<byte>> Chunks(params string[] chunks) =>
        chunks.Select(chunk => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(chunk));

    private async Task<(string Answers, InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(XmppServerOptions options, params string[] chunks)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(Chunks(chunks), new IPEndPoint(IPAddress.Loopback, 5222));

        await new XmppServer(options).ServeAsync(connection, Context(new ManualTimeProvider(), log));

        return (Encoding.UTF8.GetString(connection.WrittenBytes), connection, log);
    }

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The server did not reach the expected state in time.");
            }

            await Task.Delay(5, TestContext.CancellationToken);
        }
    }

    private ExchangeContext Context(TimeProvider clock, IExchangeLog log, string scheme = "xmpp") => new(
        1,
        new ListenUrl(scheme, "im.example.com", 5222).WithBoundPort(5222),
        new IPEndPoint(IPAddress.Loopback, 5222),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        clock,
        TestContext.CancellationToken);
}
