using Surl.Content;
using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smtp.SmtpTestExchange;

namespace Surl.Protocol.Smtp;

/// <summary>
/// ADR-0053 decisions 4 and 6: who a message reaches, what is stored, and the store's outcomes.
/// </summary>
[TestClass]
public sealed class SmtpDeliveryTests
{
    private const string Body = "Subject: hi\r\n\r\nhello\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_Accounts_DeliverOneCopyPerAccountAndDiscardTheUnknown()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = AccountStore(clock, "tester", "other");
        var request = "EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<tester@x>\r\nRCPT TO:<Tester@y>\r\nRCPT TO:<nobody@x>\r\nRCPT TO:<\"no\\\\body\"@x>\r\n"
            + $"DATA\r\n{Body}.\r\n";

        var connection = await ServeAsync(store, request, clock, TestContext.CancellationToken, log: log);

        StringAssert.EndsWith(RepliesAfterHello(connection), "354 End data with <CR><LF>.<CR><LF>\r\n250 2.0.0 Message accepted\r\n");
        var stored = TraceFields("a@x") + Body;
        CollectionAssert.AreEqual(new[] { stored }, Inbox(store, "tester").ToList());
        Assert.IsEmpty(Inbox(store, "other"));
        CollectionAssert.AreEqual(
            new[]
            {
                "Mail for nobody@x discarded: no such account",
                "Mail for \"no\\x5C\\x5Cbody\"@x discarded: no such account",
                $"Message stored: {stored.Length} bytes for 1 recipients",
            },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_DomainlessRecipient_ReachesTheAccountItNames()
    {
        var clock = new ManualTimeProvider();
        var store = AccountStore(clock, "bob");

        await ServeAsync(store, $"EHLO c\r\nMAIL FROM:<>\r\nRCPT TO:<bob>\r\nDATA\r\n{Body}.\r\n", clock, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { TraceFields(string.Empty) + Body }, Inbox(store, "bob").ToList());
    }

    [TestMethod]
    public async Task ServeAsync_Helo_WritesSmtpAsTheReceivedProtocol()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);

        await ServeAsync(store, $"HELO client.example\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\n{Body}.\r\n", clock, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { TraceFields("a@x", "client.example", "SMTP") + Body }, Inbox(store, string.Empty).ToList());
    }

    [TestMethod]
    public async Task ServeAsync_OverTls_WritesEsmtpsAsTheReceivedProtocol()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var connection = new InMemoryConnection(
            Ascii($"EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\n{Body}.\r\n"),
            initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        CollectionAssert.AreEqual(new[] { TraceFields("a@x", protocol: "ESMTPS") + Body }, Inbox(store, string.Empty).ToList());
    }

    [TestMethod]
    public async Task ServeAsync_SessionGoesOnAfterAMessage_AndTheNextMessageNeedsMailAgain()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var message = $"MAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\n{Body}.\r\n";

        var connection = await ServeAsync(store, $"EHLO c\r\n{message}RCPT TO:<b@y>\r\n{message}QUIT\r\n", clock, TestContext.CancellationToken);

        StringAssert.Contains(RepliesAfterHello(connection), "250 2.0.0 Message accepted\r\n503 5.5.1 Send MAIL first\r\n250 2.1.0 Sender OK\r\n");
        Assert.HasCount(2, Inbox(store, string.Empty));
    }

    [TestMethod]
    public async Task ServeAsync_BareLineFeedsInTheBody_AreStoredAsSent()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        const string BareLineFeedBody = "Subject: hi\n\nhello\n.dot\n\r\n";

        await ServeAsync(store, $"EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\n{BareLineFeedBody}.\r\n", clock, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { TraceFields("a@x") + BareLineFeedBody }, Inbox(store, string.Empty).ToList());
    }

    [TestMethod]
    public async Task ServeAsync_PeerClosesMidBody_StoresNothing()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = AnonymousStore(clock);

        var connection = await ServeAsync(store, "EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\nhalf a mess", clock, TestContext.CancellationToken, log: log);

        StringAssert.EndsWith(RepliesAfterHello(connection), "354 End data with <CR><LF>.<CR><LF>\r\n");
        Assert.IsEmpty(Inbox(store, string.Empty));
        Assert.AreEqual("The client closed the connection part way through a message; nothing was stored.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_StoreFull_Answers452AndTheSessionGoesOn()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = AnonymousStore(clock, maxMessages: 1);
        var message = $"MAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\n{Body}.\r\n";

        var connection = await ServeAsync(store, $"EHLO c\r\n{message}{message}NOOP\r\n", clock, TestContext.CancellationToken, log: log);

        StringAssert.EndsWith(RepliesAfterHello(connection), "452 4.3.1 Insufficient system storage\r\n250 2.0.0 OK\r\n");
        Assert.HasCount(1, Inbox(store, string.Empty));
        Assert.AreEqual("Message refused: the mail store is full", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_StoreRefusesTheMessageAsTooLarge_Answers552AndTheSessionGoesOn()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = AnonymousStore(clock, maxMessageBytes: 20);

        var connection = await ServeAsync(store, $"EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\n{Body}.\r\nNOOP\r\n", clock, TestContext.CancellationToken, log: log);

        StringAssert.EndsWith(RepliesAfterHello(connection), "552 5.3.4 Message exceeds the size limit\r\n250 2.0.0 OK\r\n");
        Assert.IsEmpty(Inbox(store, string.Empty));
        Assert.AreEqual($"Message refused: past --max-filesize after {TraceFields("a@x").Length + Body.Length} bytes", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_MessageFileCannotBeWritten_Answers451AndTheSessionGoesOn()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = await PersistedStoreAsync(clock, new UnitTestUnwritableContentFileSystem(new IOException("The disk failed.")));

        var connection = await ServeAsync(store, $"EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\n{Body}.\r\nMAIL FROM:<a@x>\r\n", clock, TestContext.CancellationToken, log: log);

        StringAssert.EndsWith(RepliesAfterHello(connection), "354 End data with <CR><LF>.<CR><LF>\r\n451 4.3.0 Local error in processing\r\n250 2.1.0 Sender OK\r\n");
        Assert.IsEmpty(Inbox(store, string.Empty));
        Assert.AreEqual("Mail store: The disk failed.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_IndexCannotBeSavedAfterTheMessageFileIsWritten_Answers250AndNotesTheFailure()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = await PersistedStoreAsync(clock, new UnitTestUnwritableContentFileSystem(new IOException("The disk failed."), keepsMessageFiles: true));

        var connection = await ServeAsync(store, $"EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\n{Body}.\r\n", clock, TestContext.CancellationToken, log: log);

        StringAssert.EndsWith(RepliesAfterHello(connection), "250 2.0.0 Message accepted\r\n");
        CollectionAssert.AreEqual(new[] { TraceFields("a@x") + Body }, Inbox(store, string.Empty).ToList());
        Assert.AreEqual("Mail store: The disk failed.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_StoreWriteCancelled_EndsTheExchange()
    {
        var clock = new ManualTimeProvider();
        var store = await PersistedStoreAsync(clock, new UnitTestUnwritableContentFileSystem(new OperationCanceledException(), keepsMessageFiles: true));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => ServeAsync(store, $"EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\n{Body}.\r\n", clock, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ServeAsync_MessageStoredOnDisk_IsTheTraceFieldsThenTheBodyInOneMessageFile()
    {
        var clock = new ManualTimeProvider();
        var fileSystem = new InMemoryContentFileSystem(clock);
        var store = await PersistedStoreAsync(clock, fileSystem);

        await ServeAsync(store, $"EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\n{Body}.\r\n", clock, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { TraceFields("a@x") + Body }, Inbox(store, string.Empty).ToList());
        var messageFile = MessageFileNames(fileSystem).Single();
        Assert.IsFalse(messageFile.StartsWith(PendingFilePrefix, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ServeAsync_BodyPastMaxFilesize_LeavesNoPendingFile()
    {
        var clock = new ManualTimeProvider();
        var fileSystem = new InMemoryContentFileSystem(clock);
        var store = await PersistedStoreAsync(clock, fileSystem);
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 200 };

        var connection = await ServeAsync(store, $"EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\n{new string('x', 150)}\r\n.\r\n", clock, TestContext.CancellationToken, limits);

        StringAssert.EndsWith(RepliesAfterHello(connection), "552 5.3.4 Message exceeds the size limit\r\n");
        Assert.IsEmpty(Inbox(store, string.Empty));
        Assert.IsEmpty(MessageFileNames(fileSystem));
    }

    [TestMethod]
    public async Task ServeAsync_PeerClosesMidBody_LeavesNoPendingFile()
    {
        var clock = new ManualTimeProvider();
        var fileSystem = new InMemoryContentFileSystem(clock);
        var store = await PersistedStoreAsync(clock, fileSystem);

        await ServeAsync(store, "EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\nhalf a mess", clock, TestContext.CancellationToken);

        Assert.IsEmpty(Inbox(store, string.Empty));
        Assert.IsEmpty(MessageFileNames(fileSystem));
    }

    [TestMethod]
    public async Task ServeAsync_NoRecipientNamesAnAccount_StoresNothingAndAnswers250()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = AccountStore(clock, "tester");

        var connection = await ServeAsync(store, $"EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<nobody@x>\r\nDATA\r\n{Body}.\r\n", clock, TestContext.CancellationToken, log: log);

        StringAssert.EndsWith(RepliesAfterHello(connection), "250 2.0.0 Message accepted\r\n");
        Assert.IsEmpty(Inbox(store, "tester"));
        Assert.AreEqual($"Message stored: {TraceFields("a@x").Length + Body.Length} bytes for 0 recipients", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_NoMaxFilesize_StoresTheMessage()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 0 };

        await ServeAsync(store, $"EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\n{Body}.\r\n", clock, TestContext.CancellationToken, limits);

        CollectionAssert.AreEqual(new[] { TraceFields("a@x") + Body }, Inbox(store, string.Empty).ToList());
    }

    private const string PendingFilePrefix = ".pending-";

    private static readonly string StateFolder = Path.Join(InMemoryContentFileSystem.RootPath, "state");

    private static readonly string MessagesFolder = Path.Join(StateFolder, MailStoreFiles.MessagesFolderName);

    private static Task<MailboxStore> PersistedStoreAsync(TimeProvider clock, IContentFileSystem fileSystem) =>
        MailboxStore.LoadAsync(new MailStoreFiles(fileSystem, StateFolder), [], allowAnonymous: true, clock);

    // Every file in the store's messages folder, pending ones included.
    private static List<string> MessageFileNames(InMemoryContentFileSystem fileSystem) =>
        fileSystem.GetEntryKind(MessagesFolder) == ContentEntryKind.Directory ? fileSystem.EnumerateDirectoryEntryNames(MessagesFolder).ToList() : [];
}
