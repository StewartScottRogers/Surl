using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smtp.SmtpTestExchange;

namespace Surl.Protocol.Smtp;

/// <summary>
/// ADR-0053 Amendment 1, decisions 2 to 5: with the store's <c>DeliveredToEnabled</c> each owner's copy begins with
/// one <c>Delivered-To</c> line per envelope address that reached it. The field is Surl's own (RFC 9228), so no
/// upstream curl run backs it.
/// </summary>
[TestClass]
public sealed class SmtpDeliveredToTests
{
    private const string Body = "Subject: hi\r\n\r\nhello\r\n";
    private const string Ann = "ann@example.com";
    private const string Bob = "bob@example.com";
    private const string Cat = "cat@example.com";

    public TestContext TestContext { get; set; } = null!;

    private static MailboxStore DomainStore(TimeProvider clock, bool enabled = true, long? annQuota = null) => new(
        [],
        allowAnonymous: false,
        clock,
        mailDomains: new MailDomainConfiguration(
            ["example.com"],
            [Ann, Bob, Cat],
            [new MailAlias("ann2@example.com", [Ann]), new MailAlias("team@example.com", [Ann, Bob]), new MailAlias("pair@example.com", [Bob, Cat])],
            [],
            annQuota is { } quota ? [new MailboxSettings(Ann, quota, [])] : null))
    {
        DeliveredToEnabled = enabled,
    };

    private static string Session(params string[] recipients) =>
        "EHLO c\r\nMAIL FROM:<a@x>\r\n" + string.Concat(recipients.Select(recipient => $"RCPT TO:<{recipient}>\r\n")) + $"DATA\r\n{Body}.\r\nQUIT\r\n";

    private static string DeliveredTo(params string[] addresses) => string.Concat(addresses.Select(address => $"Delivered-To: {address}\r\n"));

    private static string Stored(params string[] addresses) => DeliveredTo(addresses) + TraceFields("a@x") + Body;

    [TestMethod]
    public async Task ServeAsync_RecipientAndAliasOfOneOwner_GiveOneCopyWithBothLinesInRcptOrder()
    {
        var clock = new ManualTimeProvider();
        var store = DomainStore(clock);

        await ServeAsync(store, Session(Ann, "ann2@example.com"), clock, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { Stored(Ann, "ann2@example.com") }, Inbox(store, Ann).ToList());
    }

    [TestMethod]
    public async Task ServeAsync_TwoOwners_EachCopyNamesOnlyItsOwnAddresses()
    {
        var clock = new ManualTimeProvider();
        var store = DomainStore(clock);

        await ServeAsync(store, Session(Ann, Bob), clock, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { Stored(Ann) }, Inbox(store, Ann).ToList());
        CollectionAssert.AreEqual(new[] { Stored(Bob) }, Inbox(store, Bob).ToList());
    }

    [TestMethod]
    public async Task ServeAsync_PartlyOverlappingAliases_EachCopyNamesTheAddressesThatReachedItsOwners()
    {
        var clock = new ManualTimeProvider();
        var store = DomainStore(clock);

        await ServeAsync(store, Session("team@example.com", "pair@example.com"), clock, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { Stored("team@example.com", "pair@example.com") }, Inbox(store, Ann).ToList());
        CollectionAssert.AreEqual(new[] { Stored("team@example.com", "pair@example.com") }, Inbox(store, Bob).ToList());
        CollectionAssert.AreEqual(new[] { Stored("pair@example.com") }, Inbox(store, Cat).ToList());
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousOwner_GivesEachRecipientItsOwnCopyNamingItsOwnPath()
    {
        var clock = new ManualTimeProvider();
        var store = new MailboxStore([], allowAnonymous: true, clock) { DeliveredToEnabled = true };

        await ServeAsync(store, Session("p@y", "q@y"), clock, TestContext.CancellationToken);

        CollectionAssert.AreEquivalent(new[] { Stored("p@y"), Stored("q@y") }, Inbox(store, string.Empty).ToList());
    }

    [TestMethod]
    public async Task ServeAsync_DiscardedRecipient_WritesNoField()
    {
        var clock = new ManualTimeProvider();
        var store = DomainStore(clock);

        await ServeAsync(store, Session("nobody@example.com", Ann), clock, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { Stored(Ann) }, Inbox(store, Ann).ToList());
    }

    [TestMethod]
    public async Task ServeAsync_OnlyDiscardedRecipients_StoresNothingAndAnswers250()
    {
        var clock = new ManualTimeProvider();
        var store = DomainStore(clock);

        var connection = await ServeAsync(store, Session("nobody@example.com"), clock, TestContext.CancellationToken);

        StringAssert.Contains(RepliesAfterHello(connection), "250 2.0.0 Message accepted\r\n");
        Assert.IsEmpty(Inbox(store, Ann));
    }

    [TestMethod]
    public async Task ServeAsync_OneOwnerOverQuota_StoresNothingForAnyOwner()
    {
        var clock = new ManualTimeProvider();
        var store = DomainStore(clock, annQuota: 10);

        var connection = await ServeAsync(store, Session(Ann, Bob), clock, TestContext.CancellationToken);

        StringAssert.Contains(RepliesAfterHello(connection), "452 4.2.2 Mailbox full\r\n");
        Assert.IsEmpty(Inbox(store, Ann));
        Assert.IsEmpty(Inbox(store, Bob));
    }

    [TestMethod]
    public async Task ServeAsync_BodyThatFitsMaxFilesizeOnlyWithoutTheLines_IsAnsweredTooLarge()
    {
        var limits = ExchangeLimits.Default with { MaxUploadBytes = TraceFields("a@x").Length + Body.Length };

        var withoutLines = await ServeLimitedAsync(DomainStore(new ManualTimeProvider(), enabled: false), limits);
        var withLines = await ServeLimitedAsync(DomainStore(new ManualTimeProvider(), enabled: true), limits);

        StringAssert.Contains(withoutLines, "250 2.0.0 Message accepted\r\n");
        StringAssert.EndsWith(withLines, "552 5.3.4 Message exceeds the size limit\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_OptionOff_StoresTheBytesItAlwaysDid()
    {
        var clock = new ManualTimeProvider();
        var store = DomainStore(clock, enabled: false);

        await ServeAsync(store, Session(Ann, "ann2@example.com"), clock, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { TraceFields("a@x") + Body }, Inbox(store, Ann).ToList());
    }

    private async Task<string> ServeLimitedAsync(MailboxStore store, ExchangeLimits limits)
    {
        var connection = await ServeAsync(store, Session(Ann), new ManualTimeProvider(), TestContext.CancellationToken, limits);
        return RepliesAfterHello(connection);
    }

    [TestMethod]
    public async Task TeeStream_WritesEveryTargetAndRefusesWhatAWriteOnlyStreamCannotDo()
    {
        var first = new MemoryStream();
        var second = new MemoryStream();
        await using var tee = new SmtpTeeStream([first, second]);

        await tee.WriteAsync(new byte[] { 1, 2 }, TestContext.CancellationToken);
        tee.Write([3], 0, 1);
        tee.Flush();

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, first.ToArray());
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, second.ToArray());
        Assert.IsFalse(tee.CanRead);
        Assert.IsFalse(tee.CanSeek);
        Assert.IsTrue(tee.CanWrite);
        Assert.Throws<NotSupportedException>(() => tee.Length);
        Assert.Throws<NotSupportedException>(() => tee.Position);
        Assert.Throws<NotSupportedException>(() => tee.Position = 0);
        Assert.Throws<NotSupportedException>(() => tee.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => tee.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => tee.SetLength(0));
    }
}
