using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 decision 10 and ADR-0049 section 7: <c>LOGIN</c> through the authentication policy,
/// its notes (ADR-0038), and the implicit check a command makes before any login.
/// </summary>
[TestClass]
public sealed class ImapLoginTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_LoginAccepted_ActsOnTheAccountsMailboxesAndNotesIt()
    {
        var clock = new ManualTimeProvider();
        var store = AccountStore(clock, "u", "v");
        Create(store, "u", "Mine");
        var policy = new ScriptedLoginPolicy(PasswordLoginVerdict.Accepted);
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Server(store, policy), "a LOGIN u secret\r\nb LIST \"\" *\r\n", clock, TestContext.CancellationToken, log: log);

        Assert.AreEqual(
            "a OK LOGIN completed\r\n* LIST (\\HasNoChildren) \"/\" INBOX\r\n* LIST (\\HasNoChildren) \"/\" Mine\r\nb OK LIST completed\r\n",
            AfterGreeting(connection));
        Assert.AreEqual("Login accepted: imap u", log.Notes.Single());
        var login = policy.Logins.Single();
        Assert.AreEqual("imap", login.Scheme);
        Assert.AreEqual("u", login.UserName);
        Assert.AreEqual("secret", Encoding.ASCII.GetString(login.Password!.Value.Span));
        Assert.IsNull(login.TlsSession);
    }

    [TestMethod]
    [DataRow(PasswordLoginVerdict.RefusedCredentials, "a NO [AUTHENTICATIONFAILED] Authentication failed", "Login refused: imap u")]
    [DataRow(PasswordLoginVerdict.RefusedAnonymous, "a NO [AUTHENTICATIONFAILED] Authentication failed", null)]
    [DataRow(PasswordLoginVerdict.RefusedPlaintext, "a NO [PRIVACYREQUIRED] Encryption required", null)]
    [DataRow((PasswordLoginVerdict)99, "a NO [AUTHENTICATIONFAILED] Authentication failed", null)]
    public async Task ServeAsync_LoginRefused_AnswersInTheProtocolsWordsAndStaysLoggedOut(PasswordLoginVerdict verdict, string expected, string? note)
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var server = Server(AccountStore(clock, "u"), new ScriptedLoginPolicy(verdict));

        var connection = await ServeAsync(server, "a LOGIN u wrong\r\nb LOGIN u again\r\n", clock, TestContext.CancellationToken, log: log);

        Assert.StartsWith(expected + "\r\nb ", AfterGreeting(connection));
        CollectionAssert.AreEqual(note is null ? Array.Empty<string>() : new[] { note, note }, log.Notes.ToList());
    }

    [TestMethod]
    public async Task ServeAsync_LoginWhileLoginDisabled_AnswersEncryptionRequiredWithoutAskingThePolicy()
    {
        var clock = new ManualTimeProvider();
        var policy = new ScriptedLoginPolicy(isClearPasswordLoginOffered: false);

        var connection = await ServeAsync(Server(AccountStore(clock, "u"), policy), "a LOGIN u p\r\n", clock, TestContext.CancellationToken);

        Assert.EndsWith("a NO [PRIVACYREQUIRED] Encryption required\r\n", Utf8(connection.WrittenBytes));
        Assert.IsEmpty(policy.Logins);
    }

    [TestMethod]
    public async Task ServeAsync_LoginWithQuotedStringAndLiteral_PassesTheirBytes()
    {
        var clock = new ManualTimeProvider();
        var policy = new ScriptedLoginPolicy();

        var connection = await ServeAsync(Server(AccountStore(clock, "u s"), policy), "a LOGIN \"u s\" {4}\r\np\"\\x\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("+ Ready for literal data\r\na OK LOGIN completed\r\n", AfterGreeting(connection));
        Assert.AreEqual("u s", policy.Logins.Single().UserName);
        Assert.AreEqual("p\"\\x", Encoding.ASCII.GetString(policy.Logins.Single().Password!.Value.Span));
    }

    [TestMethod]
    public async Task ServeAsync_LoginWithEscapesInQuotedStrings_PassesTheUnescapedBytes()
    {
        var clock = new ManualTimeProvider();
        var policy = new ScriptedLoginPolicy();

        await ServeAsync(Server(AccountStore(clock, "u\"s"), policy), "a LOGIN \"u\\\"s\" \"p\\\\w\"\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("u\"s", policy.Logins.Single().UserName);
        Assert.AreEqual("p\\w", Encoding.ASCII.GetString(policy.Logins.Single().Password!.Value.Span));
    }

    [TestMethod]
    [DataRow("a LOGIN\r\n")]
    [DataRow("a LOGIN (\r\n")]
    [DataRow("a LOGIN u\r\n")]
    [DataRow("a LOGIN u (\r\n")]
    [DataRow("a LOGIN u p x\r\n")]
    [DataRow("a LOGIN \"u\\x\" p\r\n")]
    [DataRow("a LOGIN \"u p\r\n")]
    [DataRow("a LOGIN \"u\\\r\n")]
    public async Task ServeAsync_LoginArgumentsThatDoNotParse_AnswersBad(string request)
    {
        var responses = await ResponsesAsync(request, TestContext.CancellationToken);

        Assert.AreEqual("a BAD Invalid arguments\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_LoginAfterALogin_AnswersAlreadyAuthenticated()
    {
        var responses = await ResponsesAsync("a LOGIN u p\r\nb LOGIN u p\r\n", TestContext.CancellationToken);

        Assert.AreEqual("a OK LOGIN completed\r\nb BAD Already authenticated\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_CommandNeedingALoginBeforeOne_AsksThePolicyOnceAndRefuses()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var policy = new ScriptedLoginPolicy();

        var connection = await ServeAsync(Server(AccountStore(clock, "u"), policy), "a SELECT INBOX\r\nb LIST \"\" *\r\n", clock, TestContext.CancellationToken, log: log);

        Assert.AreEqual(
            "a NO [AUTHENTICATIONFAILED] Authentication required\r\nb NO [AUTHENTICATIONFAILED] Authentication required\r\n",
            AfterGreeting(connection));
        var login = policy.Logins.Single();
        Assert.IsNull(login.UserName);
        Assert.IsNull(login.Password);
        CollectionAssert.AreEqual(
            new[] { "SELECT refused: log in first, or give --allow-anonymous", "LIST refused: log in first, or give --allow-anonymous" },
            log.Notes.ToList());
    }

    [TestMethod]
    public async Task ServeAsync_CommandBeforeALoginWithAnonymousAllowed_ActsAsTheAnonymousOwner()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 1);
        var policy = new ScriptedLoginPolicy(anonymousVerdict: PasswordLoginVerdict.AcceptedUnchecked);

        var connection = await ServeAsync(Server(store, policy), "a STATUS INBOX (MESSAGES)\r\nb LOGIN u p\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("* STATUS INBOX (MESSAGES 1)\r\na OK STATUS completed\r\nb BAD Already authenticated\r\n", AfterGreeting(connection));
    }
}
