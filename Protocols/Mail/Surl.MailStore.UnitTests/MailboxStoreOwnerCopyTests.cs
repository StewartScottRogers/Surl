using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.MailStore.MailStoreFixture;
using static Surl.MailStore.PersistedStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// Delivering one copy per owner, all or none, with each delivery event naming the addresses that
/// reached its owner (ADR-0053 Amendment 1, decisions 3, 4, 5 and 7).
/// </summary>
[TestClass]
public sealed class MailboxStoreOwnerCopyTests
{
    private static readonly FaultRule AnnOutOfSpace = new("f1", FaultKind.OutOfSpace) { Account = "ann" };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void DeliveredToEnabled_DefaultsToFalseAndTheCompositionRootCanSetIt()
    {
        Assert.IsFalse(new MailboxStore(["ann"], false, new SettableTimeProvider()).DeliveredToEnabled);
        Assert.IsTrue(new MailboxStore(["ann"], false, new SettableTimeProvider()) { DeliveredToEnabled = true }.DeliveredToEnabled);
    }

    [TestMethod]
    public void Deliver_TwoOwnersDifferingCopies_EachInboxHoldsOnlyItsOwnBytesAndEventNamesItsAddresses()
    {
        var events = new RecordingSurlEventPublisher();
        var store = new MailboxStore(["ann", "bob"], false, new SettableTimeProvider(), events: events);
        var copies = new[]
        {
            new MailOwnerCopy(Recipient(store, "<ann@x>"), Pending(store, "for ann"u8), ["ann@x", "alias@x"]),
            new MailOwnerCopy(Recipient(store, "<bob@x>"), Pending(store, "for bob!"u8), ["bob@x"]),
        };

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver(copies));

        Assert.AreEqual("for ann", Fetch(store, store.ViewFor("ann"), "INBOX", 1));
        Assert.AreEqual("for bob!", Fetch(store, store.ViewFor("bob"), "INBOX", 1));
        var delivered = events.Events.OfType<StoreChangedEvent>().ToArray();
        Assert.HasCount(2, delivered);
        Assert.AreEqual("ann/INBOX/;UID=1", delivered[0].Name);
        Assert.AreEqual(7, delivered[0].ByteCount);
        CollectionAssert.AreEqual(new[] { "ann@x", "alias@x" }, delivered[0].DeliveredFor!.ToArray());
        CollectionAssert.AreEqual(new[] { "bob@x" }, delivered[1].DeliveredFor!.ToArray());
        Assert.AreEqual(1, store.ChangeCount);
    }

    [TestMethod]
    public async Task DeliverAsync_TwoOwnersDifferingCopies_EachInboxHoldsOnlyItsOwnBytes()
    {
        var store = new MailboxStore(["ann", "bob"], false, new SettableTimeProvider());

        var outcome = await store.DeliverAsync(
            [new MailOwnerCopy(Recipient(store, "<ann@x>"), Pending(store, "A"u8)), new MailOwnerCopy(Recipient(store, "<bob@x>"), Pending(store, "BB"u8))],
            CancellationToken.None);

        Assert.AreEqual(MailStoreOutcome.Succeeded, outcome);
        Assert.AreEqual("A", Fetch(store, store.ViewFor("ann"), "INBOX", 1));
        Assert.AreEqual("BB", Fetch(store, store.ViewFor("bob"), "INBOX", 1));
    }

    [TestMethod]
    public void Deliver_NoCopies_SucceedsAndStoresNothing()
    {
        var store = new MailboxStore(["ann"], false, new SettableTimeProvider());

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([]));

        Assert.AreEqual(0, store.ChangeCount);
    }

    [TestMethod]
    public async Task DeliverAsync_NoCopiesOrACopyTooLarge_StoresNothing()
    {
        var store = new MailboxStore(["ann", "bob"], false, new SettableTimeProvider(), maxMessageBytes: 3);

        Assert.AreEqual(MailStoreOutcome.Succeeded, await store.DeliverAsync([], CancellationToken.None));
        Assert.AreEqual(MailStoreOutcome.MessageTooLarge, await store.DeliverAsync(Copies(store, "1", "1234"), CancellationToken.None));

        Assert.IsEmpty(Uids(store, store.ViewFor("ann"), "INBOX"));
    }

    [TestMethod]
    public void Deliver_NullArguments_Throw()
    {
        var store = new MailboxStore(["ann"], false, new SettableTimeProvider());

        Assert.Throws<ArgumentNullException>(() => store.Deliver((IReadOnlyList<MailOwnerCopy>)null!));
        Assert.Throws<ArgumentNullException>(() => new MailOwnerCopy(null!, Pending(store, "x"u8)));
        Assert.Throws<ArgumentNullException>(() => new MailOwnerCopy(Recipient(store, "<ann@x>"), null!));
        Assert.Throws<ArgumentNullException>(() => Recipient(store, "<ann@x>").ForAddresses(null!));
    }

    [TestMethod]
    public async Task DeliverAsync_NullCopies_Throws()
    {
        var store = new MailboxStore(["ann"], false, new SettableTimeProvider());

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.DeliverAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task Deliver_AnOwnersQuotaPassed_StoresNothingForAnyOwner()
    {
        var fileSystem = NewFileSystem();
        var store = await QuotaStoreAsync(fileSystem);
        var ann = store.ViewFor("ann@example.com");
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "123456", "<ann@example.com>"));

        var outcome = store.Deliver(Copies(store, "12345", "12345", "example.com"));

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, outcome);
        Assert.HasCount(1, Uids(store, ann, "INBOX"));
        Assert.IsEmpty(Uids(store, store.ViewFor("bob@example.com"), "INBOX"));
        Assert.IsEmpty(PendingFiles(fileSystem));
        Assert.HasCount(1, fileSystem.EnumerateDirectoryEntryNames(MessagesFolder));
    }

    [TestMethod]
    public async Task Deliver_StoreFull_StoresNothingForAnyOwner()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["ann", "bob"], maxTotalMessageBytes: 5);

        var outcome = store.Deliver(Copies(store, "123", "123"));

        Assert.AreEqual(MailStoreOutcome.StoreFull, outcome);
        Assert.IsEmpty(Uids(store, store.ViewFor("ann"), "INBOX"));
        Assert.IsEmpty(Uids(store, store.ViewFor("bob"), "INBOX"));
        Assert.IsEmpty(fileSystem.EnumerateDirectoryEntryNames(MessagesFolder));
    }

    [TestMethod]
    public async Task Deliver_OneCopyTooLarge_StoresNothingAndDeletesEveryPendingFile()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["ann", "bob"], maxMessageBytes: 3);

        var outcome = store.Deliver(Copies(store, "123", "1234"));

        Assert.AreEqual(MailStoreOutcome.MessageTooLarge, outcome);
        Assert.IsEmpty(Uids(store, store.ViewFor("ann"), "INBOX"));
        Assert.IsEmpty(fileSystem.EnumerateDirectoryEntryNames(MessagesFolder));
    }

    [TestMethod]
    public async Task Deliver_TheSecondFileCannotBeKept_IsStorageFailedAndStoresNothing()
    {
        var fileSystem = new UnitTestFaultingContentFileSystem { FailMovesTo = "0000000000000001" };
        var store = await LoadAsync(fileSystem, ["ann", "bob"]);

        var changesBefore = store.ChangeCount;
        var outcome = store.Deliver(Copies(store, "one", "two"));

        Assert.AreEqual(MailStoreOutcome.StorageFailed, outcome);
        Assert.IsEmpty(Uids(store, store.ViewFor("ann"), "INBOX"));
        Assert.AreEqual(changesBefore, store.ChangeCount);
        fileSystem.FailMovesTo = null;
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "next", "<ann@x>"));
        Assert.HasCount(1, Uids(store, store.ViewFor("ann"), "INBOX"));
    }

    [TestMethod]
    public void Deliver_OutOfSpaceFaultForOneOwner_StoresNothingForAny()
    {
        var store = new MailboxStore(["ann", "bob"], false, new SettableTimeProvider(), faults: new ScriptedFaultPlan(AnnOutOfSpace with { Account = "ann" }));

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, store.Deliver(Copies(store, "a", "b")));

        Assert.IsEmpty(Uids(store, store.ViewFor("bob"), "INBOX"));
    }

    [TestMethod]
    public async Task DeliverAsync_OutOfSpaceFaultForOneOwner_StoresNothingForAny()
    {
        var store = new MailboxStore(["ann", "bob"], false, new SettableTimeProvider(), faults: new ScriptedFaultPlan(AnnOutOfSpace with { Account = "ann" }));

        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, await store.DeliverAsync(Copies(store, "a", "b"), CancellationToken.None));

        Assert.IsEmpty(Uids(store, store.ViewFor("bob"), "INBOX"));
    }

    [TestMethod]
    public void Deliver_APendingMessageUsedAlready_ThrowsAndStillDisposesTheOthers()
    {
        var store = new MailboxStore(["ann", "bob"], false, new SettableTimeProvider());
        var copies = Copies(store, "a", "b");
        copies[0].Message.Dispose();

        Assert.Throws<InvalidOperationException>(() => store.Deliver(copies));

        Assert.Throws<InvalidOperationException>(() => store.Deliver([copies[1]]));
    }

    [TestMethod]
    public void Deliver_SingleMessageOverloads_LeaveDeliveredForNullUnlessTheRecipientRecordsAddresses()
    {
        var events = new RecordingSurlEventPublisher();
        var store = new MailboxStore(["ann", "bob"], false, new SettableTimeProvider(), events: events);

        store.Deliver([Recipient(store, "<ann@x>"), Recipient(store, "<bob@x>").ForAddresses(["bob@x", "again@x"])], Pending(store, "hi"u8));

        var delivered = events.Events.OfType<StoreChangedEvent>().ToArray();
        Assert.IsNull(delivered[0].DeliveredFor);
        CollectionAssert.AreEqual(new[] { "bob@x", "again@x" }, delivered[1].DeliveredFor!.ToArray());
    }

    [TestMethod]
    public void WithoutOwners_ARecipientWithAddressesRecorded_KeepsThem()
    {
        var store = new MailboxStore(["ann", "bob"], false, new SettableTimeProvider());
        var recipient = Recipient(store, "<ann@x>").ForAddresses(["ann@x"]);

        Assert.AreSame(recipient, recipient.WithoutOwners([]));
        Assert.IsNull(recipient.WithoutOwners(["ann"]));
    }

    private static MailOwnerCopy[] Copies(MailboxStore store, string annBytes, string bobBytes, string domain = "x") =>
    [
        new(Recipient(store, $"<ann@{domain}>"), Pending(store, Encoding.ASCII.GetBytes(annBytes))),
        new(Recipient(store, $"<bob@{domain}>"), Pending(store, Encoding.ASCII.GetBytes(bobBytes))),
    ];

    private static List<string> PendingFiles(Content.IContentFileSystem fileSystem) =>
        [.. fileSystem.EnumerateDirectoryEntryNames(MessagesFolder).Where(name => name.StartsWith(".pending-", StringComparison.Ordinal))];

    private Task<MailboxStore> QuotaStoreAsync(Content.IContentFileSystem fileSystem) =>
        MailboxStore.LoadAsync(
            new MailStoreFiles(fileSystem, StateFolder),
            [],
            allowAnonymous: false,
            new SettableTimeProvider(),
            mailDomains: new MailDomainConfiguration(
                ["example.com"],
                ["ann@example.com", "bob@example.com"],
                [],
                [],
                [new MailboxSettings("ann@example.com", 10, [])]),
            cancellationToken: TestContext.CancellationToken);
}
