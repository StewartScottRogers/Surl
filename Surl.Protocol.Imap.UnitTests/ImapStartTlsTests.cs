using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 decisions 2 and 11: <c>STARTTLS</c>, implicit TLS, and the capabilities before and
/// after TLS and after a login.
/// </summary>
[TestClass]
public sealed class ImapStartTlsTests
{
    private const string AfterLoginCapabilities = "IMAP4rev1 UIDPLUS UNSELECT NAMESPACE CHILDREN ID MOVE APPENDLIMIT=104857600";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_StartTls_DiscardsTheBytesPipelinedAfterItNotesThemAndUpgrades()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var policy = new ScriptedLoginPolicy();
        var connection = new InMemoryConnection(Bytes("a STARTTLS\r\nb LOGIN u p\r\n", "c NOOP\r\n"));

        await Server(AnonymousStore(clock), policy, isStartTlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));

        Assert.AreEqual("a OK Begin TLS negotiation now\r\nc OK NOOP completed\r\n", AfterStartTlsGreeting(connection));
        Assert.IsTrue(connection.UpgradeRequested);
        Assert.IsEmpty(policy.Logins);
        Assert.Contains("Discarded 13 bytes sent after STARTTLS", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsWithNothingPipelined_WritesNoDiscardNote()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(Bytes("a STARTTLS\r\n", "b NOOP\r\n"));

        await Server(AnonymousStore(clock), new ScriptedLoginPolicy(), isStartTlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));

        Assert.AreEqual("a OK Begin TLS negotiation now\r\nb OK NOOP completed\r\n", AfterStartTlsGreeting(connection));
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsWithoutACertificate_AnswersNotAvailableAndStaysPlaintext()
    {
        var clock = new ManualTimeProvider();
        var connection = await ServeAsync(Server(AnonymousStore(clock)), "a STARTTLS\r\nb NOOP\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("a BAD STARTTLS not available\r\nb OK NOOP completed\r\n", AfterGreeting(connection));
        Assert.IsFalse(connection.UpgradeRequested);
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsTwice_AnswersAlreadyUsingTls()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Bytes("a STARTTLS\r\n", "b STARTTLS\r\n"));

        await Server(AnonymousStore(clock), new ScriptedLoginPolicy(), isStartTlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual("a OK Begin TLS negotiation now\r\nb BAD Already using TLS\r\n", AfterStartTlsGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsOnImplicitTls_AnswersAlreadyUsingTls()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Bytes("a STARTTLS\r\n"), initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        await Server(AnonymousStore(clock), new ScriptedLoginPolicy(), isStartTlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(Greeting + "a BAD Already using TLS\r\n", Utf8(connection.WrittenBytes));
        Assert.IsFalse(connection.UpgradeRequested);
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsWithAnArgument_AnswersInvalidArguments()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Bytes("a STARTTLS now\r\n"));

        await Server(AnonymousStore(clock), new ScriptedLoginPolicy(), isStartTlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual("a BAD Invalid arguments\r\n", AfterStartTlsGreeting(connection));
        Assert.IsFalse(connection.UpgradeRequested);
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsAfterALogin_AnswersAlreadyAuthenticated()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Bytes("a LOGIN u p\r\nb STARTTLS\r\n"));

        await Server(AnonymousStore(clock), new ScriptedLoginPolicy(), isStartTlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual("a OK LOGIN completed\r\nb BAD Already authenticated\r\n", AfterStartTlsGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_FailedHandshake_ThrowsAndWritesNothingAfterTheOk()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Bytes("a STARTTLS\r\n", "b NOOP\r\n"), upgradeFails: true);

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(() =>
            Server(AnonymousStore(clock), new ScriptedLoginPolicy(), isStartTlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken)));

        Assert.AreEqual("a OK Begin TLS negotiation now\r\n", AfterStartTlsGreeting(connection));
    }

    // Before TLS the clear password is disabled and CRAM-MD5 alone is offered; over TLS the
    // plain-text mechanisms join it and LOGIN is allowed; after a login no login capability is left.
    [TestMethod]
    public async Task ServeAsync_Capabilities_FollowTheTlsStateAndTheLogin()
    {
        var clock = new ManualTimeProvider();
        var policy = new ScriptedLoginPolicy(isClearPasswordLoginOffered: false)
        {
            IsClearPasswordLoginOfferedOverTls = true,
            SaslMechanisms = ["CRAM-MD5"],
            SaslMechanismsOverTls = ["CRAM-MD5", "PLAIN", "LOGIN"],
        };
        var connection = new InMemoryConnection(Bytes("a CAPABILITY\r\nb STARTTLS\r\n", "c CAPABILITY\r\nd LOGIN u p\r\ne CAPABILITY\r\n"));

        await Server(AnonymousStore(clock), policy, isStartTlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(
            $"* OK [CAPABILITY {Capabilities} STARTTLS LOGINDISABLED AUTH=CRAM-MD5] surl ready\r\n"
            + $"* CAPABILITY {Capabilities} STARTTLS LOGINDISABLED AUTH=CRAM-MD5\r\na OK CAPABILITY completed\r\n"
            + "b OK Begin TLS negotiation now\r\n"
            + $"* CAPABILITY {Capabilities} AUTH=CRAM-MD5 AUTH=PLAIN AUTH=LOGIN\r\nc OK CAPABILITY completed\r\n"
            + "d OK LOGIN completed\r\n"
            + $"* CAPABILITY {AfterLoginCapabilities}\r\ne OK CAPABILITY completed\r\n",
            Utf8(connection.WrittenBytes));
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, policy.Logins.Single().TlsSession);
    }

    [TestMethod]
    public async Task ServeAsync_LoginBeforeTlsWhenOnlyTlsAllowsIt_AnswersEncryptionRequiredAndAfterTlsLogsIn()
    {
        var clock = new ManualTimeProvider();
        var policy = new ScriptedLoginPolicy(isClearPasswordLoginOffered: false) { IsClearPasswordLoginOfferedOverTls = true };
        var connection = new InMemoryConnection(Bytes("a LOGIN u p\r\nb STARTTLS\r\n", "c LOGIN u p\r\nd SELECT INBOX\r\n"));

        await Server(AnonymousStore(clock), policy, isStartTlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.StartsWith(
            "a NO [PRIVACYREQUIRED] Encryption required\r\nb OK Begin TLS negotiation now\r\nc OK LOGIN completed\r\n* FLAGS",
            AfterGreetingOf(connection.WrittenBytes, $"{Capabilities} STARTTLS LOGINDISABLED"));
    }

    // The login with no credentials is asked about again once the connection is TLS.
    [TestMethod]
    public async Task ServeAsync_CommandNeedingALoginAfterTls_AsksAboutTheAnonymousLoginAgain()
    {
        var clock = new ManualTimeProvider();
        var policy = new ScriptedLoginPolicy();
        var connection = new InMemoryConnection(Bytes("a LIST \"\" *\r\nb STARTTLS\r\n", "c LIST \"\" *\r\n"));

        await Server(AnonymousStore(clock), policy, isStartTlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.HasCount(2, policy.Logins);
        Assert.IsNull(policy.Logins[0].TlsSession);
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, policy.Logins[1].TlsSession);
    }

    private static string AfterStartTlsGreeting(InMemoryConnection connection) => AfterGreetingOf(connection.WrittenBytes, Capabilities + " STARTTLS");

    private static string AfterGreetingOf(byte[] writtenBytes, string capabilities)
    {
        var greeting = $"* OK [CAPABILITY {capabilities}] surl ready\r\n";
        var written = Utf8(writtenBytes);
        Assert.StartsWith(greeting, written);
        return written[greeting.Length..];
    }
}
