using static Surl.MailStore.MailStoreFixture;
using static Surl.MailStore.PersistedStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// A mailbox's <c>quota=</c> and <c>folders=</c> (ADR-0102, decision 1.4).
/// </summary>
[TestClass]
public sealed class MailboxStoreQuotaTests
{
    private const string Ann = "ann@example.com";
    private const string Bob = "bob@example.com";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Deliver_PastAnOwnersQuota_StoresNothingForAnyRecipientAndOtherOwnersAreUnaffected()
    {
        var store = QuotaStore(annQuota: 10);
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "123456", $"<{Ann}>"));

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, Deliver(store, "12345", $"<{Ann}>", $"<{Bob}>"));

        Assert.HasCount(1, Uids(store, store.ViewFor(Ann), "INBOX"));
        Assert.IsEmpty(Uids(store, store.ViewFor(Bob), "INBOX"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "12345", $"<{Bob}>"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "1234", $"<{Ann}>"));
        Assert.HasCount(2, Uids(store, store.ViewFor(Ann), "INBOX"));
    }

    [TestMethod]
    public void Deliver_TwoCopiesToOneOwner_CountsEachCopyAgainstItsQuota()
    {
        var store = QuotaStore(annQuota: 10);

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, Deliver(store, "123456", $"<{Ann}>", $"<{Ann}>"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "12345", $"<{Ann}>", $"<{Ann}>"));
    }

    [TestMethod]
    public void Append_PastTheQuota_StoresNothing()
    {
        var store = QuotaStore(annQuota: 10);
        var view = store.ViewFor(Ann);

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, store.Append(view, "Archive", "12345678901"u8, MailFlags.None, null, out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(view, "Archive", "1234567890"u8, MailFlags.None, null, out _));
        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, store.Append(view, "INBOX", "1"u8, MailFlags.None, null, out _));
        Assert.HasCount(1, Uids(store, view, "Archive"));
        Assert.IsEmpty(Uids(store, view, "INBOX"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(store.ViewFor(Bob), "INBOX", "12345678901"u8, MailFlags.None, null, out _));
    }

    [TestMethod]
    public void CheckQuotaForAppend_AnswersFromTheLengthAlone()
    {
        var store = QuotaStore(annQuota: 10);
        var view = store.ViewFor(Ann);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CheckQuotaForAppend(view, "Archive", 10));
        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, store.CheckQuotaForAppend(view, "INBOX", 11));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.CheckQuotaForAppend(view, "Nowhere", 1));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CheckQuotaForAppend(store.ViewFor(Bob), "INBOX", long.MaxValue / 2));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => store.CheckQuotaForAppend(view, "INBOX", -1));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.CheckQuotaForAppend(null!, "INBOX", 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.CheckQuotaForAppend(view, null!, 1));
    }

    [TestMethod]
    public void Copy_PastTheQuota_CopiesNothing_AndAMoveWithinTheOwnerNeedsNoQuota()
    {
        var store = QuotaStore(annQuota: 10);
        var view = store.ViewFor(Ann);
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "123456", $"<{Ann}>"));

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, store.Copy(view, "INBOX", [1], "Archive", out var copied));
        Assert.IsNull(copied);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Move(view, "INBOX", [1], "Archive", out var moved));
        CollectionAssert.AreEqual(new uint[] { 1 }, moved!.CopyUids.ToArray());
    }

    [TestMethod]
    public async Task LoadAsync_OwnerAlreadyOverItsQuota_KeepsItsMailAndReceivesNothingUntilUnder()
    {
        var fileSystem = NewFileSystem();
        var first = await LoadQuotaStoreAsync(fileSystem, annQuota: null);
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(first, "123456", $"<{Ann}>"));
        await first.SaveChangesAsync(TestContext.CancellationToken);

        var store = await LoadQuotaStoreAsync(fileSystem, annQuota: 5);
        var view = store.ViewFor(Ann);

        Assert.AreEqual("123456", Fetch(store, view, "INBOX", 1));
        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, Deliver(store, "1", $"<{Ann}>"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, string.Empty, $"<{Ann}>"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ChangeFlags(view, "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Expunge(view, "INBOX", out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "12345", $"<{Ann}>"));
    }

    [TestMethod]
    public void Constructor_ConfiguredFolders_ExistAfterStartAndInboxIsSkipped()
    {
        var store = QuotaStore(annQuota: null);

        CollectionAssert.AreEqual(new[] { "Archive", "INBOX", "Sent" }, store.ListMailboxes(store.ViewFor(Ann)).ToArray());
        CollectionAssert.AreEqual(new[] { "INBOX" }, store.ListMailboxes(store.ViewFor(Bob)).ToArray());
        Assert.AreEqual(0, store.ChangeCount);
    }

    [TestMethod]
    public async Task LoadAsync_ConfiguredFoldersPresent_AreNotCreatedAgain()
    {
        var fileSystem = NewFileSystem();
        var first = await LoadQuotaStoreAsync(fileSystem, annQuota: null);
        Assert.AreEqual(4, first.ChangeCount);
        var archive = Read(first, first.ViewFor(Ann), "Archive").UidValidity;
        await first.SaveChangesAsync(TestContext.CancellationToken);

        var store = await LoadQuotaStoreAsync(fileSystem, annQuota: null);

        Assert.AreEqual(0, store.ChangeCount);
        Assert.AreEqual(archive, Read(store, store.ViewFor(Ann), "Archive").UidValidity);
        CollectionAssert.AreEqual(new[] { "Archive", "INBOX", "Sent" }, store.ListMailboxes(store.ViewFor(Ann)).ToArray());
    }

    [TestMethod]
    public void Constructor_ConfiguredFoldersPastMaxMailboxes_Throws()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => new MailboxStore(
            [], allowAnonymous: false, new SettableTimeProvider(), maxMailboxes: 1, mailDomains: Configuration(annQuota: null)));
    }

    [TestMethod]
    public void Constructor_AllowAnonymous_IgnoresQuotasAndFolders()
    {
        var store = new MailboxStore([], allowAnonymous: true, new SettableTimeProvider(), mailDomains: Configuration(annQuota: 1));

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "123456", $"<{Ann}>"));
        CollectionAssert.AreEqual(new[] { "INBOX" }, store.ListMailboxes(store.ViewFor(Ann)).ToArray());
    }

    private static MailboxStore QuotaStore(long? annQuota) =>
        new([], allowAnonymous: false, new SettableTimeProvider(), mailDomains: Configuration(annQuota));

    private Task<MailboxStore> LoadQuotaStoreAsync(Content.IContentFileSystem fileSystem, long? annQuota) =>
        MailboxStore.LoadAsync(
            new MailStoreFiles(fileSystem, StateFolder),
            [],
            allowAnonymous: false,
            new SettableTimeProvider(),
            mailDomains: Configuration(annQuota),
            cancellationToken: TestContext.CancellationToken);

    private static MailDomainConfiguration Configuration(long? annQuota) =>
        new(["example.com"], [Ann, Bob], [], [], [new MailboxSettings(Ann, annQuota, ["Archive", "Sent", "inbox", "Archive"])]);
}
