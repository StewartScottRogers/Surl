using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 decisions 5 and 6: <c>SELECT</c>, <c>EXAMINE</c>, <c>STATUS</c>, <c>LIST</c>,
/// <c>LSUB</c>, <c>CHECK</c>, <c>CLOSE</c> and <c>UNSELECT</c> against the store.
/// </summary>
[TestClass]
public sealed class ImapMailboxCommandTests
{
    private const string Login = "a LOGIN u p\r\n";
    private const string LoggedIn = "a OK LOGIN completed\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_SelectEmptyMailbox_SendsNoUnseen()
    {
        var responses = await ResponsesAsync(Login + "b SELECT inbox\r\n", TestContext.CancellationToken);

        Assert.AreEqual(
            LoggedIn + "* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)\r\n"
            + "* OK [PERMANENTFLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)] Flags permitted\r\n"
            + "* 0 EXISTS\r\n* 0 RECENT\r\n"
            + $"* OK [UIDVALIDITY {UidValidity}] UIDs valid\r\n* OK [UIDNEXT 1] Predicted next UID\r\n"
            + "b OK [READ-WRITE] SELECT completed\r\n",
            responses);
    }

    [TestMethod]
    public async Task ServeAsync_SelectWithTheFirstMessageSeen_NamesTheSecondAsFirstUnseen()
    {
        var responses = await ResponsesAsync(Login + "b EXAMINE INBOX\r\n", TestContext.CancellationToken, store =>
        {
            Deliver(store, string.Empty, 2);
            store.ChangeFlags(store.ViewFor(null), "INBOX", 1, MailFlagChange.Add, MailFlags.Seen, out _);
        });

        Assert.Contains("* 2 EXISTS\r\n* 0 RECENT\r\n* OK [UNSEEN 2] First unseen\r\n", responses);
    }

    [TestMethod]
    [DataRow("b SELECT Nope\r\n", "b NO [NONEXISTENT] Mailbox does not exist\r\n")]
    [DataRow("b EXAMINE Nope\r\n", "b NO [NONEXISTENT] Mailbox does not exist\r\n")]
    [DataRow("b SELECT a//b\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b SELECT &Jjo\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b SELECT\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b SELECT (\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b SELECT INBOX x\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b STATUS Nope (MESSAGES)\r\n", "b NO [NONEXISTENT] Mailbox does not exist\r\n")]
    [DataRow("b STATUS a* (MESSAGES)\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b STATUS \"a*\" (MESSAGES)\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b STATUS INBOX (MESSAGES SIZE)\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b STATUS INBOX ()\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b STATUS INBOX (MESSAGES\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b STATUS INBOX MESSAGES\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b STATUS INBOX (MESSAGES) x\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b STATUS INBOX\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b STATUS INBOX \r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b STATUS (\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b STATUS\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b STATUS inbox (recent unseen)\r\n", "* STATUS INBOX (RECENT 0 UNSEEN 0)\r\nb OK STATUS completed\r\n")]
    [DataRow("b LIST\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b LIST (\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b LIST \"\"\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b LIST \"\" (\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b LIST \"\" * x\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b LIST &Jjo *\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b LIST \"\" &Jjo\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b LIST \"\" \"\"\r\n", "* LIST (\\Noselect) \"/\" \"\"\r\nb OK LIST completed\r\n")]
    [DataRow("b LSUB \"\" \"\"\r\n", "b OK LSUB completed\r\n")]
    [DataRow("b LIST \"\" Nope\r\n", "b OK LIST completed\r\n")]
    [DataRow("b CHECK\r\n", "b BAD No mailbox selected\r\n")]
    [DataRow("b CLOSE\r\n", "b BAD No mailbox selected\r\n")]
    [DataRow("b UNSELECT\r\n", "b BAD No mailbox selected\r\n")]
    public async Task ServeAsync_MailboxCommand_AnswersAsTheAdrSays(string command, string expected)
    {
        var responses = await ResponsesAsync(Login + command, TestContext.CancellationToken);

        Assert.AreEqual(LoggedIn + expected, responses);
    }

    [TestMethod]
    [DataRow("c CHECK x\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c CLOSE x\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c UNSELECT x\r\n", "c BAD Invalid arguments\r\n")]
    [DataRow("c CHECK\r\n", "c OK CHECK completed\r\n")]
    [DataRow("c CLOSE\r\n", "c OK CLOSE completed\r\n")]
    [DataRow("c NOOP\r\n", "c OK NOOP completed\r\n")]
    [DataRow("c UNSELECT\r\nd CHECK\r\n", "c OK UNSELECT completed\r\nd BAD No mailbox selected\r\n")]
    [DataRow("c SELECT Nope\r\nd CHECK\r\n", "c NO [NONEXISTENT] Mailbox does not exist\r\nd BAD No mailbox selected\r\n")]
    [DataRow("c SELECT a//b\r\nd CHECK\r\n", "c NO [CANNOT] Invalid mailbox name\r\nd BAD No mailbox selected\r\n")]
    public async Task ServeAsync_SelectedStateCommand_AnswersAsTheAdrSays(string commands, string expected)
    {
        var responses = await ResponsesAsync(Login + "b SELECT INBOX\r\n" + commands, TestContext.CancellationToken);

        Assert.EndsWith("b OK [READ-WRITE] SELECT completed\r\n" + expected, responses);
    }

    [TestMethod]
    public async Task ServeAsync_CloseReadWrite_ExpungesTheDeletedMessagesSilently()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);
        store.ChangeFlags(store.ViewFor(null), "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _);

        var connection = await ServeAsync(Server(store), Login + "b SELECT INBOX\r\nc CLOSE\r\n", clock, TestContext.CancellationToken);

        Assert.EndsWith("b OK [READ-WRITE] SELECT completed\r\nc OK CLOSE completed\r\n", AfterGreeting(connection));
        store.ReadMailbox(store.ViewFor(null), "INBOX", out var snapshot);
        Assert.AreEqual(2u, snapshot!.Messages.Single().Uid);
    }

    [TestMethod]
    public async Task ServeAsync_CloseReadOnlyOrUnselect_ExpungesNothing()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 1);
        store.ChangeFlags(store.ViewFor(null), "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _);

        await ServeAsync(Server(store), Login + "b EXAMINE INBOX\r\nc CLOSE\r\nd SELECT INBOX\r\ne UNSELECT\r\n", clock, TestContext.CancellationToken);

        store.ReadMailbox(store.ViewFor(null), "INBOX", out var snapshot);
        Assert.HasCount(1, snapshot!.Messages);
    }

    [TestMethod]
    public async Task ServeAsync_CloseWithAStoreThatCannotBeWritten_CompletesAndNotesTheFailure()
    {
        var clock = new ManualTimeProvider();
        var store = await MailboxStore.LoadAsync(
            new MailStoreFiles(new UnitTestUnwritableContentFileSystem(new IOException("disk full")), "state"), [], allowAnonymous: true, clock);
        Deliver(store, string.Empty, 1);
        store.ChangeFlags(store.ViewFor(null), "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _);
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Server(store), Login + "b SELECT INBOX\r\nc CLOSE\r\n", clock, TestContext.CancellationToken, log: log);

        Assert.EndsWith("c OK CLOSE completed\r\n", AfterGreeting(connection));
        Assert.AreEqual("Mail store: disk full", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_ListWithHierarchy_ListsChildrenAndPlaceholdersInOrder()
    {
        var responses = await ResponsesAsync(Login + "b LIST \"\" *\r\nc LIST \"\" %\r\nd LSUB \"\" *\r\ne LIST Archive/ %\r\n", TestContext.CancellationToken, store =>
            Create(store, string.Empty, "Archive/2026", "Box", "My Box", "a\"b", "\u00E4", "INBOX/Sub"));

        Assert.AreEqual(
            LoggedIn
            + "* LIST (\\HasChildren) \"/\" INBOX\r\n"
            + "* LIST (\\Noselect \\HasChildren) \"/\" Archive\r\n"
            + "* LIST (\\HasNoChildren) \"/\" Archive/2026\r\n"
            + "* LIST (\\HasNoChildren) \"/\" Box\r\n"
            + "* LIST (\\HasNoChildren) \"/\" INBOX/Sub\r\n"
            + "* LIST (\\HasNoChildren) \"/\" \"My Box\"\r\n"
            + "* LIST (\\HasNoChildren) \"/\" \"a\\\"b\"\r\n"
            + "* LIST (\\HasNoChildren) \"/\" &AOQ-\r\n"
            + "b OK LIST completed\r\n"
            + "* LIST (\\HasChildren) \"/\" INBOX\r\n"
            + "* LIST (\\Noselect \\HasChildren) \"/\" Archive\r\n"
            + "* LIST (\\HasNoChildren) \"/\" Box\r\n"
            + "* LIST (\\HasNoChildren) \"/\" \"My Box\"\r\n"
            + "* LIST (\\HasNoChildren) \"/\" \"a\\\"b\"\r\n"
            + "* LIST (\\HasNoChildren) \"/\" &AOQ-\r\n"
            + "c OK LIST completed\r\n"
            + "* LSUB (\\HasChildren) \"/\" INBOX\r\n"
            + "* LSUB (\\HasNoChildren) \"/\" Archive/2026\r\n"
            + "* LSUB (\\HasNoChildren) \"/\" Box\r\n"
            + "* LSUB (\\HasNoChildren) \"/\" INBOX/Sub\r\n"
            + "* LSUB (\\HasNoChildren) \"/\" \"My Box\"\r\n"
            + "* LSUB (\\HasNoChildren) \"/\" \"a\\\"b\"\r\n"
            + "* LSUB (\\HasNoChildren) \"/\" &AOQ-\r\n"
            + "d OK LSUB completed\r\n"
            + "* LIST (\\HasNoChildren) \"/\" Archive/2026\r\n"
            + "e OK LIST completed\r\n",
            responses);
    }

    [TestMethod]
    public async Task ServeAsync_SelectByModifiedUtf7AndUtf8_SelectsTheSameMailbox()
    {
        var responses = await ResponsesAsync(Login + "b STATUS &AOQ- (MESSAGES)\r\nc STATUS \u00E4 (MESSAGES)\r\n", TestContext.CancellationToken, store =>
            Create(store, string.Empty, "\u00E4"));

        Assert.AreEqual(LoggedIn + "* STATUS &AOQ- (MESSAGES 0)\r\nb OK STATUS completed\r\n* STATUS &AOQ- (MESSAGES 0)\r\nc OK STATUS completed\r\n", responses);
    }
}
