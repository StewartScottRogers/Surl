using System.Text;
using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Pop3.Pop3TestExchange;

namespace Surl.Protocol.Pop3;

/// <summary>
/// ADR-0056 decisions 2, 3, 4, 6 and 7: the greeting, <c>CAPA</c>, <c>USER</c>/<c>PASS</c>
/// through the authentication policy, the maildrop lock, and a maildrop command before a login.
/// </summary>
[TestClass]
public sealed class Pop3LoginTests
{
    private const string CapaWithUser = "+OK Capability list follows\r\nTOP\r\nUIDL\r\nRESP-CODES\r\nAUTH-RESP-CODE\r\nPIPELINING\r\nUSER\r\n.\r\n";
    private const string CapaWithoutUser = "+OK Capability list follows\r\nTOP\r\nUIDL\r\nRESP-CODES\r\nAUTH-RESP-CODE\r\nPIPELINING\r\n.\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy();

        Assert.ThrowsExactly<ArgumentNullException>(() => new Pop3ProtocolServer(null!, policy, AccountStore(clock)));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Pop3ProtocolServer(policy, null!, AccountStore(clock)));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Pop3ProtocolServer(policy, policy, null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        var clock = new ManualTimeProvider();
        var server = Server(AccountStore(clock));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(clock, TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public void Schemes_IsPop3Alone()
    {
        CollectionAssert.AreEqual(new[] { "pop3" }, Server(AccountStore(new ManualTimeProvider())).Schemes.ToList());
    }

    [TestMethod]
    public async Task ServeAsync_PeerClosesAtOnce_SendsTheGreetingAlone()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AccountStore(clock), string.Empty, clock, TestContext.CancellationToken);

        Assert.AreEqual(Greeting, Utf8(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_CapaBeforeAndAfterLogin_OffersUserOnlyBefore()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AccountStore(clock), "CAPA\r\n" + Login + "CAPA\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual(CapaWithUser + LoginReplies + CapaWithoutUser, RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_CapaWithoutClearPasswordOffer_LeavesOutUser()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { IsClearPasswordOffered = false };

        var connection = await ServeAsync(AccountStore(clock), "capa\r\nCAPA x\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual(CapaWithoutUser + "-ERR Invalid arguments\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_LoginAccepted_OpensTheMaildropAndNotesIt()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var policy = new Pop3TestPolicy();

        var connection = await ServeAsync(AccountStore(clock, Message), Login + "STAT\r\n", clock, TestContext.CancellationToken, policy, log: log);

        Assert.AreEqual("+OK 1 133\r\n", RepliesAfterLogin(connection));
        var login = policy.Logins.Single();
        Assert.AreEqual("pop3", login.Scheme);
        Assert.AreEqual("u", login.UserName);
        Assert.AreEqual("p", Encoding.ASCII.GetString(login.Password!.Value.Span));
        CollectionAssert.AreEqual(new[] { "Login accepted: pop3 u", "Maildrop opened: 1 messages, 133 octets" }, log.Notes.ToList());
    }

    [TestMethod]
    public async Task ServeAsync_PasswordWithSpaces_IsTheWholeRestOfTheLine()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { Verdict = PasswordLoginVerdict.RefusedCredentials };

        await ServeAsync(AccountStore(clock), "USER u\r\nPASS a b  c\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("a b  c", Encoding.ASCII.GetString(policy.Logins.Single().Password!.Value.Span));
    }

    [TestMethod]
    public async Task ServeAsync_LoginRefused_AnswersAuthenticationFailedNotesItAndForgetsTheUser()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(AccountStore(clock), "USER u\r\nPASS wrong\r\nPASS p\r\nSTAT\r\n", clock, TestContext.CancellationToken, log: log);

        Assert.AreEqual(
            "+OK User accepted\r\n-ERR [AUTH] Authentication failed\r\n-ERR Send USER first\r\n-ERR [AUTH] Authentication required\r\n",
            RepliesAfterGreeting(connection));
        CollectionAssert.AreEqual(new[] { "Login refused: pop3 u", "STAT refused: log in first, or give --allow-anonymous" }, log.Notes.ToList());
    }

    [TestMethod]
    public async Task ServeAsync_LoginRefusedAsPlainText_AnswersEncryptionRequiredWithNoLoginNote()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var policy = new Pop3TestPolicy { Verdict = PasswordLoginVerdict.RefusedPlaintext };

        var connection = await ServeAsync(AccountStore(clock), Login, clock, TestContext.CancellationToken, policy, log: log);

        Assert.AreEqual("+OK User accepted\r\n-ERR [AUTH] Encryption required\r\n", RepliesAfterGreeting(connection));
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_LoginRefusedAsAnonymous_AnswersAuthenticationFailed()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { Verdict = PasswordLoginVerdict.RefusedAnonymous };

        var connection = await ServeAsync(AccountStore(clock), Login, clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("+OK User accepted\r\n-ERR [AUTH] Authentication failed\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_LoginAcceptedUnchecked_OpensTheAnonymousMaildrop()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var policy = new Pop3TestPolicy { Verdict = PasswordLoginVerdict.AcceptedUnchecked };

        var connection = await ServeAsync(AnonymousStore(clock, Message), Login + "STAT\r\n", clock, TestContext.CancellationToken, policy, log: log);

        Assert.AreEqual("+OK 1 133\r\n", RepliesAfterLogin(connection));
        CollectionAssert.AreEqual(new[] { "Maildrop opened: 1 messages, 133 octets" }, log.Notes.ToList());
    }

    [TestMethod]
    public async Task ServeAsync_UserWithoutClearPasswordOffer_AnswersEncryptionRequiredSoThePasswordIsNeverSent()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { IsClearPasswordOffered = false };

        var connection = await ServeAsync(AccountStore(clock), Login, clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("-ERR [AUTH] Encryption required\r\n-ERR Send USER first\r\n", RepliesAfterGreeting(connection));
        Assert.IsEmpty(policy.Logins);
    }

    [TestMethod]
    [DataRow("USER\r\n")]
    [DataRow("USER a b\r\n")]
    [DataRow("USER a\x01\r\n")]
    public async Task ServeAsync_UserWithoutOneName_AnswersInvalidArgumentsAndForgetsTheUser(string user)
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AccountStore(clock), "USER u\r\n" + user + "PASS p\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("+OK User accepted\r\n-ERR Invalid arguments\r\n-ERR Send USER first\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_PassWithoutPassword_AnswersInvalidArgumentsAndForgetsTheUser()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy();

        var connection = await ServeAsync(AccountStore(clock), "USER u\r\nPASS\r\nPASS p\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("+OK User accepted\r\n-ERR Invalid arguments\r\n-ERR Send USER first\r\n", RepliesAfterGreeting(connection));
        Assert.IsEmpty(policy.Logins);
    }

    [TestMethod]
    public async Task ServeAsync_LoginCommandsAfterLogin_AnswerAlreadyLoggedIn()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AccountStore(clock), Login + "USER u\r\nPASS p\r\nSTLS\r\nAPOP u d\r\nAUTH\r\nAUTH PLAIN\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual(string.Concat(Enumerable.Repeat("-ERR Already logged in\r\n", 6)), RepliesAfterLogin(connection));
    }

    [TestMethod]
    public async Task ServeAsync_StlsApopAndAuthBeforeLogin_AreNotOffered()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AccountStore(clock), "STLS\r\nAPOP u d\r\nAUTH\r\nAUTH PLAIN\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual(
            "-ERR STLS not available\r\n-ERR Unsupported authentication mechanism\r\n+OK SASL mechanisms follow\r\n.\r\n-ERR Unsupported authentication mechanism\r\n",
            RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_MaildropHeldByAnotherSession_AnswersInUseAndStaysUnauthorized()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = AccountStore(clock, Message);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(store.ViewFor("u"), out var held));

        using (held)
        {
            var connection = await ServeAsync(store, Login + "STAT\r\n", clock, TestContext.CancellationToken, log: log);

            Assert.AreEqual(
                "+OK User accepted\r\n-ERR [IN-USE] Maildrop is locked by another session\r\n-ERR [AUTH] Authentication required\r\n",
                RepliesAfterGreeting(connection));
            CollectionAssert.AreEqual(
                new[] { "Login accepted: pop3 u", "Maildrop locked by another session", "STAT refused: log in first, or give --allow-anonymous" },
                log.Notes.ToList());
        }
    }

    [TestMethod]
    public async Task ServeAsync_SessionEnds_ReleasesTheMaildropLock()
    {
        var clock = new ManualTimeProvider();
        var store = AccountStore(clock, Message);

        await ServeAsync(store, Login, clock, TestContext.CancellationToken);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(store.ViewFor("u"), out var again));
        again!.Dispose();
    }

    [TestMethod]
    public async Task ServeAsync_AccountTheStoreDoesNotHold_OpensAnEmptyMaildrop()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { Verdict = PasswordLoginVerdict.Accepted };

        var connection = await ServeAsync(AccountStore(clock, Message), "USER other\r\nPASS x\r\nSTAT\r\nUIDL\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("+OK 0 0\r\n+OK Unique-ID listing follows\r\n.\r\n", RepliesAfterLogin(connection));
    }

    [TestMethod]
    public async Task ServeAsync_MaildropCommandsBeforeLogin_AskThePolicyOnceAndRefuseEach()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var policy = new Pop3TestPolicy();

        var connection = await ServeAsync(AccountStore(clock, Message), "LIST\r\nRETR 1\r\nNOOP\r\n", clock, TestContext.CancellationToken, policy, log: log);

        Assert.AreEqual(string.Concat(Enumerable.Repeat("-ERR [AUTH] Authentication required\r\n", 3)), RepliesAfterGreeting(connection));
        var anonymous = policy.Logins.Single();
        Assert.IsNull(anonymous.UserName);
        Assert.IsNull(anonymous.Password);
        CollectionAssert.AreEqual(
            new[]
            {
                "LIST refused: log in first, or give --allow-anonymous",
                "RETR refused: log in first, or give --allow-anonymous",
                "NOOP refused: log in first, or give --allow-anonymous",
            },
            log.Notes.ToList());
    }

    [TestMethod]
    public async Task ServeAsync_MaildropCommandUnderAllowAnonymous_OpensTheAnonymousMaildropAndRunsIt()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var policy = new Pop3TestPolicy { AnonymousVerdict = PasswordLoginVerdict.AcceptedUnchecked };

        var connection = await ServeAsync(AnonymousStore(clock, Message), "STAT\r\nCAPA\r\nUSER u\r\n", clock, TestContext.CancellationToken, policy, log: log);

        Assert.AreEqual("+OK 1 133\r\n" + CapaWithoutUser + "-ERR Already logged in\r\n", RepliesAfterGreeting(connection));
        CollectionAssert.AreEqual(new[] { "Maildrop opened: 1 messages, 133 octets" }, log.Notes.ToList());
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousMaildropHeldByAnotherSession_AnswersTheCommandInUse()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock, Message);
        var policy = new Pop3TestPolicy { AnonymousVerdict = PasswordLoginVerdict.AcceptedUnchecked };
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(store.ViewFor(null), out var held));

        using (held)
        {
            var connection = await ServeAsync(store, "RETR 1\r\n", clock, TestContext.CancellationToken, policy);

            Assert.AreEqual("-ERR [IN-USE] Maildrop is locked by another session\r\n", RepliesAfterGreeting(connection));
        }
    }

    [TestMethod]
    [DataRow("\r\n")]
    [DataRow(" LIST\r\n")]
    [DataRow("XYZZY\r\n")]
    [DataRow("LI\x01ST\r\n")]
    public async Task ServeAsync_NoKnownCommand_AnswersNotRecognized(string line)
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AccountStore(clock), line, clock, TestContext.CancellationToken);

        Assert.AreEqual("-ERR Command not recognized\r\n", RepliesAfterGreeting(connection));
    }
}
