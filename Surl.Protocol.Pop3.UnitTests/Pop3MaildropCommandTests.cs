using Surl.Content;
using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Pop3.Pop3TestExchange;

namespace Surl.Protocol.Pop3;

/// <summary>
/// ADR-0056 decisions 4 and 5: <c>STAT</c>, <c>LIST</c>, <c>UIDL</c>, <c>RETR</c>, <c>TOP</c>,
/// <c>DELE</c>, <c>RSET</c>, <c>NOOP</c> and <c>QUIT</c> against a logged-in session's maildrop.
/// </summary>
[TestClass]
public sealed class Pop3MaildropCommandTests
{
    private const string Second = "Subject: two\r\n\r\nline 1\r\nline 2\r\n";

    public TestContext TestContext { get; set; } = null!;

    private Task<InMemoryConnection> ServeLoggedInAsync(MailboxStore store, string commands, TimeProvider clock, IExchangeLog? log = null) =>
        ServeAsync(store, Login + commands, clock, TestContext.CancellationToken, log: log);

    [TestMethod]
    public async Task ServeAsync_StatListAndUidl_DescribeTheMaildrop()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeLoggedInAsync(AccountStore(clock, Message, Second), "STAT\r\nLIST\r\nLIST 2\r\nUIDL\r\nuidl 2\r\n", clock);

        Assert.AreEqual(
            "+OK 2 165\r\n"
            + "+OK 2 messages (165 octets)\r\n1 133\r\n2 32\r\n.\r\n"
            + "+OK 2 32\r\n"
            + $"+OK Unique-ID listing follows\r\n1 {UidValidity}.1\r\n2 {UidValidity}.2\r\n.\r\n"
            + $"+OK 2 {UidValidity}.2\r\n",
            RepliesAfterLogin(connection));
    }

    [TestMethod]
    public async Task ServeAsync_Retr_SendsTheMessageDotStuffed()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeLoggedInAsync(AccountStore(clock, Message), "RETR 1\r\n", clock);

        Assert.AreEqual("+OK 133 octets\r\n" + StuffedMessage, RepliesAfterLogin(connection));
    }

    [TestMethod]
    public async Task ServeAsync_RetrOfAMessageWithoutFinalCrlf_EndsItsLastLine()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeLoggedInAsync(AccountStore(clock, "Subject: d\r\n\r\n.\r\n..\r\n. x\r\nend"), "RETR 1\r\n", clock);

        Assert.AreEqual("+OK 29 octets\r\nSubject: d\r\n\r\n..\r\n...\r\n.. x\r\nend\r\n.\r\n", RepliesAfterLogin(connection));
    }

    [TestMethod]
    public async Task ServeAsync_RetrOfADeletedOrMissingMessage_AnswersNoSuchMessage()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeLoggedInAsync(AccountStore(clock, Message), "DELE 1\r\nRETR 1\r\nRETR 2\r\nLIST 1\r\nUIDL 1\r\nDELE 1\r\nTOP 1 0\r\n", clock);

        Assert.AreEqual(
            "+OK Message deleted\r\n" + string.Concat(Enumerable.Repeat("-ERR No such message\r\n", 6)),
            RepliesAfterLogin(connection));
    }

    [TestMethod]
    [DataRow("RETR\r\n")]
    [DataRow("RETR 1 2\r\n")]
    [DataRow("RETR 0\r\n")]
    [DataRow("RETR x\r\n")]
    [DataRow("RETR 12345678901\r\n")]
    [DataRow("RETR  1\r\n")]
    [DataRow("DELE\r\n")]
    [DataRow("TOP\r\n")]
    [DataRow("TOP 1 x\r\n")]
    [DataRow("TOP 1 -1\r\n")]
    [DataRow("TOP 1 2147483648\r\n")]
    [DataRow("TOP 1 0 0\r\n")]
    [DataRow("LIST 1 2\r\n")]
    [DataRow("UIDL x\r\n")]
    [DataRow("STAT 1\r\n")]
    [DataRow("RSET 1\r\n")]
    [DataRow("NOOP 1\r\n")]
    [DataRow("QUIT 1\r\n")]
    public async Task ServeAsync_BadArguments_AnswersInvalidArguments(string command)
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeLoggedInAsync(AccountStore(clock, Message), command, clock);

        Assert.AreEqual("-ERR Invalid arguments\r\n", RepliesAfterLogin(connection));
    }

    [TestMethod]
    public async Task ServeAsync_Top_SendsTheHeadersAndTheBodyLinesAsked()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeLoggedInAsync(AccountStore(clock, Second), "TOP 1\r\nTOP 1 1\r\nTOP 1 9\r\n", clock);

        Assert.AreEqual(
            "+OK Top of message follows\r\nSubject: two\r\n\r\n.\r\n"
            + "+OK Top of message follows\r\nSubject: two\r\n\r\nline 1\r\n.\r\n"
            + "+OK Top of message follows\r\nSubject: two\r\n\r\nline 1\r\nline 2\r\n.\r\n",
            RepliesAfterLogin(connection));
    }

    [TestMethod]
    public async Task ServeAsync_DeleThenRset_RestoresTheMessage()
    {
        var clock = new ManualTimeProvider();
        var store = AccountStore(clock, Message, Second);

        var connection = await ServeLoggedInAsync(store, "DELE 1\r\nSTAT\r\nLIST\r\nRSET\r\nSTAT\r\nNOOP\r\nQUIT\r\n", clock);

        Assert.AreEqual(
            "+OK Message deleted\r\n+OK 1 32\r\n+OK 1 messages (32 octets)\r\n2 32\r\n.\r\n"
            + "+OK Maildrop has 2 messages (165 octets)\r\n+OK 2 165\r\n+OK\r\n+OK surl signing off\r\n",
            RepliesAfterLogin(connection));
        Assert.HasCount(2, Inbox(store, "u"));
    }

    [TestMethod]
    public async Task ServeAsync_DeleThenQuit_RemovesTheMessageFromTheStoreAndNotesIt()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = AccountStore(clock, Message, Second);

        var connection = await ServeLoggedInAsync(store, "DELE 1\r\nQUIT\r\nSTAT\r\n", clock, log);

        Assert.AreEqual("+OK Message deleted\r\n+OK surl signing off\r\n", RepliesAfterLogin(connection));
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.AreEqual(new[] { Second }, Inbox(store, "u").ToList());
        Assert.AreEqual("1 messages removed from the maildrop", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_QuitWithNothingDeleted_RemovesNothing()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = AccountStore(clock, Message);
        var changesBefore = store.ChangeCount;

        await ServeLoggedInAsync(store, "QUIT\r\n", clock, log);

        Assert.HasCount(1, Inbox(store, "u"));
        Assert.AreEqual(changesBefore, store.ChangeCount);
        Assert.AreEqual("Maildrop opened: 1 messages, 133 octets", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_QuitBeforeLogin_SignsOff()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AccountStore(clock, Message), "QUIT\r\nNOOP\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("+OK surl signing off\r\n", RepliesAfterGreeting(connection));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_DeleThenDroppedConnection_RemovesNothing()
    {
        var clock = new ManualTimeProvider();
        var store = AccountStore(clock, Message);

        await ServeLoggedInAsync(store, "DELE 1\r\n", clock);

        Assert.HasCount(1, Inbox(store, "u"));
    }

    [TestMethod]
    public async Task ServeAsync_StoreWriteFailsAtQuit_SignsOffAndNotesIt()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = await PersistedStoreAsync(clock, new IOException("disk full"));

        var connection = await ServeLoggedInAsync(store, "DELE 1\r\nQUIT\r\n", clock, log);

        Assert.AreEqual("+OK Message deleted\r\n+OK surl signing off\r\n", RepliesAfterLogin(connection));
        Assert.AreEqual("Mail store: disk full", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_StoreWriteCancelledAtQuit_EndsTheExchange()
    {
        var clock = new ManualTimeProvider();
        var store = await PersistedStoreAsync(clock, new OperationCanceledException());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => ServeLoggedInAsync(store, "DELE 1\r\nQUIT\r\n", clock));
    }

    private static async Task<MailboxStore> PersistedStoreAsync(TimeProvider clock, Exception writeFailure)
    {
        var store = await MailboxStore.LoadAsync(new MailStoreFiles(new UnitTestUnwritableContentFileSystem(writeFailure), "state"), ["u"], allowAnonymous: false, clock);
        return Seed(store, "u@x", [Message]);
    }
}
