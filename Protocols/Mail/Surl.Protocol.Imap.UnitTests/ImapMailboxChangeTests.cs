using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 decision 6: <c>CREATE</c>, <c>DELETE</c>, <c>RENAME</c>, <c>SUBSCRIBE</c> and
/// <c>UNSUBSCRIBE</c> against the store, and the store's refusals in decision 3's words.
/// </summary>
[TestClass]
public sealed class ImapMailboxChangeTests
{
    private const string Login = "a LOGIN u p\r\n";
    private const string LoggedIn = "a OK LOGIN completed\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_CreateThenList_ListsTheNewMailbox()
    {
        var responses = await ResponsesAsync(Login + "b CREATE Archive/\r\nc CREATE \"My Box\"\r\nd LIST \"\" *\r\n", TestContext.CancellationToken);

        Assert.AreEqual(
            LoggedIn + "b OK CREATE completed\r\nc OK CREATE completed\r\n"
            + "* LIST (\\HasNoChildren) \"/\" INBOX\r\n* LIST (\\HasNoChildren) \"/\" Archive\r\n* LIST (\\HasNoChildren) \"/\" \"My Box\"\r\nd OK LIST completed\r\n",
            responses);
    }

    [TestMethod]
    [DataRow("b CREATE INBOX\r\n", "b NO [ALREADYEXISTS] Mailbox already exists\r\n")]
    [DataRow("b CREATE inbox/\r\n", "b NO [ALREADYEXISTS] Mailbox already exists\r\n")]
    [DataRow("b CREATE Sent\r\n", "b NO [ALREADYEXISTS] Mailbox already exists\r\n")]
    [DataRow("b CREATE a//b\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b CREATE /\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b CREATE \"a*\"\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b CREATE &Jjo\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b CREATE\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b CREATE a b\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b DELETE Sent\r\n", "b OK DELETE completed\r\n")]
    [DataRow("b DELETE INBOX\r\n", "b NO [CANNOT] INBOX cannot be deleted\r\n")]
    [DataRow("b DELETE Nope\r\n", "b NO [NONEXISTENT] Mailbox does not exist\r\n")]
    [DataRow("b DELETE a//b\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b DELETE\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b RENAME Sent Old\r\n", "b OK RENAME completed\r\n")]
    [DataRow("b RENAME Nope Old\r\n", "b NO [NONEXISTENT] Mailbox does not exist\r\n")]
    [DataRow("b RENAME Sent INBOX\r\n", "b NO [ALREADYEXISTS] Mailbox already exists\r\n")]
    [DataRow("b RENAME a//b Old\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b RENAME Sent a//b\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b RENAME Sent\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b RENAME Sent Old x\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b SUBSCRIBE Sent\r\n", "b OK SUBSCRIBE completed\r\n")]
    [DataRow("b SUBSCRIBE Nope\r\n", "b NO [NONEXISTENT] Mailbox does not exist\r\n")]
    [DataRow("b SUBSCRIBE a//b\r\n", "b NO [CANNOT] Invalid mailbox name\r\n")]
    [DataRow("b SUBSCRIBE\r\n", "b BAD Invalid arguments\r\n")]
    [DataRow("b UNSUBSCRIBE Nope\r\n", "b OK UNSUBSCRIBE completed\r\n")]
    [DataRow("b UNSUBSCRIBE &Jjo\r\n", "b OK UNSUBSCRIBE completed\r\n")]
    [DataRow("b UNSUBSCRIBE\r\n", "b BAD Invalid arguments\r\n")]
    public async Task ServeAsync_MailboxChange_AnswersAsDecisionsThreeAndSixSay(string command, string expected)
    {
        var responses = await ResponsesAsync(Login + command, TestContext.CancellationToken, store => Create(store, string.Empty, "Sent"));

        Assert.AreEqual(LoggedIn + expected, responses);
    }

    [TestMethod]
    public async Task ServeAsync_CreateANameTheStoreRefuses_AnswersInvalidMailboxName()
    {
        var responses = await ResponsesAsync(Login + $"b CREATE {new string('x', MailboxStore.MaxMailboxNameBytes + 1)}\r\n", TestContext.CancellationToken);

        Assert.AreEqual(LoggedIn + "b NO [CANNOT] Invalid mailbox name\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_CreatePastTheMailboxBound_AnswersLimit()
    {
        var clock = new ManualTimeProvider();
        var store = new MailboxStore([], allowAnonymous: true, clock, maxMailboxes: 0);

        var connection = await ServeAsync(Server(store), Login + "b CREATE Archive\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual(LoggedIn + "b NO [LIMIT] Too many mailboxes\r\n", AfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_RenameInbox_MovesItsMessagesAndLeavesItEmpty()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);

        var connection = await ServeAsync(Server(store), Login + "b RENAME INBOX Old\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual(LoggedIn + "b OK RENAME completed\r\n", AfterGreeting(connection));
        store.ReadMailbox(store.ViewFor(null), "INBOX", out var inbox);
        store.ReadMailbox(store.ViewFor(null), "Old", out var old);
        Assert.AreEqual(0, inbox!.Messages.Count);
        Assert.AreEqual(2, old!.Messages.Count);
    }

    [TestMethod]
    public async Task ServeAsync_DeleteTheSelectedMailbox_ReportsEveryMessageExpunged()
    {
        var responses = await ResponsesAsync(Login + "b SELECT Sent\r\nc DELETE Sent\r\n", TestContext.CancellationToken, store =>
        {
            Create(store, string.Empty, "Sent");
            store.Append(store.ViewFor(null), "Sent", "m"u8, MailFlags.None, null, out _);
        });

        Assert.EndsWith("b OK [READ-WRITE] SELECT completed\r\n* 1 EXPUNGE\r\nc OK DELETE completed\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_MailboxChangeBeforeALogin_IsRefused()
    {
        var clock = new ManualTimeProvider();
        var store = AccountStore(clock, "u");

        var connection = await ServeAsync(Server(store, new ScriptedLoginPolicy()), "b CREATE Archive\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("b NO [AUTHENTICATIONFAILED] Authentication required\r\n", AfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_ChangeWhoseIndexCannotBeSaved_SucceedsAndNotesIt()
    {
        var clock = new ManualTimeProvider();
        var store = await MailboxStore.LoadAsync(
            new MailStoreFiles(new UnitTestUnwritableContentFileSystem(new IOException("disk full")), "state"), [], allowAnonymous: true, clock, cancellationToken: TestContext.CancellationToken);
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Server(store), Login + "b CREATE Archive\r\n", clock, TestContext.CancellationToken, log: log);

        Assert.AreEqual(LoggedIn + "b OK CREATE completed\r\n", AfterGreeting(connection));
        Assert.AreEqual("Mail store: disk full", log.Notes.Single());
    }
}
