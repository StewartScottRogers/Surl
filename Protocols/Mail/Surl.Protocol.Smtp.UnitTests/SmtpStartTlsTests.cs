using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smtp.SmtpTestExchange;

namespace Surl.Protocol.Smtp;

/// <summary>
/// <c>STARTTLS</c> (RFC 3207) and implicit TLS, as ADR-0053 decisions 2 and 5 decide: the
/// capability list per TLS state, the upgrade point, the discard of pipelined bytes and the
/// session starting over.
/// </summary>
[TestClass]
public sealed class SmtpStartTlsTests
{
    private const string ReadyToStartTls = "220 2.0.0 Ready to start TLS\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_EhloOnPlaintextWithACertificate_AdvertisesStartTlsLast()
    {
        var connection = await ServeStartTlsAsync(new InMemoryConnection(Ascii("EHLO c\r\n")));

        Assert.AreEqual(Greeting + EhloReplyWithStartTls, Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_EhloWithoutACertificate_DoesNotAdvertiseStartTls()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Ascii("EHLO c\r\n"));

        await Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(Greeting + EhloReply, Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_EhloOnImplicitTls_DoesNotAdvertiseStartTls()
    {
        var connection = await ServeStartTlsAsync(new InMemoryConnection(Ascii("EHLO c\r\n"), initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession));

        Assert.AreEqual(Greeting + EhloReply, Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_StartTls_Answers220UpgradesAndAdvertisesTheTlsListAfter()
    {
        var connection = await ServeStartTlsAsync(new InMemoryConnection(Ascii("EHLO c\r\nSTARTTLS\r\n", "EHLO c\r\n")));

        Assert.IsTrue(connection.UpgradeRequested);
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, connection.TlsSession);
        Assert.AreEqual(Greeting + EhloReplyWithStartTls + ReadyToStartTls + EhloReply, Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_BytesPipelinedAfterStartTls_AreDiscardedNeverRun()
    {
        var log = new RecordingExchangeLog();
        var connection = await ServeStartTlsAsync(new InMemoryConnection(Ascii("STARTTLS\r\nQUIT\r\n", "NOOP\r\n")), log);

        Assert.AreEqual(Greeting + ReadyToStartTls + "250 2.0.0 OK\r\n", Utf8(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        CollectionAssert.AreEqual(new[] { "Discarded 6 bytes sent after STARTTLS" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsTwice_Answers503TheSecondTime()
    {
        var connection = await ServeStartTlsAsync(new InMemoryConnection(Ascii("STARTTLS\r\n", "STARTTLS\r\n")));

        Assert.AreEqual(Greeting + ReadyToStartTls + "503 5.5.1 Already using TLS\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsWithoutACertificate_Answers454AndStaysPlaintext()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Ascii("STARTTLS\r\n", "NOOP\r\n"));

        await Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.IsFalse(connection.UpgradeRequested);
        Assert.IsNull(connection.TlsSession);
        Assert.AreEqual(Greeting + "454 4.7.0 TLS not available\r\n250 2.0.0 OK\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsWithAnArgument_Answers501AndDoesNotUpgrade()
    {
        var connection = await ServeStartTlsAsync(new InMemoryConnection(Ascii("STARTTLS now\r\n")));

        Assert.IsFalse(connection.UpgradeRequested);
        Assert.AreEqual(Greeting + "501 5.5.4 Syntax: STARTTLS takes no argument\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_FailedUpgrade_ThrowsTheHandshakeFailureAndWritesNothingAfter220()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Ascii("STARTTLS\r\n", "NOOP\r\n"), upgradeFails: true);

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => StartTlsServer(AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken)));

        Assert.IsTrue(connection.UpgradeRequested);
        Assert.AreEqual(Greeting + ReadyToStartTls, Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_MailBeforeTheNewEhlo_Answers503()
    {
        var connection = await ServeStartTlsAsync(new InMemoryConnection(Ascii("EHLO c\r\nSTARTTLS\r\n", "MAIL FROM:<a@x>\r\n")));

        Assert.AreEqual(Greeting + EhloReplyWithStartTls + ReadyToStartTls + "503 5.5.1 Send EHLO or HELO first\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_TransactionBeforeStartTls_IsForgotten()
    {
        var connection = await ServeStartTlsAsync(new InMemoryConnection(Ascii("EHLO c\r\nMAIL FROM:<a@x>\r\nSTARTTLS\r\n", "EHLO c\r\nRCPT TO:<b@y>\r\n")));

        Assert.AreEqual(
            Greeting + EhloReplyWithStartTls + "250 2.1.0 Sender OK\r\n" + ReadyToStartTls + EhloReply + "503 5.5.1 Send MAIL first\r\n",
            Utf8(connection.WrittenBytes));
    }

    private async Task<InMemoryConnection> ServeStartTlsAsync(InMemoryConnection connection, IExchangeLog? log = null)
    {
        var clock = new ManualTimeProvider();
        await StartTlsServer(AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));
        return connection;
    }
}
