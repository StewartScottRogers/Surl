using Surl.MailStore;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 decision 7: <c>STORE</c>, <c>COPY</c>, <c>MOVE</c>, <c>EXPUNGE</c> and their
/// <c>UID</c> forms against the selected mailbox.
/// </summary>
[TestClass]
public sealed class ImapMessageChangeTests
{
    private const string Login = "a LOGIN u p\r\n";
    private const string Selected = "b OK [READ-WRITE] SELECT completed\r\n";
    private const string Examined = "b OK [READ-ONLY] EXAMINE completed\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("c STORE 1 +FLAGS \\Deleted\r\n", "* 1 FETCH (FLAGS (\\Deleted))\r\nc OK STORE completed\r\n")]
    [DataRow("c STORE 1:* +FLAGS (\\Seen \\Flagged)\r\n", "* 1 FETCH (FLAGS (\\Flagged \\Seen))\r\n* 2 FETCH (FLAGS (\\Flagged \\Seen))\r\n* 3 FETCH (FLAGS (\\Flagged \\Seen))\r\nc OK STORE completed\r\n")]
    [DataRow("c STORE 1:3 -FLAGS \\SEEN\r\n", "* 2 FETCH (FLAGS ())\r\nc OK STORE completed\r\n")]
    [DataRow("c STORE 2 FLAGS (\\Answered \\Draft)\r\n", "* 2 FETCH (FLAGS (\\Answered \\Draft))\r\nc OK STORE completed\r\n")]
    [DataRow("c STORE 2 FLAGS ()\r\n", "* 2 FETCH (FLAGS ())\r\nc OK STORE completed\r\n")]
    [DataRow("c STORE 2 +FLAGS \\Seen\r\n", "c OK STORE completed\r\n")]
    [DataRow("c STORE 1 +FLAGS $Label\r\n", "c OK STORE completed\r\n")]
    [DataRow("c STORE 1 +FLAGS.SILENT \\Deleted\r\n", "c OK STORE completed\r\n")]
    [DataRow("c UID STORE 2 FLAGS (\\Answered)\r\n", "* 2 FETCH (UID 2 FLAGS (\\Answered))\r\nc OK STORE completed\r\n")]
    [DataRow("c UID STORE 9 FLAGS (\\Answered)\r\n", "c OK STORE completed\r\n")]
    [DataRow("c UID STORE 1:* -FLAGS.SILENT (\\Seen)\r\n", "c OK STORE completed\r\n")]
    [DataRow("c STORE 4 +FLAGS \\Seen\r\n", "c BAD Invalid message sequence number\r\n")]
    [DataRow("c STORE 1 XFLAGS \\Seen\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c STORE 1 +FLAGS\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c STORE 1 +FLAGS \r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c STORE 1 +FLAGS \\Seen \r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c STORE 1 +FLAGS \\Recent\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c STORE 1 +FLAGS (\\Seen\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c STORE 1 +FLAGS (\\Seen) x\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c STORE 1\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c STORE 1 (\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c STORE\r\n", "c BAD Invalid arguments\r\n")]
    public async Task ServeAsync_Store_ChangesTheFlagsAndAnswersEachChangedMessage(string command, string expected)
    {
        var responses = await AfterSelectAsync(command);

        Assert.AreEqual(expected, responses);
    }

    [TestMethod]
    public async Task ServeAsync_Store_KeepsTheFlagsInTheStore()
    {
        var clock = new ManualTimeProvider();
        var store = ThreeMessages(clock);

        await ServeAsync(Server(store), Login + "b SELECT INBOX\r\nc STORE 1,3 +FLAGS.SILENT (\\Deleted)\r\n", clock, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { MailFlags.Deleted, MailFlags.Seen, MailFlags.Deleted }, Flags(store, "INBOX"));
    }

    [TestMethod]
    public async Task ServeAsync_StoreOfAMessageAnotherSessionExpunged_AnswersOnlyTheOthers()
    {
        var clock = new ManualTimeProvider();
        var store = ThreeMessages(clock);
        var view = store.ViewFor(null);
        var connection = new ActAfterWriteConnection(Bytes(Login + "b SELECT INBOX\r\nc STORE 1:2 +FLAGS \\Flagged\r\n"), Selected, () =>
        {
            store.ChangeFlags(view, "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _);
            store.Expunge(view, "INBOX", out _);
        });

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.EndsWith(Selected + "* 2 FETCH (FLAGS (\\Flagged \\Seen))\r\nc OK STORE completed\r\n", AfterGreeting(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("c STORE 1 +FLAGS \\Seen\r\n")]
    [DataRow("c EXPUNGE\r\n")]
    [DataRow("c UID EXPUNGE 1\r\n")]
    [DataRow("c MOVE 1 Sent\r\n")]
    public async Task ServeAsync_ChangeUnderExamine_AnswersReadOnly(string command)
    {
        var responses = await AfterSelectAsync(command, "b EXAMINE INBOX\r\n", Examined);

        Assert.AreEqual("c NO [READ-ONLY] Mailbox is read-only\r\n", responses);
    }

    [TestMethod]
    [DataRow("c COPY 1:3 Sent\r\n", "c OK [COPYUID {0} 1:3 1:3] COPY completed\r\n")]
    [DataRow("c COPY 3,1 Sent\r\n", "c OK [COPYUID {0} 1,3 1:2] COPY completed\r\n")]
    [DataRow("c UID COPY 2 sent\r\n", "c NO [TRYCREATE] Mailbox does not exist\r\n")]
    [DataRow("c UID COPY 2 Sent\r\n", "c OK [COPYUID {0} 2 1] COPY completed\r\n")]
    [DataRow("c UID COPY 9 Sent\r\n", "c OK COPY completed\r\n")]
    [DataRow("c COPY 1 Nope\r\n", "c NO [TRYCREATE] Mailbox does not exist\r\n")]
    [DataRow("c COPY 1 a//b\r\n", "c NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("c COPY 4 Sent\r\n", "c BAD Invalid message sequence number\r\n")]
    [DataRow("c COPY 1\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c COPY 1 Sent x\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c COPY\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c MOVE 1 Nope\r\n", "c NO [TRYCREATE] Mailbox does not exist\r\n")]
    [DataRow("c MOVE 4 Sent\r\n", "c BAD Invalid message sequence number\r\n")]
    [DataRow("c UID MOVE 9 Sent\r\n", "c OK MOVE completed\r\n")]
    [DataRow("c MOVE 1 Sent\r\n", "* OK [COPYUID {0} 1 1] Moved\r\n* 1 EXPUNGE\r\nc OK MOVE completed\r\n")]
    [DataRow("c UID MOVE 1:3 Sent\r\n", "* OK [COPYUID {0} 1:3 1:3] Moved\r\n* 3 EXPUNGE\r\n* 2 EXPUNGE\r\n* 1 EXPUNGE\r\nc OK MOVE completed\r\n")]
    public async Task ServeAsync_CopyOrMove_AnswersAsDecisionSevenSays(string command, string expected)
    {
        var responses = await AfterSelectAsync(command);

        Assert.AreEqual(string.Format(System.Globalization.CultureInfo.InvariantCulture, expected, UidValidity + 1), responses);
    }

    [TestMethod]
    public async Task ServeAsync_CopyUnderExamine_Copies()
    {
        var responses = await AfterSelectAsync("c COPY 2 Sent\r\n", "b EXAMINE INBOX\r\n", Examined);

        Assert.AreEqual($"c OK [COPYUID {UidValidity + 1} 2 1] COPY completed\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_Move_MovesTheMessagesAndLaterCommandsSeeNoSecondExpunge()
    {
        var clock = new ManualTimeProvider();
        var store = ThreeMessages(clock);

        var connection = await ServeAsync(Server(store), Login + "b SELECT INBOX\r\nc MOVE 2 Sent\r\nd NOOP\r\ne FETCH 2 UID\r\n", clock, TestContext.CancellationToken);

        Assert.EndsWith(
            $"* OK [COPYUID {UidValidity + 1} 2 1] Moved\r\n* 2 EXPUNGE\r\nc OK MOVE completed\r\nd OK NOOP completed\r\n* 2 FETCH (UID 3)\r\ne OK FETCH completed\r\n",
            AfterGreeting(connection));
        CollectionAssert.AreEqual(new[] { MailFlags.None, MailFlags.None }, Flags(store, "INBOX"));
        CollectionAssert.AreEqual(new[] { MailFlags.Seen }, Flags(store, "Sent"));
    }

    [TestMethod]
    public async Task ServeAsync_MoveIntoTheSelectedMailbox_ExpungesThenReportsTheCopyAsExists()
    {
        var responses = await AfterSelectAsync("c MOVE 1 INBOX\r\nd NOOP\r\n");

        Assert.AreEqual(
            $"* OK [COPYUID {UidValidity} 1 4] Moved\r\n* 1 EXPUNGE\r\nc OK MOVE completed\r\n* 3 EXISTS\r\nd OK NOOP completed\r\n",
            responses);
    }

    [TestMethod]
    public async Task ServeAsync_CopyToAFullStore_AnswersOverQuota()
    {
        var clock = new ManualTimeProvider();
        var store = new MailboxStore([], allowAnonymous: true, clock, maxMessages: 1);
        Deliver(store, string.Empty, 1);

        var connection = await ServeAsync(Server(store), Login + "b SELECT INBOX\r\nc COPY 1 INBOX\r\n", clock, TestContext.CancellationToken);

        Assert.EndsWith(Selected + "c NO [OVERQUOTA] The mail store is full\r\n", AfterGreeting(connection));
    }

    [TestMethod]
    [DataRow("c EXPUNGE\r\n", "* 3 EXPUNGE\r\n* 1 EXPUNGE\r\nc OK EXPUNGE completed\r\n")]
    [DataRow("c UID EXPUNGE 3:2\r\n", "* 3 EXPUNGE\r\nc OK EXPUNGE completed\r\n")]
    [DataRow("c UID EXPUNGE 2\r\n", "c OK EXPUNGE completed\r\n")]
    [DataRow("c EXPUNGE 1\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c UID EXPUNGE\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c UID EXPUNGE x\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c UID EXPUNGE 1 x\r\n", "c BAD Invalid arguments\r\n")]
    public async Task ServeAsync_Expunge_SendsEachExpungeHighestNumberFirst(string command, string expected)
    {
        var responses = await AfterSelectAsync("c STORE 1,3 +FLAGS.SILENT \\Deleted\r\n" + command);

        Assert.AreEqual("c OK STORE completed\r\n" + expected, responses);
    }

    [TestMethod]
    public async Task ServeAsync_ExpungeWithNothingDeleted_ChangesNothing()
    {
        var responses = await AfterSelectAsync("c EXPUNGE\r\n");

        Assert.AreEqual("c OK EXPUNGE completed\r\n", responses);
    }

    [TestMethod]
    [DataRow("c STORE 1 +FLAGS \\Seen\r\n")]
    [DataRow("c COPY 1 INBOX\r\n")]
    [DataRow("c MOVE 1 INBOX\r\n")]
    [DataRow("c EXPUNGE\r\n")]
    [DataRow("c UID STORE 1 +FLAGS \\Seen\r\n")]
    [DataRow("c UID COPY 1 INBOX\r\n")]
    [DataRow("c UID MOVE 1 INBOX\r\n")]
    [DataRow("c UID EXPUNGE 1\r\n")]
    public async Task ServeAsync_MessageChangeWithNoMailboxSelected_AnswersNoMailboxSelected(string command)
    {
        var responses = await ResponsesAsync(Login + command, TestContext.CancellationToken);

        Assert.AreEqual("a OK LOGIN completed\r\nc BAD No mailbox selected\r\n", responses);
    }

    // INBOX holds three messages, the second one \Seen, beside an empty mailbox Sent.
    private static MailboxStore ThreeMessages(TimeProvider clock)
    {
        var store = AnonymousStore(clock);
        Fill(store);
        return store;
    }

    private static void Fill(MailboxStore store)
    {
        Deliver(store, string.Empty, 3);
        store.ChangeFlags(store.ViewFor(null), "INBOX", 2, MailFlagChange.Add, MailFlags.Seen, out _);
        Create(store, string.Empty, "Sent");
    }

    private static MailFlags[] Flags(MailboxStore store, string mailbox)
    {
        store.ReadMailbox(store.ViewFor(null), mailbox, out var snapshot);
        return [.. snapshot!.Messages.Select(message => message.Flags)];
    }

    // What was written after the SELECT's (or EXAMINE's) tagged response.
    private async Task<string> AfterSelectAsync(string commands, string select = "b SELECT INBOX\r\n", string selected = Selected)
    {
        var responses = await ResponsesAsync(Login + select + commands, TestContext.CancellationToken, Fill);
        return responses[(responses.IndexOf(selected, StringComparison.Ordinal) + selected.Length)..];
    }
}
