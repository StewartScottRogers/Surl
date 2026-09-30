using System.Text;
using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 decision 8: <c>APPEND</c>, checked before its <c>+</c> continuation, its message
/// streamed into the store as the bytes sent.
/// </summary>
[TestClass]
public sealed class ImapAppendTests
{
    private const string Login = "a LOGIN u p\r\n";
    private const string LoggedIn = "a OK LOGIN completed\r\n";
    private const string Ready = "+ Ready for literal data\r\n";
    private const string Message = "From: a@x\r\nSubject: hi\r\n\r\nhello\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_AppendAsCurlSendsIt_StoresTheBytesSeenAndAnswersAppendUid()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Server(store), Login + $"b APPEND INBOX (\\Seen) {{33}}\r\n{Message}\r\n", clock, TestContext.CancellationToken, log: log);

        Assert.AreEqual(LoggedIn + Ready + $"b OK [APPENDUID {UidValidity} 1] APPEND completed\r\n", AfterGreeting(connection));
        var summary = Summary(store, "INBOX", 1);
        Assert.AreEqual(MailFlags.Seen, summary.Flags);
        Assert.AreEqual(clock.GetUtcNow(), summary.InternalDate);
        Assert.AreEqual(Message, Body(store, "INBOX", 1));
        Assert.Contains("Message appended to INBOX: 33 bytes", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_AppendWithFlagsAndDate_KeepsTheSystemFlagsAndTheDate()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Create(store, string.Empty, "My Box");

        var connection = await ServeAsync(
            Server(store),
            Login + "b APPEND \"My Box\" (\\Flagged $Label \\Draft \\Custom) \" 1-Feb-2026 10:20:30 +0230\" {5}\r\nhello\r\nc APPEND INBOX () \"28-Feb-2026 23:59:59 -1400\" {0}\r\n\r\n",
            clock,
            TestContext.CancellationToken);

        Assert.AreEqual(
            LoggedIn + Ready + $"b OK [APPENDUID {UidValidity + 1} 1] APPEND completed\r\n" + Ready + $"c OK [APPENDUID {UidValidity} 1] APPEND completed\r\n",
            AfterGreeting(connection));
        var boxed = Summary(store, "My Box", 1);
        Assert.AreEqual(MailFlags.Flagged | MailFlags.Draft, boxed.Flags);
        Assert.AreEqual(new DateTimeOffset(2026, 2, 1, 10, 20, 30, new TimeSpan(2, 30, 0)), boxed.InternalDate);
        var inbox = Summary(store, "INBOX", 1);
        Assert.AreEqual(MailFlags.None, inbox.Flags);
        Assert.AreEqual(new DateTimeOffset(2026, 2, 28, 23, 59, 59, TimeSpan.FromHours(-14)), inbox.InternalDate);
        Assert.AreEqual(string.Empty, Body(store, "INBOX", 1));
    }

    [TestMethod]
    public async Task ServeAsync_AppendToTheSelectedMailbox_ReportsTheNewMessageAsExists()
    {
        var responses = await ResponsesAsync(Login + "b SELECT INBOX\r\nc APPEND INBOX {5}\r\nhello\r\n", TestContext.CancellationToken);

        Assert.EndsWith("b OK [READ-WRITE] SELECT completed\r\n" + Ready + $"* 1 EXISTS\r\nc OK [APPENDUID {UidValidity} 1] APPEND completed\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_AppendWithItsMailboxAsALiteral_ReadsTheMailboxThenTheMessage()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);

        var connection = await ServeAsync(Server(store), Login + "b APPEND {5}\r\ninbox {5}\r\nhello\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual(LoggedIn + Ready + Ready + $"b OK [APPENDUID {UidValidity} 1] APPEND completed\r\n", AfterGreeting(connection));
        Assert.AreEqual("hello", Body(store, "INBOX", 1));
    }

    [TestMethod]
    [DataRow("b APPEND Nope {5}\r\n", "b NO [TRYCREATE] Mailbox does not exist\r\n")]
    [DataRow("b APPEND a//b {5}\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b APPEND &Jjo {5}\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b APPEND INBOX (\\Recent) {5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX (\\*) {5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX (\\Seen {5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX (\\Seen){5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX{5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX x {5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX \"1-Feb-2026 10:20:30 +0200\" {5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX \"x1-Feb-2026 10:20:30 +0200\" {5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX \"01-Fex-2026 10:20:30 +0200\" {5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX \"01-Feb-2026 10:20:30 0200\" {5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX \"01-Feb-2026 10:20:30x+0200\" {5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX \"01-Feb-2026 10:20:30 +02x0\" {5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX \"01-Feb-2026 10:20:30 +0260\" {5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX \"01-Feb-2026 10:20:30 +1401\" {5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX \"01-Jan-0001 00:00:00 +0100\" {5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX \"01-Feb-2026 10:20:30 +0200\"{5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX \"01-Feb-2026 {5}\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b APPEND INBOX\r\n", "b BAD Invalid arguments\r\n")]
    public async Task ServeAsync_AppendRefusedBeforeItsLiteral_SendsNoContinuationAndGoesOn(string command, string expected)
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);

        var connection = await ServeAsync(Server(store), Login + command + "c NOOP\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual(LoggedIn + expected + "c OK NOOP completed\r\n", AfterGreeting(connection));
        store.ReadMailbox(store.ViewFor(null), "INBOX", out var inbox);
        Assert.AreEqual(0, inbox!.Messages.Count);
    }

    [TestMethod]
    public async Task ServeAsync_AppendBeforeALogin_IsRefusedWithoutAContinuation()
    {
        var clock = new ManualTimeProvider();
        var store = AccountStore(clock, "u");

        var connection = await ServeAsync(Server(store, new ScriptedLoginPolicy()), "b APPEND INBOX {5}\r\nc NOOP\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("b NO [AUTHENTICATIONFAILED] Authentication required\r\nc OK NOOP completed\r\n", AfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_AppendPastMaxFilesize_AnswersTooBigBeforeTheContinuation()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var log = new RecordingExchangeLog();
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 10 };

        var connection = await ServeAsync(Server(store), Login + "b APPEND INBOX {11}\r\nc APPEND INBOX {10}\r\n0123456789\r\n", clock, TestContext.CancellationToken, limits, log);

        Assert.EndsWith(
            LoggedIn + "b NO [TOOBIG] Message exceeds the size limit\r\n" + Ready + $"c OK [APPENDUID {UidValidity} 1] APPEND completed\r\n",
            Utf8(connection.WrittenBytes));
        Assert.Contains("APPEND refused: 11 bytes is past --max-filesize", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_AppendWithNoUploadLimit_TakesALiteralPastTheLineLimit()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var body = new string('x', 100);
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 0, MaxLineBytes = 40 };

        var connection = await ServeAsync(Server(store), Login + $"b APPEND INBOX {{100}}\r\n{body}\r\n", clock, TestContext.CancellationToken, limits);

        Assert.EndsWith($"b OK [APPENDUID {UidValidity} 1] APPEND completed\r\n", Utf8(connection.WrittenBytes));
        Assert.AreEqual(body, Body(store, "INBOX", 1));
    }

    [TestMethod]
    public async Task ServeAsync_AppendFollowedByMoreThanItsLineEnd_StoresNothing()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);

        var connection = await ServeAsync(Server(store), Login + "b APPEND INBOX {5}\r\nhello (\\Seen) {5}\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual(LoggedIn + Ready + "b BAD Invalid arguments\r\n", AfterGreeting(connection));
        store.ReadMailbox(store.ViewFor(null), "INBOX", out var inbox);
        Assert.AreEqual(0, inbox!.Messages.Count);
    }

    [TestMethod]
    public async Task ServeAsync_PeerClosesInsideTheLiteral_StoresNothingAndEnds()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);

        var connection = await ServeAsync(Server(store), Login + "b APPEND INBOX {50}\r\nhello", clock, TestContext.CancellationToken);

        Assert.AreEqual(LoggedIn + Ready, AfterGreeting(connection));
        store.ReadMailbox(store.ViewFor(null), "INBOX", out var inbox);
        Assert.AreEqual(0, inbox!.Messages.Count);
    }

    [TestMethod]
    public async Task ServeAsync_PeerClosesBeforeTheLineEndAfterTheLiteral_StoresNothingAndEnds()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);

        var connection = await ServeAsync(Server(store), Login + "b APPEND INBOX {5}\r\nhello", clock, TestContext.CancellationToken);

        Assert.AreEqual(LoggedIn + Ready, AfterGreeting(connection));
        store.ReadMailbox(store.ViewFor(null), "INBOX", out var inbox);
        Assert.AreEqual(0, inbox!.Messages.Count);
    }

    [TestMethod]
    public async Task ServeAsync_LineAfterTheLiteralPastTheLineLimit_AnswersLineTooLongAndCloses()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var limits = ExchangeLimits.Default with { MaxLineBytes = 30 };

        var connection = await ServeAsync(Server(store), Login + "b APPEND INBOX {5}\r\nhello" + new string('x', 40) + "\r\n", clock, TestContext.CancellationToken, limits);

        Assert.AreEqual(LoggedIn + Ready + "b BAD Command line too long\r\n", AfterGreeting(connection));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_AppendWithANonSynchronizingLiteral_IsRefusedAndCloses()
    {
        var responses = await ResponsesAsync(Login + "b APPEND INBOX {5+}\r\nhello\r\n", TestContext.CancellationToken);

        Assert.AreEqual(LoggedIn + "b BAD Non-synchronizing literals are not supported\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_AppendToAFullStore_AnswersOverQuota()
    {
        var clock = new ManualTimeProvider();
        var store = new MailboxStore([], allowAnonymous: true, clock, maxMessages: 1);
        Deliver(store, string.Empty, 1);
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Server(store), Login + "b APPEND INBOX {5}\r\nhello\r\n", clock, TestContext.CancellationToken, log: log);

        Assert.AreEqual(LoggedIn + Ready + "b NO [OVERQUOTA] The mail store is full\r\n", AfterGreeting(connection));
        Assert.Contains("Message refused: the mail store is full", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_AppendPastTheStoresMessageLimit_AnswersTooBig()
    {
        var clock = new ManualTimeProvider();
        var store = new MailboxStore([], allowAnonymous: true, clock, maxMessageBytes: 3);
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Server(store), Login + "b APPEND INBOX {5}\r\nhello\r\n", clock, TestContext.CancellationToken, log: log);

        Assert.AreEqual(LoggedIn + Ready + "b NO [TOOBIG] Message exceeds the size limit\r\n", AfterGreeting(connection));
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_AppendWhoseFileCannotBeWritten_AnswersServerBug()
    {
        var clock = new ManualTimeProvider();
        var store = await MailboxStore.LoadAsync(
            new MailStoreFiles(new UnitTestNoRoomContentFileSystem(), "state"), [], allowAnonymous: true, clock, cancellationToken: TestContext.CancellationToken);
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Server(store), Login + "b APPEND INBOX {5}\r\nhello\r\n", clock, TestContext.CancellationToken, log: log);

        Assert.AreEqual(LoggedIn + Ready + "b NO [SERVERBUG] Could not store the change\r\n", AfterGreeting(connection));
        Assert.Contains("Mail store: no room", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_AppendWhoseIndexCannotBeSaved_StillSucceeds()
    {
        var clock = new ManualTimeProvider();
        var store = await MailboxStore.LoadAsync(
            new MailStoreFiles(new UnitTestUnwritableContentFileSystem(new IOException("disk full")), "state"), [], allowAnonymous: true, clock, cancellationToken: TestContext.CancellationToken);
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Server(store), Login + "b APPEND INBOX {5}\r\nhello\r\n", clock, TestContext.CancellationToken, log: log);

        Assert.EndsWith($"b OK [APPENDUID {UidValidity} 1] APPEND completed\r\n", AfterGreeting(connection));
        Assert.Contains("Mail store: disk full", log.Notes);
        Assert.AreEqual("hello", Body(store, "INBOX", 1));
    }

    private static MailMessageSummary Summary(MailboxStore store, string mailbox, uint uid)
    {
        store.ReadMailbox(store.ViewFor(null), mailbox, out var snapshot);
        return snapshot!.Messages.Single(message => message.Uid == uid);
    }

    private static string Body(MailboxStore store, string mailbox, uint uid)
    {
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.FetchMessage(store.ViewFor(null), mailbox, uid, out var bytes));
        return Encoding.UTF8.GetString(bytes.Span);
    }
}
