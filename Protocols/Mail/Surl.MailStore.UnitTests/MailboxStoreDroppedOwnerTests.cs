using Surl.Protocol.Abstractions;
using static Surl.MailStore.MailStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// Views, recipients and maildrop locks taken before a reset or restore dropped their owner
/// (ADR-0193).
/// </summary>
[TestClass]
public sealed class MailboxStoreDroppedOwnerTests
{
    [TestMethod]
    public void RemoveMessages_OwnerResetDropped_RemovesNothing()
    {
        var handles = HandlesToAnOwnerResetDropped();

        Assert.AreEqual(0, handles.Maildrop.RemoveMessages([1]));
        Assert.AreEqual("seeded", System.Text.Encoding.ASCII.GetString(handles.Maildrop.ReadMessage(1).Span));
        handles.Maildrop.Dispose();
        AssertUntouched(handles);
    }

    [TestMethod]
    public void LockMaildrop_ViewOfAnOwnerResetDropped_GivesAnEmptyMaildropThatLocksNothing()
    {
        var handles = HandlesToAnOwnerResetDropped();

        AssertEmptyUnlockedMaildrop(handles);
        AssertUntouched(handles);
    }

    [TestMethod]
    public void Deliver_RecipientWhoseOwnerResetDropped_SucceedsAndStoresNothing()
    {
        var handles = HandlesToAnOwnerResetDropped();

        var outcome = handles.Store.Deliver([handles.Recipient], Pending(handles.Store, "late"u8));

        Assert.AreEqual(MailStoreOutcome.Succeeded, outcome);
        AssertUntouched(handles);
    }

    [TestMethod]
    public async Task DeliverAsync_RecipientWhoseOwnerResetDropped_SucceedsAndStoresNothing()
    {
        var handles = HandlesToAnOwnerResetDropped();

        var outcome = await handles.Store.DeliverAsync([handles.Recipient], Pending(handles.Store, "late"u8), CancellationToken.None);

        Assert.AreEqual(MailStoreOutcome.Succeeded, outcome);
        AssertUntouched(handles);
    }

    [TestMethod]
    public async Task Deliver_CopiesForRecipientsWhoseOwnersResetDropped_SucceedsAndStoresNothing()
    {
        var handles = HandlesToAnOwnerResetDropped();
        var store = handles.Store;

        var outcome = store.Deliver([new MailOwnerCopy(handles.Recipient, Pending(store, "late"u8))]);
        var awaited = await store.DeliverAsync([new MailOwnerCopy(handles.Recipient, Pending(store, "later"u8))], CancellationToken.None);

        Assert.AreEqual(MailStoreOutcome.Succeeded, outcome);
        Assert.AreEqual(MailStoreOutcome.Succeeded, awaited);
        AssertUntouched(handles);
    }

    [TestMethod]
    public void Deliver_OneRecipientDroppedAndOneHeld_StoresOnlyTheHeldOwnersCopy()
    {
        var store = NewStore("ann");
        store.AddOwner("bob");
        var bob = Recipient(store, "<bob@x>");
        store.Reset();

        var outcome = store.Deliver([bob, Recipient(store, "<ann@x>")], Pending(store, "both"u8));

        Assert.AreEqual(MailStoreOutcome.Succeeded, outcome);
        Assert.AreEqual("both", Fetch(store, store.ViewFor("ann"), "INBOX", 1));
        Assert.AreEqual(1, store.TakeSnapshot().MessageCount);
    }

    [TestMethod]
    public void Deliver_OneCopyDroppedAndOneHeld_StoresOnlyTheHeldOwnersCopyAndCountsOnlyItsBytes()
    {
        var store = new MailboxStore(["ann"], allowAnonymous: false, new SettableTimeProvider(), maxTotalMessageBytes: 4);
        store.AddOwner("bob");
        var bob = Recipient(store, "<bob@x>");
        store.Reset();

        var outcome = store.Deliver(
            [new MailOwnerCopy(bob, Pending(store, "bobs"u8)), new MailOwnerCopy(Recipient(store, "<ann@x>"), Pending(store, "anns"u8))]);

        Assert.AreEqual(MailStoreOutcome.Succeeded, outcome);
        Assert.AreEqual("anns", Fetch(store, store.ViewFor("ann"), "INBOX", 1));
        var snapshot = store.TakeSnapshot();
        Assert.AreEqual(1, snapshot.MessageCount);
        Assert.AreEqual(4, snapshot.ByteCount);
    }

    [TestMethod]
    public async Task Deliver_OutOfSpaceRuleForADroppedOwner_IsNotAsked()
    {
        var faults = new SpendingFaultPlan(new FaultRule("full", FaultKind.OutOfSpace) { Account = "bob" });
        var store = new MailboxStore(["ann"], allowAnonymous: false, new SettableTimeProvider(), faults: faults);
        store.AddOwner("bob");
        var bob = Recipient(store, "<bob@x>");
        store.Reset();

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([bob], Pending(store, "a"u8)));
        Assert.AreEqual(MailStoreOutcome.Succeeded, await store.DeliverAsync([bob], Pending(store, "b"u8), CancellationToken.None));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([new MailOwnerCopy(bob, Pending(store, "c"u8))]));
    }

    [TestMethod]
    public void CreateMailbox_ViewOfAnOwnerResetDropped_IsMailboxMissingAndChangesNothing()
    {
        var handles = HandlesToAnOwnerResetDropped();

        var outcome = handles.Store.CreateMailbox(handles.View, "ghost");

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, outcome);
        Assert.AreEqual(0, handles.Store.ListMailboxes(handles.View).Count);
        AssertUntouched(handles);
    }

    [TestMethod]
    public void Handles_AfterTheNameIsAddedAgain_DoNotReachTheNewOwner()
    {
        var handles = HandlesToAnOwnerResetDropped();
        var store = handles.Store;
        store.AddOwner("bob");
        var changes = store.ChangeCount;

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([handles.Recipient], Pending(store, "late"u8)));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.CreateMailbox(handles.View, "ghost"));
        Assert.AreEqual(0, handles.Maildrop.RemoveMessages([1]));
        AssertEmptyUnlockedMaildrop(handles);

        Assert.AreEqual(changes, store.ChangeCount);
        Assert.AreEqual(0, Read(store, store.ViewFor("bob"), "INBOX").Messages.Count);
        Assert.AreEqual(1, store.ListMailboxes(store.ViewFor("bob")).Count);
    }

    [TestMethod]
    public void Handles_ToAnOwnerRestoreDropped_ChangeNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var store = new MailboxStore(["ann"], allowAnonymous: false, new SettableTimeProvider(), events: events);
        var snapshot = store.TakeSnapshot();
        var handles = TakeHandlesToANewOwner(store, events);
        store.Restore(snapshot);
        handles = handles with { ChangeCount = store.ChangeCount, EventCount = events.Events.Count };

        Assert.AreEqual(0, handles.Maildrop.RemoveMessages([1]));
        AssertEmptyUnlockedMaildrop(handles);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([handles.Recipient], Pending(store, "late"u8)));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.CreateMailbox(handles.View, "ghost"));
        handles.Maildrop.Dispose();

        AssertUntouched(handles);
    }

    [TestMethod]
    public void Handles_ToAnOwnerRestoreBringsBack_ReachItAgain()
    {
        var events = new RecordingSurlEventPublisher();
        var store = new MailboxStore(["ann"], allowAnonymous: false, new SettableTimeProvider(), events: events);
        var handles = TakeHandlesToANewOwner(store, events);
        var snapshot = store.TakeSnapshot();
        store.Reset();

        store.Restore(snapshot);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([handles.Recipient], Pending(store, "late"u8)));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(handles.View, "kept"));
        Assert.AreEqual(1, handles.Maildrop.RemoveMessages([1]));
        Assert.AreEqual("late", Fetch(store, handles.View, "INBOX", 2));
    }

    private static DroppedOwnerHandles HandlesToAnOwnerResetDropped()
    {
        var events = new RecordingSurlEventPublisher();
        var store = new MailboxStore(["ann"], allowAnonymous: false, new SettableTimeProvider(), events: events);
        var handles = TakeHandlesToANewOwner(store, events);
        store.Reset();
        return handles with { ChangeCount = store.ChangeCount, EventCount = events.Events.Count };
    }

    /// <summary>
    /// Adds the owner <c>bob</c>, seeds one message to it, and takes a recipient, a view and a
    /// maildrop lock of it.
    /// </summary>
    private static DroppedOwnerHandles TakeHandlesToANewOwner(MailboxStore store, RecordingSurlEventPublisher events)
    {
        store.AddOwner("bob");
        var recipient = Recipient(store, "<bob@x>");
        store.Seed("<bob@x>", "seeded"u8);
        var view = store.ViewFor("bob");
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(view, out var maildrop));
        return new(store, events, recipient, view, maildrop!, store.ChangeCount, events.Events.Count);
    }

    private static void AssertEmptyUnlockedMaildrop(DroppedOwnerHandles handles)
    {
        Assert.AreEqual(MailStoreOutcome.Succeeded, handles.Store.LockMaildrop(handles.View, out var first));
        Assert.AreEqual(MailStoreOutcome.Succeeded, handles.Store.LockMaildrop(handles.View, out var second));
        using (first)
        using (second)
        {
            Assert.AreEqual(0, first!.Messages.Count);
        }
    }

    /// <summary>
    /// Nothing was changed, published or stored since the owner was dropped.
    /// </summary>
    private static void AssertUntouched(DroppedOwnerHandles handles)
    {
        Assert.AreEqual(handles.ChangeCount, handles.Store.ChangeCount);
        Assert.AreEqual(handles.EventCount, handles.Events.Events.Count);
        var snapshot = handles.Store.TakeSnapshot();
        Assert.AreEqual(0, snapshot.MessageCount);
        Assert.AreEqual(0, snapshot.ByteCount);
    }

    private sealed record DroppedOwnerHandles(
        MailboxStore Store,
        RecordingSurlEventPublisher Events,
        MailRecipient Recipient,
        MailView View,
        MaildropLock Maildrop,
        long ChangeCount,
        int EventCount);
}
