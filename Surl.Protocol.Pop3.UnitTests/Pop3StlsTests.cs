using Surl.Protocol.Abstractions;
using static Surl.Protocol.Pop3.Pop3TestExchange;

namespace Surl.Protocol.Pop3;

/// <summary>
/// ADR-0056 decisions 3 and 8: <c>STLS</c>, what it throws away, the session starting over, and
/// <c>CAPA</c> before and after TLS.
/// </summary>
[TestClass]
public sealed class Pop3StlsTests
{
    private const string CapaStart = "+OK Capability list follows\r\nTOP\r\nUIDL\r\nRESP-CODES\r\nAUTH-RESP-CODE\r\nPIPELINING\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_CapaBeforeAndAfterStls_OffersWhatEachTlsStateAllows()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy
        {
            IsClearPasswordOffered = false,
            IsClearPasswordOfferedOverTls = true,
            SaslMechanisms = ["CRAM-MD5"],
            SaslMechanismsOverTls = ["CRAM-MD5", "OAUTHBEARER", "XOAUTH2", "PLAIN", "LOGIN"],
        };
        var connection = new InMemoryConnection(Ascii("CAPA\r\nSTLS\r\n", "CAPA\r\n"));

        await Server(AccountStore(clock), policy, isStlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(
            CapaStart + "SASL CRAM-MD5\r\nSTLS\r\n.\r\n"
            + "+OK Begin TLS negotiation\r\n"
            + CapaStart + "USER\r\nSASL CRAM-MD5 OAUTHBEARER XOAUTH2 PLAIN LOGIN\r\n.\r\n",
            RepliesAfterGreeting(connection));
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, connection.TlsSession);
    }

    [TestMethod]
    public async Task ServeAsync_BytesPipelinedAfterStls_AreDiscardedAndNoted()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(Ascii("STLS\r\nUSER u\r\nPASS p\r\n", "NOOP\r\n"));

        await Server(AccountStore(clock), isStlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));

        Assert.AreEqual("+OK Begin TLS negotiation\r\n-ERR [AUTH] Authentication required\r\n", RepliesAfterGreeting(connection));
        CollectionAssert.Contains(log.Notes.ToList(), "Discarded 16 bytes sent after STLS");
    }

    [TestMethod]
    public async Task ServeAsync_StlsWithNothingPipelined_NotesNoDiscard()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(Ascii("STLS\r\n"));

        await Server(AccountStore(clock), isStlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));

        Assert.AreEqual("+OK Begin TLS negotiation\r\n", RepliesAfterGreeting(connection));
        Assert.IsEmpty(log.Notes);
    }

    // RFC 2595 section 4: the session starts over, so a USER given before STLS is forgotten, and
    // the check for a login with no credentials is asked again with the TLS session.
    [TestMethod]
    public async Task ServeAsync_AfterStls_ForgetsTheUserAndAsksAboutAnonymousAgain()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy();
        var connection = new InMemoryConnection(Ascii("USER u\r\nSTAT\r\nSTLS\r\n", "PASS p\r\nSTAT\r\n"));

        await Server(AccountStore(clock), policy, isStlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(
            "+OK User accepted\r\n-ERR [AUTH] Authentication required\r\n+OK Begin TLS negotiation\r\n-ERR Send USER first\r\n-ERR [AUTH] Authentication required\r\n",
            RepliesAfterGreeting(connection));
        Assert.HasCount(2, policy.Logins);
        Assert.IsNull(policy.Logins[0].TlsSession);
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, policy.Logins[1].TlsSession);
    }

    [TestMethod]
    public async Task ServeAsync_StlsWithoutACertificate_IsNotAvailableAndNotAdvertised()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AccountStore(clock), "CAPA\r\nSTLS\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual(CapaStart + "USER\r\n.\r\n-ERR STLS not available\r\n", RepliesAfterGreeting(connection));
        Assert.IsFalse(connection.UpgradeRequested);
    }

    [TestMethod]
    public async Task ServeAsync_StlsOverTls_AnswersAlreadyUsingTlsAndIsNotAdvertised()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Ascii("CAPA\r\nSTLS\r\n"), initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        await Server(AccountStore(clock), isStlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(CapaStart + "USER\r\n.\r\n-ERR Already using TLS\r\n", RepliesAfterGreeting(connection));
        Assert.IsFalse(connection.UpgradeRequested);
    }

    [TestMethod]
    public async Task ServeAsync_StlsWithAnArgument_AnswersInvalidArguments()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AccountStore(clock), "STLS now\r\n", clock, TestContext.CancellationToken, isStlsAvailable: true);

        Assert.AreEqual("-ERR Invalid arguments\r\n", RepliesAfterGreeting(connection));
        Assert.IsFalse(connection.UpgradeRequested);
    }

    [TestMethod]
    public async Task ServeAsync_StlsAfterLogin_AnswersAlreadyLoggedIn()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AccountStore(clock), Login + "STLS\r\n", clock, TestContext.CancellationToken, isStlsAvailable: true);

        Assert.AreEqual("-ERR Already logged in\r\n", RepliesAfterLogin(connection));
    }

    [TestMethod]
    public async Task ServeAsync_HandshakeFails_ThrowsAfterTheReplyAndWritesNothingMore()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Ascii("STLS\r\n", "NOOP\r\n"), upgradeFails: true);

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(() =>
            Server(AccountStore(clock), isStlsAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken)));

        Assert.AreEqual("+OK Begin TLS negotiation\r\n", RepliesAfterGreeting(connection));
    }
}
