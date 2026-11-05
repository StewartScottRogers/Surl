using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.MailStore.MailStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// What <see cref="MailboxStore"/> publishes on the event stream (ADR-0124 decision 1; ADR-0127).
/// </summary>
[TestClass]
public sealed class MailboxStoreEventTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Deliver_ToTwoOwners_PublishesOneDeliveredEventPerInboxCopyWithOwnerMailboxUidAndBytes()
    {
        var (store, events) = RecordingStore("ann", "bob");

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "hello", "<ann@x>", "<bob@x>"));

        CollectionAssert.AreEqual(
            new[] { Change(StoreChange.Delivered, "ann/INBOX/;UID=1", 5), Change(StoreChange.Delivered, "bob/INBOX/;UID=1", 5) },
            events.Events.ToArray());
    }

    [TestMethod]
    public void EveryChangeKind_PublishesOneEventEachInTheOrderTheChangesHappened()
    {
        var (store, events) = RecordingStore("ann");
        var view = store.ViewFor("ann");

        Deliver(store, "one", "<ann@x>");
        store.CreateMailbox(view, "Work");
        store.Append(view, "Work", "four"u8, MailFlags.None, null, out _);
        store.Copy(view, "INBOX", [1], "Work", out _);
        store.ChangeFlags(view, "Work", 1, MailFlagChange.Add, MailFlags.Deleted, out _);
        store.Expunge(view, "Work", out _);
        store.Move(view, "Work", [2], "INBOX", out _);
        store.RenameMailbox(view, "Work", "Play");
        store.DeleteMailbox(view, "Play");

        CollectionAssert.AreEqual(
            new[]
            {
                Change(StoreChange.Delivered, "ann/INBOX/;UID=1", 3),
                Change(StoreChange.Written, "ann/Work", null),
                Change(StoreChange.Written, "ann/Work/;UID=1", 4),
                Change(StoreChange.Written, "ann/Work/;UID=2", 3),
                Change(StoreChange.Written, "ann/Work/;UID=1", 4),
                Change(StoreChange.Deleted, "ann/Work/;UID=1", 4),
                Change(StoreChange.Written, "ann/INBOX/;UID=2", 3),
                Change(StoreChange.Deleted, "ann/Work/;UID=2", 3),
                Change(StoreChange.Deleted, "ann/Work", null),
                Change(StoreChange.Written, "ann/Play", null),
                Change(StoreChange.Deleted, "ann/Play", null),
            },
            events.Events.ToArray());
    }

    [TestMethod]
    public void RenameMailbox_Inbox_PublishesOnlyTheNewMailbox()
    {
        var (store, events) = RecordingStore("ann");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.RenameMailbox(store.ViewFor("ann"), "INBOX", "Old"));

        CollectionAssert.AreEqual(new[] { Change(StoreChange.Written, "ann/Old", null) }, events.Events.ToArray());
    }

    [TestMethod]
    public void ChangeFlags_ThatChangeNothing_PublishNothing()
    {
        var (store, events) = RecordingStore("ann");
        Deliver(store, "one", "<ann@x>");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ChangeFlags(store.ViewFor("ann"), "INBOX", 1, MailFlagChange.Remove, MailFlags.Seen, out _));

        Assert.HasCount(1, events.Events);
    }

    [TestMethod]
    public void Expunge_ByUid_PublishesEachRemovedMessage()
    {
        var (store, events) = RecordingStore("ann");
        var view = store.ViewFor("ann");
        Deliver(store, "one", "<ann@x>");
        store.ChangeFlags(view, "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Expunge(view, "INBOX", [1, 9], out _));

        Assert.AreEqual(Change(StoreChange.Deleted, "ann/INBOX/;UID=1", 3), events.Events[^1]);
    }

    [TestMethod]
    public void MaildropRemoveMessages_PublishesEachMessageDeleted()
    {
        var (store, events) = RecordingStore("ann");
        Deliver(store, "one", "<ann@x>");
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(store.ViewFor("ann"), out var maildrop));

        using (maildrop)
        {
            Assert.AreEqual(1, maildrop!.RemoveMessages([1]));
        }

        Assert.AreEqual(Change(StoreChange.Deleted, "ann/INBOX/;UID=1", 3), events.Events[^1]);
    }

    [TestMethod]
    public void RefusedChanges_PublishNothing()
    {
        var (store, events) = RecordingStore("ann");
        var view = store.ViewFor("ann");

        store.CreateMailbox(view, "INBOX");
        store.DeleteMailbox(view, "INBOX");
        store.Append(view, "Missing", "x"u8, MailFlags.None, null, out _);
        store.Expunge(view, "INBOX", out _);
        store.Copy(view, "INBOX", [1], "INBOX", out _);

        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public void SharedFolderAppend_IsNamedForTheSharedOwner()
    {
        var events = new RecordingSurlEventPublisher();
        var store = new MailboxStore(
            ["ann"],
            allowAnonymous: false,
            new SettableTimeProvider(),
            mailDomains: new MailDomainConfiguration(["example.com"], [], [], [], null, [new MailSharedFolder("team", ["ann"])]),
            events: events);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(store.ViewFor("ann"), "Shared/team", "x"u8, MailFlags.None, null, out _));

        Assert.AreEqual(Change(StoreChange.Written, ":shared/team/;UID=1", 1), events.Events[^1]);
    }

    [TestMethod]
    public void AnonymousDelivery_IsNamedForTheEmptyOwner()
    {
        var events = new RecordingSurlEventPublisher();
        var store = new MailboxStore([], allowAnonymous: true, new SettableTimeProvider(), events: events);

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "x", "<anyone@x>"));

        Assert.AreEqual(Change(StoreChange.Delivered, "/INBOX/;UID=1", 1), events.Events[^1]);
    }

    [TestMethod]
    public async Task LoadAsync_WithAPublisher_PublishesItsChanges()
    {
        var events = new RecordingSurlEventPublisher();
        var store = await MailboxStore.LoadAsync(
            new MailStoreFiles(PersistedStoreFixture.NewFileSystem(), PersistedStoreFixture.StateFolder),
            ["ann"],
            allowAnonymous: false,
            new SettableTimeProvider(),
            events: events,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(store.ViewFor("ann"), "INBOX", Encoding.ASCII.GetBytes("abc"), MailFlags.None, null, out _));

        CollectionAssert.AreEqual(new[] { Change(StoreChange.Written, "ann/INBOX/;UID=1", 3) }, events.Events.ToArray());
    }

    [TestMethod]
    public void MailStoreChangeName_NamesMailboxesAndMessages()
    {
        Assert.AreEqual("ann/Work/Q1", MailStoreChangeName.ForMailbox("ann", "Work/Q1"));
        Assert.AreEqual("ann/Work/Q1/;UID=4294967295", MailStoreChangeName.ForMessage("ann", "Work/Q1", uint.MaxValue));
    }

    private static (MailboxStore Store, RecordingSurlEventPublisher Events) RecordingStore(params string[] accountNames)
    {
        var events = new RecordingSurlEventPublisher();
        return (new MailboxStore(accountNames, allowAnonymous: false, new SettableTimeProvider(), events: events), events);
    }

    private static StoreChangedEvent Change(StoreChange change, string name, long? byteCount) =>
        new(StoreKind.Mail, change, name, byteCount, ExchangeId: null);
}
