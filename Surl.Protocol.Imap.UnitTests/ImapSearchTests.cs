using System.Text;
using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 decision 7: <c>SEARCH</c> and <c>UID SEARCH</c>, each search key once, against five
/// messages that differ in flags, header fields, dates and size.
/// </summary>
[TestClass]
public sealed class ImapSearchTests
{
    private const string Login = "a LOGIN u p\r\n";
    private const string Select = "b SELECT INBOX\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("ALL", "1 2 3 4 5")]
    [DataRow("ANSWERED", "1")]
    [DataRow("UNANSWERED", "2 3 4 5")]
    [DataRow("DELETED", "2")]
    [DataRow("UNDELETED", "1 3 4 5")]
    [DataRow("DRAFT", "2")]
    [DataRow("UNDRAFT", "1 3 4 5")]
    [DataRow("FLAGGED", "2")]
    [DataRow("UNFLAGGED", "1 3 4 5")]
    [DataRow("SEEN", "1")]
    [DataRow("unseen", "2 3 4 5")]
    [DataRow("NEW", "")]
    [DataRow("OLD", "1 2 3 4 5")]
    [DataRow("RECENT", "")]
    [DataRow("BCC hidden", "1")]
    [DataRow("CC CC@Z", "1")]
    [DataRow("FROM ann", "1")]
    [DataRow("TO bob", "1")]
    [DataRow("SUBJECT \"hello world\"", "1")]
    [DataRow("HEADER X-Tag blue", "1")]
    [DataRow("HEADER x-tag \"\"", "1")]
    [DataRow("BODY \"body text\"", "1")]
    [DataRow("BODY hello", "")]
    [DataRow("TEXT subject", "1 2 3 4 5")]
    [DataRow("TEXT hello", "1")]
    [DataRow("KEYWORD $Label1", "")]
    [DataRow("UNKEYWORD $Label1", "1 2 3 4 5")]
    [DataRow("LARGER 200", "3")]
    [DataRow("SMALLER 45", "5")]
    [DataRow("BEFORE 29-Sep-2026", "3")]
    [DataRow("ON 29-sep-2026", "1 4 5")]
    [DataRow("SINCE 29-Sep-2026", "1 2 4 5")]
    [DataRow("SINCE \"5-OCT-2026\"", "2")]
    [DataRow("SENTBEFORE 1-Jan-2000", "4")]
    [DataRow("SENTON 1-Oct-2026", "2")]
    [DataRow("SENTSINCE 1-Mar-2001", "1 2 5")]
    [DataRow("UID 2:3", "2 3")]
    [DataRow("UID *", "5")]
    [DataRow("2,4", "2 4")]
    [DataRow("4:*", "4 5")]
    [DataRow("9", "")]
    [DataRow("NOT SEEN", "2 3 4 5")]
    [DataRow("OR SEEN DELETED", "1 2")]
    [DataRow("(SEEN ANSWERED)", "1")]
    [DataRow("SEEN UNANSWERED", "")]
    [DataRow("CHARSET UTF-8 SUBJECT hello", "1")]
    [DataRow("charset \"us-ascii\" ALL", "1 2 3 4 5")]
    public async Task ServeAsync_SearchKey_AnswersTheMessagesThatMatch(string keys, string expected)
    {
        var responses = await AfterSelectAsync($"c SEARCH {keys}\r\n", FillWithFiveMessages);

        Assert.AreEqual($"* SEARCH{(expected.Length == 0 ? string.Empty : " " + expected)}\r\nc OK SEARCH completed\r\n", responses);
    }

    [TestMethod]
    [DataRow("c SEARCH\r\n")]
    [DataRow("c SEARCH \r\n")]
    [DataRow("c SEARCH XYZZY\r\n")]
    [DataRow("c SEARCH ALL x\r\n")]
    [DataRow("c SEARCH ALL)\r\n")]
    [DataRow("c SEARCH (ALL\r\n")]
    [DataRow("c SEARCH 1:x\r\n")]
    [DataRow("c SEARCH 0\r\n")]
    [DataRow("c SEARCH SUBJECT\r\n")]
    [DataRow("c SEARCH HEADER Subject\r\n")]
    [DataRow("c SEARCH HEADER\r\n")]
    [DataRow("c SEARCH KEYWORD\r\n")]
    [DataRow("c SEARCH KEYWORD (\r\n")]
    [DataRow("c SEARCH LARGER\r\n")]
    [DataRow("c SEARCH LARGER x\r\n")]
    [DataRow("c SEARCH LARGER 99999999999\r\n")]
    [DataRow("c SEARCH BEFORE 32-Sep-2026\r\n")]
    [DataRow("c SEARCH BEFORE\r\n")]
    [DataRow("c SEARCH UID\r\n")]
    [DataRow("c SEARCH UID x\r\n")]
    [DataRow("c SEARCH UID 1:\r\n")]
    [DataRow("c SEARCH NOT\r\n")]
    [DataRow("c SEARCH NOT x\r\n")]
    [DataRow("c SEARCH OR\r\n")]
    [DataRow("c SEARCH OR ALL\r\n")]
    [DataRow("c SEARCH CHARSET\r\n")]
    [DataRow("c SEARCH CHARSET UTF-8\r\n")]
    [DataRow("c UID SEARCH\r\n")]
    public async Task ServeAsync_SearchArgumentsThatDoNotParse_AnswersInvalidArguments(string command)
    {
        var responses = await AfterSelectAsync(command, FillWithFiveMessages);

        Assert.AreEqual("c BAD Invalid arguments\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_SearchWithAnotherCharset_AnswersBadCharset()
    {
        var responses = await AfterSelectAsync("c SEARCH CHARSET KOI8-R ALL\r\n", FillWithFiveMessages);

        Assert.AreEqual("c NO [BADCHARSET (US-ASCII UTF-8)] Unsupported charset\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_UidSearchAfterAnExpunge_AnswersUidsWhereSearchAnswersNumbers()
    {
        var responses = await AfterSelectAsync("c SEARCH ALL\r\nd UID SEARCH ALL\r\ne UID SEARCH UID 2\r\n", store =>
        {
            Deliver(store, string.Empty, 3);
            store.ChangeFlags(store.ViewFor(null), "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _);
            store.Expunge(store.ViewFor(null), "INBOX", out _);
        });

        Assert.AreEqual(
            "* SEARCH 1 2\r\nc OK SEARCH completed\r\n* SEARCH 2 3\r\nd OK SEARCH completed\r\n* SEARCH 2\r\ne OK SEARCH completed\r\n",
            responses);
    }

    [TestMethod]
    public async Task ServeAsync_SearchInAnEmptyMailbox_AnswersNoNumber()
    {
        var responses = await AfterSelectAsync("c SEARCH ALL\r\nd SEARCH *\r\ne UID SEARCH UID *\r\n");

        Assert.AreEqual("* SEARCH\r\nc OK SEARCH completed\r\n* SEARCH\r\nd OK SEARCH completed\r\n* SEARCH\r\ne OK SEARCH completed\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_SearchDoesNotSetSeen()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 1);

        await ServeAsync(Server(store), Login + Select + "c SEARCH TEXT hello\r\n", clock, TestContext.CancellationToken);

        store.ReadMailbox(store.ViewFor(null), "INBOX", out var snapshot);
        Assert.AreEqual(MailFlags.None, snapshot!.Messages[0].Flags);
    }

    [TestMethod]
    public async Task ServeAsync_SearchAfterAnotherSessionExpungedAMessage_LeavesItOut()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);
        var connection = new ActAfterWriteConnection(Bytes(Login + Select + "c SEARCH ALL\r\n"), "SELECT completed", () =>
        {
            store.ChangeFlags(store.ViewFor(null), "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _);
            store.Expunge(store.ViewFor(null), "INBOX", out _);
        });

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.EndsWith("* SEARCH 2\r\nc OK SEARCH completed\r\n", AfterGreeting(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_SearchOfAMessageWhoseFileCannotBeRead_AnswersServerBug()
    {
        var files = new UnitTestUnreadableContentFileSystem();
        var clock = new ManualTimeProvider();
        var store = await MailboxStore.LoadAsync(new MailStoreFiles(files, "state"), [], allowAnonymous: true, clock, cancellationToken: TestContext.CancellationToken);
        Deliver(store, string.Empty, 1);
        await store.SaveChangesAsync(TestContext.CancellationToken);
        files.FailReads = true;
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Server(store), Login + Select + "c SEARCH SEEN\r\nd SEARCH BODY hello\r\n", clock, TestContext.CancellationToken, log: log);

        Assert.EndsWith("* SEARCH\r\nc OK SEARCH completed\r\nd NO [SERVERBUG] Could not read the message\r\n", AfterGreeting(connection));
        Assert.Contains("Mail store: message file unreadable", log.Notes);
    }

    private static void FillWithFiveMessages(MailboxStore store)
    {
        var september = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        Append(store, "From: Ann <ann@x>\r\nTo: bob@y\r\nCc: cc@z\r\nBcc: hidden@z\r\nSubject: Hello World\r\nDate: Tue, 29 Sep 2026 07:30:00 +0000\r\nX-Tag: blue\r\n\r\nThe body text\r\n", MailFlags.Seen | MailFlags.Answered, null);
        Append(store, "Subject: other\r\nDate: 1 Oct 26 10:00 -0700\r\n\r\nshort\r\n", MailFlags.Deleted | MailFlags.Flagged | MailFlags.Draft, september.AddDays(34));
        Append(store, "Subject: third\r\nDate: garbage\r\n\r\n" + new string('x', 200), MailFlags.None, september);
        Append(store, "Subject: fourth\r\nDate: Mon, 5 Jan 99 00:00 +0000\r\n\r\nx", MailFlags.None, null);
        Append(store, "Date: 3 Mar 101 00:00 +0000\r\n\r\nno subject", MailFlags.None, null);
    }

    private static void Append(MailboxStore store, string message, MailFlags flags, DateTimeOffset? date) =>
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(store.ViewFor(null), "INBOX", Encoding.UTF8.GetBytes(message), flags, date, out _));

    // What was written after the SELECT's tagged response.
    private async Task<string> AfterSelectAsync(string commands, Action<MailboxStore>? fill = null)
    {
        var responses = await ResponsesAsync(Login + Select + commands, TestContext.CancellationToken, fill);
        const string selected = "b OK [READ-WRITE] SELECT completed\r\n";
        return responses[(responses.IndexOf(selected, StringComparison.Ordinal) + selected.Length)..];
    }
}
