using static Surl.MailStore.MailStoreFixture;

namespace Surl.MailStore;

[TestClass]
public sealed class MailboxStoreBoundTests
{
    [TestMethod]
    public void Deliver_PastMaxMessages_IsStoreFullAndStoresNothing()
    {
        var store = new MailboxStore(["alice", "bob"], false, new SettableTimeProvider(), maxMessages: 3);
        Deliver(store, "1", "<alice@x>", "<bob@x>");

        Assert.AreEqual(MailStoreOutcome.StoreFull, Deliver(store, "2", "<alice@x>", "<bob@x>"));

        Assert.AreEqual(1, Read(store, store.ViewFor("alice"), "INBOX").Messages.Count);
        Assert.AreEqual(1, Read(store, store.ViewFor("bob"), "INBOX").Messages.Count);
        Assert.AreEqual(1, store.ChangeCount);
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "3", "<alice@x>"));
    }

    [TestMethod]
    public void Deliver_PastMaxTotalMessageBytes_IsStoreFull_CountingCopiesOnce()
    {
        var store = new MailboxStore(["alice", "bob"], false, new SettableTimeProvider(), maxTotalMessageBytes: 10);

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "123456", "<alice@x>", "<bob@x>"));
        Assert.AreEqual(MailStoreOutcome.StoreFull, Deliver(store, "12345", "<alice@x>"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "1234", "<alice@x>"));
    }

    [TestMethod]
    public void Deliver_BytesFreedByExpunge_MakeRoomOnlyOnceNoCopyRemains()
    {
        var store = new MailboxStore(["alice", "bob"], false, new SettableTimeProvider(), maxTotalMessageBytes: 10);
        var alice = store.ViewFor("alice");
        var bob = store.ViewFor("bob");
        Deliver(store, "123456", "<alice@x>", "<bob@x>");

        store.ChangeFlags(alice, "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _);
        store.Expunge(alice, "INBOX", out _);
        Assert.AreEqual(MailStoreOutcome.StoreFull, Deliver(store, "123456", "<alice@x>"));

        store.ChangeFlags(bob, "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _);
        store.Expunge(bob, "INBOX", out _);
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "123456", "<alice@x>"));
    }

    [TestMethod]
    public void DeleteMailbox_FreesItsMessagesAndBytes()
    {
        var store = new MailboxStore(["alice"], false, new SettableTimeProvider(), maxMessages: 1, maxTotalMessageBytes: 5);
        var view = store.ViewFor("alice");
        store.CreateMailbox(view, "Sent");
        store.Append(view, "Sent", "12345"u8, MailFlags.None, null, out _);
        Assert.AreEqual(MailStoreOutcome.StoreFull, Deliver(store, "1", "<alice@x>"));

        store.DeleteMailbox(view, "Sent");

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "12345", "<alice@x>"));
    }

    [TestMethod]
    public void MessagePastMaxMessageBytes_IsMessageTooLarge()
    {
        var store = new MailboxStore(["alice"], false, new SettableTimeProvider(), maxMessageBytes: 3);
        var view = store.ViewFor("alice");

        Assert.AreEqual(MailStoreOutcome.MessageTooLarge, Deliver(store, "1234", "<alice@x>"));
        Assert.AreEqual(MailStoreOutcome.MessageTooLarge, store.Append(view, "INBOX", "1234"u8, MailFlags.None, null, out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "123", "<alice@x>"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(view, "INBOX", "123"u8, MailFlags.None, null, out _));
    }

    [TestMethod]
    public void Append_PastABound_IsStoreFull()
    {
        var store = new MailboxStore(["alice"], false, new SettableTimeProvider(), maxMessages: 1);
        var view = store.ViewFor("alice");
        store.Append(view, "INBOX", "1"u8, MailFlags.None, null, out _);

        Assert.AreEqual(MailStoreOutcome.StoreFull, store.Append(view, "INBOX", "2"u8, MailFlags.None, null, out var stored));

        Assert.AreEqual(default, stored);
    }

    [TestMethod]
    public void Copy_PastMaxMessages_IsStoreFullAndCopiesNothing()
    {
        var store = new MailboxStore(["alice"], false, new SettableTimeProvider(), maxMessages: 3);
        var view = store.ViewFor("alice");
        Deliver(store, "1", "<alice@x>");
        Deliver(store, "2", "<alice@x>");

        Assert.AreEqual(MailStoreOutcome.StoreFull, store.Copy(view, "INBOX", [1, 2], "INBOX", out var copied));

        Assert.IsNull(copied);
        Assert.AreEqual(2, Read(store, view, "INBOX").Messages.Count);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Copy(view, "INBOX", [1], "INBOX", out _));
    }

    [TestMethod]
    public void MessagePastTheLastUid_IsStoreFull()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        view.Owner!.Inbox.NextUid = uint.MaxValue - 1;

        Assert.AreEqual(MailStoreOutcome.StoreFull, Deliver(store, "m", "<alice@x>", "<alice@x>"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "m", "<alice@x>"));
        Assert.AreEqual(MailStoreOutcome.StoreFull, Deliver(store, "m", "<alice@x>"));
        Assert.AreEqual(MailStoreOutcome.StoreFull, store.Append(view, "INBOX", "m"u8, MailFlags.None, null, out _));
        Assert.AreEqual(MailStoreOutcome.StoreFull, store.Copy(view, "INBOX", [uint.MaxValue - 1], "INBOX", out _));
        Assert.AreEqual(MailStoreOutcome.StoreFull, store.Move(view, "INBOX", [uint.MaxValue - 1], "INBOX", out var moved));
        Assert.IsNull(moved);
        CollectionAssert.AreEqual(new[] { uint.MaxValue - 1 }, Uids(store, view, "INBOX"));
    }

    [TestMethod]
    public void Move_AtMaxMessages_MovesAllTheSame()
    {
        var store = new MailboxStore(["alice"], false, new SettableTimeProvider(), maxMessages: 2);
        var view = store.ViewFor("alice");
        store.CreateMailbox(view, "Keep");
        Deliver(store, "1", "<alice@x>");
        Deliver(store, "2", "<alice@x>");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Move(view, "INBOX", [1, 2], "Keep", out var moved));

        CollectionAssert.AreEqual(new uint[] { 1, 2 }, moved!.CopyUids.ToArray());
        Assert.AreEqual(0, Read(store, view, "INBOX").Messages.Count);
        Assert.AreEqual(MailStoreOutcome.StoreFull, Deliver(store, "3", "<alice@x>"));
    }

    [TestMethod]
    public void NewMailbox_WhenNoUidValidityIsLeft_IsStoreFull()
    {
        var clock = new SettableTimeProvider();
        var store = new MailboxStore(["alice"], false, clock);
        var view = store.ViewFor("alice");
        store.CreateMailbox(view, "Sent");
        clock.UtcNow = DateTimeOffset.FromUnixTimeSeconds(uint.MaxValue);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(view, "Last"));
        Assert.AreEqual(uint.MaxValue, Read(store, view, "Last").UidValidity);
        Assert.AreEqual(MailStoreOutcome.StoreFull, store.CreateMailbox(view, "Other"));
        Assert.AreEqual(MailStoreOutcome.StoreFull, store.RenameMailbox(view, "Sent", "Other"));
        CollectionAssert.AreEqual(new[] { "INBOX", "Last", "Sent" }, store.ListMailboxes(view).ToArray());
    }

    [TestMethod]
    public void Constructor_ClockPastTheLastUidValidity_Throws()
    {
        var clock = new SettableTimeProvider { UtcNow = DateTimeOffset.FromUnixTimeSeconds(uint.MaxValue + 1L) };

        Assert.ThrowsExactly<InvalidOperationException>(() => new MailboxStore(["alice"], false, clock));
    }

    [TestMethod]
    public void CreateMailbox_PastMaxMailboxes_IsTooManyMailboxes_NotCountingInboxes()
    {
        var store = new MailboxStore(["alice", "bob"], false, new SettableTimeProvider(), maxMailboxes: 1);
        var alice = store.ViewFor("alice");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(alice, "Sent"));
        Assert.AreEqual(MailStoreOutcome.TooManyMailboxes, store.CreateMailbox(store.ViewFor("bob"), "Sent"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.RenameMailbox(alice, "Sent", "Archive"));
        Assert.AreEqual(MailStoreOutcome.TooManyMailboxes, store.RenameMailbox(alice, "INBOX", "Old"));

        store.DeleteMailbox(alice, "Archive");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.RenameMailbox(alice, "INBOX", "Old"));
        Assert.AreEqual(MailStoreOutcome.TooManyMailboxes, store.CreateMailbox(alice, "Other"));
    }
}
