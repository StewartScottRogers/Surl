using System.Text;
using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 decision 4: <c>FETCH</c> and <c>UID FETCH</c> against the store - sequence sets, data
/// items, sections, partials, <c>\Seen</c>, and messages another session expunged.
/// </summary>
[TestClass]
public sealed class ImapFetchTests
{
    private const string Login = "a LOGIN u p\r\n";
    private const string Select = "b SELECT INBOX\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("c FETCH 1:2 FLAGS\r\n", "* 1 FETCH (FLAGS ())\r\n* 2 FETCH (FLAGS ())\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 2:1 UID\r\n", "* 1 FETCH (UID 1)\r\n* 2 FETCH (UID 2)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH *:2 UID\r\n", "* 2 FETCH (UID 2)\r\n* 3 FETCH (UID 3)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 3,1 UID\r\n", "* 1 FETCH (UID 1)\r\n* 3 FETCH (UID 3)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH * (UID FLAGS)\r\n", "* 3 FETCH (UID 3 FLAGS ())\r\nc OK FETCH completed\r\n")]
    [DataRow("c fetch 1 rfc822.size\r\n", "* 1 FETCH (RFC822.SIZE 21)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 4 FLAGS\r\n", "c BAD Invalid message sequence number\r\n")]
    [DataRow("c FETCH 1:4 FLAGS\r\n", "c BAD Invalid message sequence number\r\n")]
    [DataRow("c FETCH 4:1 FLAGS\r\n", "c BAD Invalid message sequence number\r\n")]
    [DataRow("c UID FETCH 9 FLAGS\r\n", "c OK FETCH completed\r\n")]
    [DataRow("c UID FETCH 2,9 FLAGS\r\n", "* 2 FETCH (UID 2 FLAGS ())\r\nc OK FETCH completed\r\n")]
    [DataRow("c UID FETCH 9:* UID\r\n", "* 3 FETCH (UID 3)\r\nc OK FETCH completed\r\n")]
    [DataRow("c UID FETCH 2:* RFC822.SIZE\r\n", "* 2 FETCH (UID 2 RFC822.SIZE 21)\r\n* 3 FETCH (UID 3 RFC822.SIZE 21)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 1 FAST\r\n", "* 1 FETCH (FLAGS () INTERNALDATE \"29-Sep-2026 08:00:00 +0000\" RFC822.SIZE 21)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 1 FULL\r\n", "* 1 FETCH (FLAGS () INTERNALDATE \"29-Sep-2026 08:00:00 +0000\" RFC822.SIZE 21 ENVELOPE (NIL \"1\" NIL NIL NIL NIL NIL NIL NIL NIL) BODY (\"TEXT\" \"PLAIN\" (\"CHARSET\" \"US-ASCII\") NIL NIL \"7BIT\" 7 1))\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 1 BODY.PEEK[]\r\n", "* 1 FETCH (BODY[] {21}\r\nSubject: 1\r\n\r\nhello\r\n)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 1 (BODY.PEEK[HEADER] BODY.PEEK[TEXT]<2.100>)\r\n", "* 1 FETCH (BODY[HEADER] {14}\r\nSubject: 1\r\n\r\n BODY[TEXT]<2> {5}\r\nllo\r\n)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 1 BODY.PEEK[]<30.5>\r\n", "* 1 FETCH (BODY[]<30> {0}\r\n)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 1 BODY.PEEK[header.fields.not (subject)]\r\n", "* 1 FETCH (BODY[HEADER.FIELDS.NOT (SUBJECT)] {2}\r\n\r\n)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 1 BODY.PEEK[HEADER.FIELDS (\"Subject\" X-None)]\r\n", "* 1 FETCH (BODY[HEADER.FIELDS (SUBJECT X-NONE)] {14}\r\nSubject: 1\r\n\r\n)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 1 BODY.PEEK[1.MIME]\r\n", "* 1 FETCH (BODY[1.MIME] {14}\r\nSubject: 1\r\n\r\n)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 1 BODY.PEEK[2]\r\n", "* 1 FETCH (BODY[2] {0}\r\n)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 1 BODY.PEEK[1.1]\r\n", "* 1 FETCH (BODY[1.1] {0}\r\n)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 1 BODY.PEEK[1.TEXT]\r\n", "* 1 FETCH (BODY[1.TEXT] {0}\r\n)\r\nc OK FETCH completed\r\n")]
    [DataRow("c FETCH 1 RFC822.HEADER\r\n", "* 1 FETCH (RFC822.HEADER {14}\r\nSubject: 1\r\n\r\n)\r\nc OK FETCH completed\r\n")]
    public async Task ServeAsync_Fetch_AnswersAsTheAdrSays(string command, string expected)
    {
        var responses = await AfterSelectAsync(command, store => Deliver(store, string.Empty, 3));

        Assert.AreEqual(expected, responses);
    }

    [TestMethod]
    [DataRow("c FETCH\r\n")]
    [DataRow("c FETCH 1\r\n")]
    [DataRow("c FETCH 1 \r\n")]
    [DataRow("c FETCH x FLAGS\r\n")]
    [DataRow("c FETCH 0 FLAGS\r\n")]
    [DataRow("c FETCH 01 FLAGS\r\n")]
    [DataRow("c FETCH 1, FLAGS\r\n")]
    [DataRow("c FETCH 1:2:3 FLAGS\r\n")]
    [DataRow("c FETCH 4294967296 FLAGS\r\n")]
    [DataRow("c FETCH 1 FLAGS x\r\n")]
    [DataRow("c FETCH 1 XYZ\r\n")]
    [DataRow("c FETCH 1 [\r\n")]
    [DataRow("c FETCH 1 ()\r\n")]
    [DataRow("c FETCH 1 (FLAGS\r\n")]
    [DataRow("c FETCH 1 (FLAGS UID\r\n")]
    [DataRow("c FETCH 1 (ALL)\r\n")]
    [DataRow("c FETCH 1 BODY.PEEK\r\n")]
    [DataRow("c FETCH 1 BODY[\r\n")]
    [DataRow("c FETCH 1 BODY[TEXT\r\n")]
    [DataRow("c FETCH 1 BODY[MIME]\r\n")]
    [DataRow("c FETCH 1 BODY[1.]\r\n")]
    [DataRow("c FETCH 1 BODY[.1]\r\n")]
    [DataRow("c FETCH 1 BODY[TEXT.]\r\n")]
    [DataRow("c FETCH 1 BODY[0]\r\n")]
    [DataRow("c FETCH 1 BODY[01]\r\n")]
    [DataRow("c FETCH 1 BODY[99999999999]\r\n")]
    [DataRow("c FETCH 1 BODY[1..TEXT]\r\n")]
    [DataRow("c FETCH 1 BODY[HEADER.FIELDS]\r\n")]
    [DataRow("c FETCH 1 BODY[HEADER.FIELDS SUBJECT]\r\n")]
    [DataRow("c FETCH 1 BODY[HEADER.FIELDS ()]\r\n")]
    [DataRow("c FETCH 1 BODY[HEADER.FIELDS (\"\")]\r\n")]
    [DataRow("c FETCH 1 BODY[HEADER.FIELDS (\"A:B\")]\r\n")]
    [DataRow("c FETCH 1 BODY[HEADER.FIELDS (\"A B\")]\r\n")]
    [DataRow("c FETCH 1 BODY[HEADER.FIELDS (SUBJECT]\r\n")]
    [DataRow("c FETCH 1 BODY[HEADER.FIELDS (SUBJECT)\r\n")]
    [DataRow("c FETCH 1 BODY[]<0>\r\n")]
    [DataRow("c FETCH 1 BODY[]<x.1>\r\n")]
    [DataRow("c FETCH 1 BODY[]<0.0>\r\n")]
    [DataRow("c FETCH 1 BODY[]<0.x>\r\n")]
    [DataRow("c FETCH 1 BODY[]<0.1\r\n")]
    [DataRow("c FETCH 1 BODY[]<99999999999.1>\r\n")]
    [DataRow("c UID FETCH\r\n")]
    [DataRow("c UID FETCH 1\r\n")]
    public async Task ServeAsync_FetchArgumentsThatDoNotParse_AnswersInvalidArguments(string command)
    {
        var responses = await AfterSelectAsync(command, store => Deliver(store, string.Empty, 1));

        Assert.AreEqual("c BAD Invalid arguments\r\n", responses);
    }

    [TestMethod]
    [DataRow("c UID\r\n")]
    [DataRow("c UID XYZZY 1\r\n")]
    [DataRow("c UID (\r\n")]
    public async Task ServeAsync_UidWithoutAUidCommand_AnswersNotRecognized(string command)
    {
        var responses = await AfterSelectAsync(command, store => Deliver(store, string.Empty, 1));

        Assert.AreEqual("c BAD Command not recognized\r\n", responses);
    }

    [TestMethod]
    [DataRow("c FETCH 1 FLAGS\r\n")]
    [DataRow("c UID FETCH 1 FLAGS\r\n")]
    [DataRow("c SEARCH ALL\r\n")]
    [DataRow("c UID SEARCH ALL\r\n")]
    public async Task ServeAsync_FetchOrSearchWithNoMailboxSelected_AnswersNoMailboxSelected(string command)
    {
        var responses = await ResponsesAsync(Login + command, TestContext.CancellationToken, store => Deliver(store, string.Empty, 1));

        Assert.AreEqual("a OK LOGIN completed\r\nc BAD No mailbox selected\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_FetchInAnEmptyMailbox_AnswersEveryNumberInvalid()
    {
        var responses = await AfterSelectAsync("c FETCH * FLAGS\r\nd UID FETCH 1:* FLAGS\r\n");

        Assert.AreEqual("c BAD Invalid message sequence number\r\nd OK FETCH completed\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_FetchBody_SetsSeenAndSaysSoOnce()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 1);

        var connection = await ServeAsync(Server(store), Login + Select + "c FETCH 1 BODY[TEXT]\r\nd FETCH 1 BODY[TEXT]\r\ne FETCH 1 FLAGS\r\n", clock, TestContext.CancellationToken);

        Assert.EndsWith(
            "* 1 FETCH (FLAGS (\\Seen) BODY[TEXT] {7}\r\nhello\r\n)\r\nc OK FETCH completed\r\n"
            + "* 1 FETCH (BODY[TEXT] {7}\r\nhello\r\n)\r\nd OK FETCH completed\r\n"
            + "* 1 FETCH (FLAGS (\\Seen))\r\ne OK FETCH completed\r\n",
            AfterGreeting(connection));
        Assert.AreEqual(MailFlags.Seen, Flags(store, 1));
    }

    [TestMethod]
    [DataRow("c FETCH 1 RFC822\r\n", "* 1 FETCH (FLAGS (\\Seen) RFC822 {21}\r\nSubject: 1\r\n\r\nhello\r\n)\r\n", MailFlags.Seen)]
    [DataRow("c FETCH 1 RFC822.TEXT\r\n", "* 1 FETCH (FLAGS (\\Seen) RFC822.TEXT {7}\r\nhello\r\n)\r\n", MailFlags.Seen)]
    [DataRow("c UID FETCH 1 BODY[]<0.7>\r\n", "* 1 FETCH (UID 1 FLAGS (\\Seen) BODY[]<0> {7}\r\nSubject)\r\n", MailFlags.Seen)]
    [DataRow("c FETCH 1 RFC822.HEADER\r\n", "* 1 FETCH (RFC822.HEADER {14}\r\nSubject: 1\r\n\r\n)\r\n", MailFlags.None)]
    [DataRow("c FETCH 1 BODY.PEEK[TEXT]\r\n", "* 1 FETCH (BODY[TEXT] {7}\r\nhello\r\n)\r\n", MailFlags.None)]
    [DataRow("c FETCH 1 (FLAGS BODY.PEEK[TEXT])\r\n", "* 1 FETCH (FLAGS () BODY[TEXT] {7}\r\nhello\r\n)\r\n", MailFlags.None)]
    public async Task ServeAsync_FetchItem_SetsSeenOnlyForANonPeekBody(string command, string expected, MailFlags flags)
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 1);

        var connection = await ServeAsync(Server(store), Login + Select + command, clock, TestContext.CancellationToken);

        Assert.EndsWith(expected + "c OK FETCH completed\r\n", AfterGreeting(connection));
        Assert.AreEqual(flags, Flags(store, 1));
    }

    [TestMethod]
    public async Task ServeAsync_FetchBodyUnderExamine_LeavesSeenUnset()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 1);

        var connection = await ServeAsync(Server(store), Login + "b EXAMINE INBOX\r\nc FETCH 1 BODY[TEXT]\r\n", clock, TestContext.CancellationToken);

        Assert.EndsWith("* 1 FETCH (BODY[TEXT] {7}\r\nhello\r\n)\r\nc OK FETCH completed\r\n", AfterGreeting(connection));
        Assert.AreEqual(MailFlags.None, Flags(store, 1));
    }

    [TestMethod]
    public async Task ServeAsync_FetchOfFlagsAndDatesSetOnAppend_WritesThemAsTheAdrSays()
    {
        var date = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromMinutes(-330));
        var responses = await AfterSelectAsync("c FETCH 1:2 (FLAGS INTERNALDATE)\r\n", store =>
        {
            Append(store, "x", MailFlags.Answered | MailFlags.Flagged | MailFlags.Deleted | MailFlags.Seen | MailFlags.Draft, date);
            Append(store, "y", MailFlags.Draft, date.ToOffset(TimeSpan.FromHours(2)));
        });

        Assert.AreEqual(
            "* 1 FETCH (FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft) INTERNALDATE \"02-Jan-2026 03:04:05 -0530\")\r\n"
            + "* 2 FETCH (FLAGS (\\Draft) INTERNALDATE \"02-Jan-2026 10:34:05 +0200\")\r\n"
            + "c OK FETCH completed\r\n",
            responses);
    }

    [TestMethod]
    public async Task ServeAsync_FetchOfAMessageAnotherSessionExpunged_AnswersItEmptyAndNotesIt()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);
        var log = new RecordingExchangeLog();
        var connection = new ActAfterWriteConnection(Bytes(Login + Select + "c FETCH 1:2 (UID RFC822.SIZE BODY[])\r\nd NOOP\r\n"), "SELECT completed", () =>
        {
            store.ChangeFlags(store.ViewFor(null), "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _);
            store.Expunge(store.ViewFor(null), "INBOX", out _);
        });

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));

        Assert.EndsWith(
            "* 1 FETCH (UID 1 RFC822.SIZE 0 BODY[] {0}\r\n)\r\n"
            + "* 2 FETCH (UID 2 FLAGS (\\Seen) RFC822.SIZE 21 BODY[] {21}\r\nSubject: 2\r\n\r\nhello\r\n)\r\n"
            + "c OK FETCH completed\r\n* 1 EXPUNGE\r\nd OK NOOP completed\r\n",
            AfterGreeting(connection.WrittenBytes));
        Assert.Contains("Message 1 in INBOX was expunged by another session", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_FetchAfterAnotherSessionDeletedTheMailbox_AnswersEveryMessageEmpty()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Create(store, string.Empty, "Box");
        store.Append(store.ViewFor(null), "Box", "x"u8, MailFlags.None, null, out _);
        var connection = new ActAfterWriteConnection(Bytes(Login + "b SELECT Box\r\nc FETCH 1 (FLAGS INTERNALDATE)\r\n"), "SELECT completed", () =>
            store.DeleteMailbox(store.ViewFor(null), "Box"));

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.EndsWith("* 1 FETCH (FLAGS () INTERNALDATE \"01-Jan-1970 00:00:00 +0000\")\r\nc OK FETCH completed\r\n", AfterGreeting(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_FetchAMessageWhoseFileCannotBeRead_AnswersServerBugAfterTheMessagesBefore()
    {
        var files = new UnitTestUnreadableContentFileSystem();
        var clock = new ManualTimeProvider();
        var store = await MailboxStore.LoadAsync(new MailStoreFiles(files, "state"), [], allowAnonymous: true, clock, cancellationToken: TestContext.CancellationToken);
        Deliver(store, string.Empty, 2);
        await store.SaveChangesAsync(TestContext.CancellationToken);
        var log = new RecordingExchangeLog();
        var connection = new ActAfterWriteConnection(Bytes(Login + Select + "c FETCH 1:2 (UID BODY[TEXT])\r\n"), "* 1 FETCH", () => files.FailReads = true);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));

        Assert.EndsWith(
            "* 1 FETCH (UID 1 FLAGS (\\Seen) BODY[TEXT] {7}\r\nhello\r\n)\r\nc NO [SERVERBUG] Could not read the message\r\n",
            AfterGreeting(connection.WrittenBytes));
        Assert.Contains("Mail store: message file unreadable", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_FetchOfAMultipartMessage_AnswersItsPartsAndStructure()
    {
        var responses = await AfterSelectAsync(
            "c FETCH 1 (BODY.PEEK[2.1] BODY.PEEK[1] BODYSTRUCTURE)\r\n",
            store => Append(store, MessageStructureSamples.Multipart));

        Assert.AreEqual(
            "* 1 FETCH (BODY[2.1] {10}\r\ninner text BODY[1] {5}\r\nhello BODYSTRUCTURE "
            + MessageStructureSamples.MultipartBodyStructure + ")\r\nc OK FETCH completed\r\n",
            responses);
    }

    [TestMethod]
    public async Task ServeAsync_FetchEnvelopeWithBytesAQuotedStringCannotHold_SendsThemAsLiterals()
    {
        var responses = await AfterSelectAsync("c FETCH 1 ENVELOPE\r\n", store => Append(store, "Subject: caf\u00E9 \"x\\y\"\r\n\r\n"));

        Assert.AreEqual(
            "* 1 FETCH (ENVELOPE (NIL {11}\r\ncaf\u00E9 \"x\\y\" NIL NIL NIL NIL NIL NIL NIL NIL))\r\nc OK FETCH completed\r\n",
            responses);
    }

    [TestMethod]
    public async Task ServeAsync_FetchEnvelopeWithAQuoteAndBackslash_EscapesThem()
    {
        var responses = await AfterSelectAsync("c FETCH 1 ENVELOPE\r\n", store => Append(store, "Subject: a \"b\" \\c\r\n\r\n"));

        Assert.AreEqual(
            "* 1 FETCH (ENVELOPE (NIL \"a \\\"b\\\" \\\\c\" NIL NIL NIL NIL NIL NIL NIL NIL))\r\nc OK FETCH completed\r\n",
            responses);
    }

    private static MailFlags Flags(MailboxStore store, uint uid)
    {
        store.ReadMailbox(store.ViewFor(null), "INBOX", out var snapshot);
        return snapshot!.Messages.Single(message => message.Uid == uid).Flags;
    }

    private static void Append(MailboxStore store, string message, MailFlags flags = MailFlags.None, DateTimeOffset? date = null) =>
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(store.ViewFor(null), "INBOX", Encoding.UTF8.GetBytes(message), flags, date, out _));

    // What was written after the SELECT's tagged response.
    private async Task<string> AfterSelectAsync(string commands, Action<MailboxStore>? fill = null)
    {
        var responses = await ResponsesAsync(Login + Select + commands, TestContext.CancellationToken, fill);
        const string selected = "b OK [READ-WRITE] SELECT completed\r\n";
        return responses[(responses.IndexOf(selected, StringComparison.Ordinal) + selected.Length)..];
    }
}
