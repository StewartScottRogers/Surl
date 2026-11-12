using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smtp.SmtpTestExchange;

namespace Surl.Protocol.Smtp;

/// <summary>
/// ADR-0102 decision 1: with a mail domain configured, <c>RCPT TO</c> reaches mailboxes, aliases
/// and catch-alls through the store, and a recipient outside every domain is refused as relaying
/// with the reply measured from pinned upstream curl 8.21.0 (rows R1 to R4). The request bytes are
/// what upstream curl sends (ADR-0053's rows).
/// </summary>
[TestClass]
public sealed class SmtpMailDomainTests
{
    private const string Body = "Subject: hi\r\n\r\nhello\r\n";
    private const string Ann = "ann@example.com";
    private const string Bob = "bob@example.com";
    private const string Cat = "cat@example.com";

    public TestContext TestContext { get; set; } = null!;

    // example.com with three mailboxes, team@ an alias to ann and bob, and cat@ the catch-all.
    private static MailboxStore DomainStore(TimeProvider clock) => new(
        ["dave"],
        allowAnonymous: false,
        clock,
        mailDomains: new MailDomainConfiguration(
            ["example.com"],
            [Ann, Bob, Cat],
            [new MailAlias("team@example.com", [Ann, Bob]), new MailAlias("pair@example.com", [Bob, Cat])],
            [new MailCatchAll("example.com", Cat)]));

    private static string Session(params string[] recipients) =>
        "EHLO c\r\nMAIL FROM:<a@x> SIZE=33\r\n" + string.Concat(recipients.Select(recipient => $"RCPT TO:<{recipient}>\r\n")) + $"DATA\r\n{Body}.\r\nQUIT\r\n";

    private static string Stored => TraceFields("a@x") + Body;

    [TestMethod]
    public async Task ServeAsync_RecipientInTheDomain_IsAcceptedAndDelivered()
    {
        var clock = new ManualTimeProvider();
        var store = DomainStore(clock);

        var connection = await ServeAsync(store, Session(Ann), clock, TestContext.CancellationToken);

        Assert.AreEqual(
            "250 2.1.0 Sender OK\r\n250 2.1.5 Recipient OK\r\n354 End data with <CR><LF>.<CR><LF>\r\n250 2.0.0 Message accepted\r\n221 2.0.0 Bye\r\n",
            RepliesAfterHello(connection));
        CollectionAssert.AreEqual(new[] { Stored }, Inbox(store, Ann).ToList());
        Assert.IsEmpty(Inbox(store, Bob));
    }

    [TestMethod]
    public async Task ServeAsync_AccountNamedByTheLocalPart_IsDeliveredAtTheDomain()
    {
        var clock = new ManualTimeProvider();
        var store = DomainStore(clock);

        await ServeAsync(store, Session("dave@example.com"), clock, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { Stored }, Inbox(store, "dave").ToList());
    }

    [TestMethod]
    public async Task ServeAsync_AliasAndCatchAll_AreDeliveredAsTheStoreResolvesThem()
    {
        var clock = new ManualTimeProvider();
        var store = DomainStore(clock);

        var connection = await ServeAsync(store, Session("team@example.com", "nobody@example.com"), clock, TestContext.CancellationToken);

        StringAssert.EndsWith(RepliesAfterHello(connection), "250 2.0.0 Message accepted\r\n221 2.0.0 Bye\r\n");
        CollectionAssert.AreEqual(new[] { Stored }, Inbox(store, Ann).ToList());
        CollectionAssert.AreEqual(new[] { Stored }, Inbox(store, Bob).ToList());
        CollectionAssert.AreEqual(new[] { Stored }, Inbox(store, Cat).ToList());
    }

    [TestMethod]
    public async Task ServeAsync_OwnerNamedByAnAddressAndAnAlias_GetsOneCopyWhicheverComesFirst()
    {
        foreach (var recipients in new[] { new[] { Bob, "team@example.com" }, new[] { "team@example.com", Bob }, new[] { "team@example.com", Ann, Bob } })
        {
            var clock = new ManualTimeProvider();
            var log = new RecordingExchangeLog();
            var store = DomainStore(clock);

            await ServeAsync(store, Session(recipients), clock, TestContext.CancellationToken, log: log);

            CollectionAssert.AreEqual(new[] { Stored }, Inbox(store, Ann).ToList(), string.Join(' ', recipients));
            CollectionAssert.AreEqual(new[] { Stored }, Inbox(store, Bob).ToList(), string.Join(' ', recipients));
            CollectionAssert.Contains(log.Notes.ToList(), $"Message stored: {Stored.Length} bytes for 1 recipients");
        }
    }

    [TestMethod]
    public async Task ServeAsync_PartlyOverlappingAliases_GiveEveryOwnerExactlyOneCopy()
    {
        var clock = new ManualTimeProvider();
        var store = DomainStore(clock);

        await ServeAsync(store, Session("team@example.com", "pair@example.com"), clock, TestContext.CancellationToken);

        Assert.HasCount(1, Inbox(store, Ann));
        Assert.HasCount(1, Inbox(store, Cat));
        Assert.HasCount(1, Inbox(store, Bob));
    }

    [TestMethod]
    public async Task ServeAsync_RecipientOutsideEveryDomain_IsRefusedAsRelayingAndNotDelivered()
    {
        foreach (var recipient in new[] { "bob@elsewhere.example", "ann@[127.0.0.1]", "ann", "Postmaster", "ann@mail.example.com" })
        {
            var clock = new ManualTimeProvider();
            var log = new RecordingExchangeLog();
            var store = DomainStore(clock);

            // Row R1: curl sends MAIL and RCPT, reads the 550, and quits (exit 55).
            var connection = await ServeAsync(store, $"EHLO c\r\nMAIL FROM:<a@x> SIZE=33\r\nRCPT TO:<{recipient}>\r\nQUIT\r\n", clock, TestContext.CancellationToken, log: log);

            Assert.AreEqual("250 2.1.0 Sender OK\r\n550 5.7.1 Relaying denied\r\n221 2.0.0 Bye\r\n", RepliesAfterHello(connection), recipient);
            CollectionAssert.Contains(log.Notes.ToList(), $"RCPT {recipient} refused: outside every --mail-domain");
            Assert.IsEmpty(Inbox(store, Ann));
        }
    }

    [TestMethod]
    public async Task ServeAsync_RelayingRefusedRecipient_IsNotCountedSoDataNeedsAnAcceptedOne()
    {
        var clock = new ManualTimeProvider();
        var store = DomainStore(clock);

        var connection = await ServeAsync(store, "EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<bob@elsewhere.example>\r\nDATA\r\nQUIT\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("250 2.1.0 Sender OK\r\n550 5.7.1 Relaying denied\r\n503 5.5.1 Send RCPT first\r\n221 2.0.0 Bye\r\n", RepliesAfterHello(connection));
    }

    [TestMethod]
    public async Task ServeAsync_OneAcceptedAndOneRefusedRecipient_DeliversToTheAcceptedOneOnly()
    {
        var clock = new ManualTimeProvider();
        var store = DomainStore(clock);

        // Row R3: with --mail-rcpt-allowfails curl sends DATA after the refused RCPT (exit 0).
        var connection = await ServeAsync(store, Session(Ann, "bob@elsewhere.example"), clock, TestContext.CancellationToken);

        Assert.AreEqual(
            "250 2.1.0 Sender OK\r\n250 2.1.5 Recipient OK\r\n550 5.7.1 Relaying denied\r\n354 End data with <CR><LF>.<CR><LF>\r\n250 2.0.0 Message accepted\r\n221 2.0.0 Bye\r\n",
            RepliesAfterHello(connection));
        CollectionAssert.AreEqual(new[] { Stored }, Inbox(store, Ann).ToList());
        Assert.IsEmpty(Inbox(store, Bob));
    }

    [TestMethod]
    public async Task ServeAsync_RecipientWithAParameterOutsideEveryDomain_IsAnsweredForTheParameterFirst()
    {
        var clock = new ManualTimeProvider();
        var store = DomainStore(clock);

        var connection = await ServeAsync(store, "EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<bob@elsewhere.example> NOTIFY=NEVER\r\nQUIT\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("250 2.1.0 Sender OK\r\n555 5.5.4 Unsupported parameter\r\n221 2.0.0 Bye\r\n", RepliesAfterHello(connection));
    }

    // Ann's quota is 10 bytes, so any message passes it; Bob has none.
    private static MailboxStore QuotaStore(TimeProvider clock) => new(
        [],
        allowAnonymous: false,
        clock,
        mailDomains: new MailDomainConfiguration(
            ["example.com"],
            [Ann, Bob],
            [],
            [],
            [new MailboxSettings(Ann, 10, [])]));

    [TestMethod]
    public async Task ServeAsync_DeliveryPastARecipientsQuota_Answers452MailboxFullAndDeliversToNoRecipient()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = QuotaStore(clock);

        var connection = await ServeAsync(store, Session(Ann, Bob), clock, TestContext.CancellationToken, log: log);

        Assert.AreEqual(
            "250 2.1.0 Sender OK\r\n250 2.1.5 Recipient OK\r\n250 2.1.5 Recipient OK\r\n354 End data with <CR><LF>.<CR><LF>\r\n452 4.2.2 Mailbox full\r\n221 2.0.0 Bye\r\n",
            RepliesAfterHello(connection));
        Assert.IsEmpty(Inbox(store, Ann));
        Assert.IsEmpty(Inbox(store, Bob));
        Assert.AreEqual("Message refused: a recipient's mailbox would pass its quota", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_RcptToAnOverQuotaMailbox_IsAcceptedAsAnyKnownRecipient()
    {
        var clock = new ManualTimeProvider();
        var store = QuotaStore(clock);

        var connection = await ServeAsync(store, "EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<" + Ann + ">\r\nQUIT\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("250 2.1.0 Sender OK\r\n250 2.1.5 Recipient OK\r\n221 2.0.0 Bye\r\n", RepliesAfterHello(connection));
    }

    [TestMethod]
    public async Task ServeAsync_AllowAnonymousWithAMailDomain_RefusesNothingAsRelaying()
    {
        var clock = new ManualTimeProvider();
        var store = new MailboxStore([], allowAnonymous: true, clock, mailDomains: new MailDomainConfiguration(["example.com"], [], [], []));

        var connection = await ServeAsync(store, Session("bob@elsewhere.example"), clock, TestContext.CancellationToken);

        StringAssert.Contains(RepliesAfterHello(connection), "250 2.1.5 Recipient OK\r\n");
        CollectionAssert.AreEqual(new[] { Stored }, Inbox(store, string.Empty).ToList());
    }
}
