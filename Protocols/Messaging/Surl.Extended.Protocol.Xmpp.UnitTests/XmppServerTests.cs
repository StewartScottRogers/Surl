using System.Net;
using System.Net.Sockets;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// Opening and closing streams and ending broken ones with stream errors (BL-536; ADR-0114
/// decisions 1, 6 and 7), every client stream and every expected answer written by hand from
/// RFC 6120 sections 4.2 to 4.4, 4.7, 4.9 and 9.1. Stream IDs come from
/// <see cref="CountingRandomSource"/>, so the first is 31 zeros and a 1.
/// </summary>
[TestClass]
public sealed class XmppServerTests
{
    private const string Streams = "xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'";

    // RFC 6120 section 9.1.1, step 1, on one line.
    private const string Rfc6120ClientHeader =
        "<?xml version='1.0'?><stream:stream from='juliet@im.example.com' to='im.example.com' version='1.0' xml:lang='en' " + Streams + ">";

    private const string FirstId = "00000000000000000000000000000001";

    // With no certificate, the plain-text features: the SASL mechanisms the default policy offers.
    private const string PlainFeatures =
        "<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><mechanism>PLAIN</mechanism></mechanisms></stream:features>";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_Rfc6120OpeningHeader_IsAnsweredWithTheRfcsResponseHeaderShape()
    {
        var (answers, log) = await ServeAsync("im.example.com", Rfc6120ClientHeader);

        Assert.AreEqual(ResponseHeader("im.example.com") + PlainFeatures, answers);
        CollectionAssert.Contains(log.Notes.ToList(), "Opened a stream for im.example.com.");
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesTheStream_IsAnsweredWithTheServersClose()
    {
        var (answers, log) = await ServeAsync("im.example.com", Rfc6120ClientHeader, " ", "</stream:stream>");

        Assert.AreEqual(ResponseHeader("im.example.com") + PlainFeatures + "</stream:stream>", answers);
        CollectionAssert.Contains(log.Notes.ToList(), "The client closed the stream; closed.");
    }

    [TestMethod]
    public async Task ServeAsync_InputEndsAfterTheHeader_ClosesWithoutTheServersClose()
    {
        var (answers, _) = await ServeAsync("im.example.com", Rfc6120ClientHeader);

        Assert.DoesNotContain("</stream:stream>", answers);
    }

    [TestMethod]
    public async Task ServeAsync_EmptyStreamHeader_IsAnsweredWithAHeaderThenTheClose()
    {
        var (answers, _) = await ServeAsync("im.example.com", "<stream:stream version='1.0' " + Streams + "/>");

        Assert.AreEqual(ResponseHeader("im.example.com") + PlainFeatures + "</stream:stream>", answers);
    }

    [TestMethod]
    public async Task ServeAsync_NoTo_OpensAStreamForTheFirstDomainServed()
    {
        var (answers, _) = await ServeAsync("localhost", "<stream:stream version='1.0' " + Streams + ">");

        StringAssert.StartsWith(answers, ResponseHeader("localhost"), StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("LOCALHOST.")]
    [DataRow("chat.example.com")]
    public async Task ServeAsync_ToNamingADomainServed_OpensAStreamForIt(string to)
    {
        var options = new XmppServerOptions { MailDomains = ["Chat.Example.com"], RandomSource = new CountingRandomSource() };

        var (answers, _) = await ServeAsync("localhost", options, ExchangeLimits.Default, $"<stream:stream to='{to}' version='1.0' {Streams}>");

        StringAssert.StartsWith(answers, ResponseHeader(XmppDomains.Normalized(to)), StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ServeAsync_ClientLanguageAndHigherMinorVersion_AreAnsweredWithItsLanguageAndVersion10()
    {
        var (answers, _) = await ServeAsync("localhost", $"<stream:stream version='1.5' xml:lang='fr&amp;' {Streams}>");

        StringAssert.StartsWith(answers, ResponseHeader("localhost").Replace("xml:lang='en'", "xml:lang='fr&amp;'", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ServeAsync_EachStream_GetsANewId()
    {
        var server = new XmppServer();
        var first = await WrittenAsync(server, Rfc6120ClientHeader);
        var second = await WrittenAsync(server, Rfc6120ClientHeader);

        Assert.MatchesRegex("id='[0-9a-f]{32}'", first);
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    [DataRow("2.0")]
    [DataRow("0.9")]
    [DataRow("1")]
    [DataRow("1.0.0")]
    [DataRow("one.0")]
    [DataRow("1.x")]
    [DataRow(".0")]
    [DataRow("1.99999999999")]
    [DataRow(" 1.0")]
    [DataRow("-1.0")]
    [DataRow(null)]
    public async Task ServeAsync_VersionOtherThanMajor1_IsUnsupportedVersion(string? version)
    {
        var versionAttribute = version is null ? string.Empty : $"version='{version}' ";

        var (answers, _) = await ServeAsync("im.example.com", $"<stream:stream to='im.example.com' {versionAttribute}{Streams}>");

        Assert.AreEqual(ResponseHeader("im.example.com") + ErrorAndClose("unsupported-version"), answers);
    }

    [TestMethod]
    public async Task ServeAsync_WrongTo_IsHostUnknown()
    {
        var (answers, log) = await ServeAsync("im.example.com", $"<stream:stream to='example.net' version='1.0' {Streams}>");

        Assert.AreEqual(ResponseHeader("im.example.com") + ErrorAndClose("host-unknown"), answers);
        CollectionAssert.Contains(
            log.Notes.ToList(),
            "The stream header's to named example.net, and no such domain is served; answered the stream error host-unknown and closed.");
    }

    [TestMethod]
    public async Task ServeAsync_NoToAndNoDomainServed_IsHostUnknownFromNoDomain()
    {
        var connection = new UnitTestXmppClientConnection { LocalEndPoint = new UnixDomainSocketEndPoint("/tmp/surl.sock") };
        connection.Send($"<stream:stream version='1.0' {Streams}>");
        connection.Close();

        await new XmppServer(new XmppServerOptions { RandomSource = new CountingRandomSource() })
            .ServeAsync(connection, Context("0.0.0.0", new ManualTimeProvider(), cancellation: TestContext.CancellationToken) with { LocalEndPoint = connection.LocalEndPoint });

        Assert.AreEqual(ResponseHeader(null) + ErrorAndClose("host-unknown"), connection.Written);
    }

    [TestMethod]
    [DataRow("<!DOCTYPE stream>", "restricted-xml")]
    [DataRow("<!-- hello -->", "restricted-xml")]
    [DataRow("<?xml-stylesheet href='x'?>", "restricted-xml")]
    [DataRow("<stream:stream version='1.0' xmlns='jabber:server' xmlns:stream='http://etherx.jabber.org/streams'>", "invalid-namespace")]
    [DataRow("<message xmlns='jabber:client'/>", "not-well-formed")]
    public async Task ServeAsync_RefusedXml_EndsTheStreamWithItsError(string client, string condition)
    {
        var (answers, _) = await ServeAsync("im.example.com", client);

        Assert.AreEqual(ResponseHeader("im.example.com") + ErrorAndClose(condition), answers);
    }

    [TestMethod]
    [DataRow("<message to='romeo@example.net'><body>hi</body></message>", "not-authorized")]
    [DataRow("<presence/>", "not-authorized")]
    [DataRow("<iq type='get' id='1'><ping xmlns='urn:xmpp:ping'/></iq>", "not-authorized")]
    [DataRow("<thing/>", "unsupported-stanza-type")]
    [DataRow("<message><body>&lt;&ent;</body></message>", "restricted-xml")]
    [DataRow("<message><body></message>", "not-well-formed")]
    public async Task ServeAsync_ElementBeforeAuthentication_EndsTheStreamWithItsError(string element, string condition)
    {
        var (answers, _) = await ServeAsync("im.example.com", Rfc6120ClientHeader, element);

        Assert.AreEqual(ResponseHeader("im.example.com") + PlainFeatures + ErrorAndClose(condition), answers);
    }

    [TestMethod]
    public async Task ServeAsync_ElementOverTheSizeLimit_IsPolicyViolation()
    {
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 10000 };
        var options = new XmppServerOptions { RandomSource = new CountingRandomSource() };

        var (answers, _) = await ServeAsync(
            "im.example.com", options, limits, Rfc6120ClientHeader, "<message><body>", new string('x', 4000), new string('x', 4000), new string('x', 4000), "</body></message>");

        Assert.AreEqual(ResponseHeader("im.example.com") + PlainFeatures + ErrorAndClose("policy-violation"), answers);
    }

    [TestMethod]
    public async Task ServeAsync_NoHeaderWithinTheHeadTimeout_IsConnectionTimeout()
    {
        var clock = new ManualTimeProvider();
        var connection = new UnitTestXmppClientConnection();
        connection.Send("<?xml version='1.0'?><stream:str");
        var serving = new XmppServer(new XmppServerOptions { RandomSource = new CountingRandomSource() })
            .ServeAsync(connection, Context("localhost", clock, cancellation: TestContext.CancellationToken));
        await WaitUntilAsync(() => clock.PendingTimerCount > 0 && connection.ReadsWaiting > 1);

        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        Assert.AreEqual(ResponseHeader("localhost") + ErrorAndClose("connection-timeout"), connection.Written);
    }

    [TestMethod]
    public async Task ServeAsync_InputEndsBeforeTheHeader_ClosesWithNoBytes()
    {
        var (answers, _) = await ServeAsync("localhost", "<stream:stre");

        Assert.AreEqual(string.Empty, answers);
    }

    [TestMethod]
    [DataRow(false, "connection-timeout")]
    [DataRow(true, "system-shutdown")]
    public async Task ServeAsync_CancelledWhileOpen_SaysFarewellWithItsError(bool shutdown, string condition)
    {
        using var shutdownSource = new CancellationTokenSource();
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(shutdownSource.Token);
        var connection = new UnitTestXmppClientConnection();
        connection.Send(Rfc6120ClientHeader);
        var context = Context("im.example.com", new ManualTimeProvider(), cancellation: exchange.Token) with { ShutdownToken = shutdownSource.Token };
        var serving = new XmppServer(new XmppServerOptions { RandomSource = new CountingRandomSource() }).ServeAsync(connection, context);
        await WaitUntilAsync(() => connection.Written.EndsWith(PlainFeatures, StringComparison.Ordinal) && connection.ReadsWaiting > 1);

        await (shutdown ? shutdownSource : exchange).CancelAsync();
        await serving;

        Assert.AreEqual(ResponseHeader("im.example.com") + PlainFeatures + ErrorAndClose(condition), connection.Written);
    }

    [TestMethod]
    public async Task ServeAsync_CancelledAndTheFarewellIsCutOff_EndsQuietly()
    {
        using var exchange = new CancellationTokenSource();
        await exchange.CancelAsync();
        var connection = new UnitTestXmppClientConnection { WritesAreCancelled = true };
        var log = new RecordingExchangeLog();

        await new XmppServer().ServeAsync(connection, Context("localhost", new ManualTimeProvider(), log, exchange.Token));

        Assert.AreEqual(string.Empty, connection.Written);
        CollectionAssert.Contains(log.Notes.ToList(), "The exchange was cancelled; answered the stream error connection-timeout and closed.");
    }

    [TestMethod]
    public async Task ServeAsync_ReadFails_EndsQuietly()
    {
        var connection = new UnitTestXmppClientConnection { ReadsFail = true };
        var log = new RecordingExchangeLog();

        await new XmppServer().ServeAsync(connection, Context("localhost", new ManualTimeProvider(), log, TestContext.CancellationToken));

        Assert.AreEqual(string.Empty, connection.Written);
        CollectionAssert.Contains(log.Notes.ToList(), "The connection failed (The connection was reset.).");
    }

    [TestMethod]
    public async Task ServeAsync_WritingTheStreamErrorFails_EndsQuietly()
    {
        var connection = new UnitTestXmppClientConnection { WritesFail = true };
        connection.Send("<message/>");
        var log = new RecordingExchangeLog();

        await new XmppServer().ServeAsync(connection, Context("localhost", new ManualTimeProvider(), log, TestContext.CancellationToken));

        CollectionAssert.Contains(log.Notes.ToList(), "The connection failed (The connection was reset.).");
    }

    [TestMethod]
    public void Schemes_AreXmpp()
    {
        CollectionAssert.AreEqual(new[] { "xmpp" }, new XmppServer().Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new XmppServer();
        var context = Context("localhost", new ManualTimeProvider());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(new UnitTestXmppClientConnection(), null!));
    }

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_Refusal_IsAHeaderAndResourceConstraint(ConnectionRefusal refusal)
    {
        var connection = new UnitTestXmppClientConnection();

        await new XmppServer(new XmppServerOptions { RandomSource = new CountingRandomSource() })
            .WriteRefusalAsync(connection, refusal, TestContext.CancellationToken);

        Assert.AreEqual(ResponseHeader("127.0.0.1") + ErrorAndClose("resource-constraint"), connection.Written);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_OnAUnixSocket_LeavesFromOut()
    {
        var connection = new UnitTestXmppClientConnection { LocalEndPoint = new UnixDomainSocketEndPoint("/tmp/surl.sock") };

        await new XmppServer(new XmppServerOptions { RandomSource = new CountingRandomSource() })
            .WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken);

        Assert.AreEqual(ResponseHeader(null) + ErrorAndClose("resource-constraint"), connection.Written);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullConnection_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => new XmppServer().WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken).AsTask());
    }

    private static string ResponseHeader(string? from) =>
        "<?xml version='1.0'?><stream:stream " + (from is null ? string.Empty : $"from='{from}' ") + $"id='{FirstId}' version='1.0' xml:lang='en' " + Streams + ">";

    private static string ErrorAndClose(string condition) =>
        $"<stream:error><{condition} xmlns='urn:ietf:params:xml:ns:xmpp-streams'/></stream:error></stream:stream>";

    private Task<(string Answers, RecordingExchangeLog Log)> ServeAsync(string listenHost, params string[] chunks) =>
        ServeAsync(listenHost, new XmppServerOptions { RandomSource = new CountingRandomSource() }, ExchangeLimits.Default, chunks);

    private async Task<(string Answers, RecordingExchangeLog Log)> ServeAsync(
        string listenHost, XmppServerOptions options, ExchangeLimits limits, params string[] chunks)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(
            chunks.Select(chunk => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(chunk)), new IPEndPoint(IPAddress.Loopback, 5222));

        await new XmppServer(options).ServeAsync(connection, Context(listenHost, new ManualTimeProvider(), log, TestContext.CancellationToken) with { Limits = limits });

        return (Encoding.UTF8.GetString(connection.WrittenBytes), log);
    }

    private async Task<string> WrittenAsync(XmppServer server, string client)
    {
        var connection = new InMemoryConnection([Encoding.UTF8.GetBytes(client)], new IPEndPoint(IPAddress.Loopback, 5222));
        await server.ServeAsync(connection, Context("im.example.com", new ManualTimeProvider(), cancellation: TestContext.CancellationToken));
        return Encoding.UTF8.GetString(connection.WrittenBytes);
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

    private static ExchangeContext Context(string listenHost, TimeProvider clock, IExchangeLog? log = null, CancellationToken cancellation = default) => new(
        1,
        new ListenUrl("xmpp", listenHost, 5222).WithBoundPort(5222),
        new IPEndPoint(IPAddress.Loopback, 5222),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log ?? new RecordingExchangeLog(),
        clock,
        cancellation);
}
