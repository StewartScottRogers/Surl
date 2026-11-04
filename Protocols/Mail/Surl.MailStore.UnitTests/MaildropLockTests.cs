using System.Text;
using static Surl.MailStore.MailStoreFixture;

namespace Surl.MailStore;

[TestClass]
public sealed class MaildropLockTests
{
    [TestMethod]
    public void LockMaildrop_NumbersTheInboxInUidOrder()
    {
        var store = NewStore("alice");
        Deliver(store, "one", "<alice@x>");
        Deliver(store, "three", "<alice@x>");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(store.ViewFor("alice"), out var maildrop));

        using (maildrop)
        {
            CollectionAssert.AreEqual(
                new[] { new MaildropMessage(1, 1, 3), new MaildropMessage(2, 2, 5) },
                maildrop!.Messages.ToArray());
            Assert.AreEqual("three", Encoding.ASCII.GetString(maildrop.ReadMessage(2).Span));
        }
    }

    [TestMethod]
    public void LockMaildrop_WhileHeld_IsMaildropLocked_UntilReleased()
    {
        var store = NewStore("alice", "bob");
        store.LockMaildrop(store.ViewFor("alice"), out var first);

        Assert.AreEqual(MailStoreOutcome.MaildropLocked, store.LockMaildrop(store.ViewFor("alice"), out var second));
        Assert.IsNull(second);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(store.ViewFor("bob"), out var bob));

        first!.Dispose();
        first.Dispose();

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(store.ViewFor("alice"), out var third));
        third!.Dispose();
        bob!.Dispose();
    }

    [TestMethod]
    public void LockMaildrop_AllowAnonymous_EverySessionSharesOneLock()
    {
        var store = new MailboxStore([], allowAnonymous: true, new SettableTimeProvider());
        store.LockMaildrop(store.ViewFor("alice"), out var first);

        Assert.AreEqual(MailStoreOutcome.MaildropLocked, store.LockMaildrop(store.ViewFor(null), out _));

        first!.Dispose();
    }

    [TestMethod]
    public void LockMaildrop_ViewIsFixedAtLogin()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        Deliver(store, "old", "<alice@x>");
        store.LockMaildrop(view, out var maildrop);

        Deliver(store, "new", "<alice@x>");
        store.ChangeFlags(view, "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _);
        store.Expunge(view, "INBOX", out _);

        Assert.AreEqual(1, maildrop!.Messages.Count);
        Assert.AreEqual("old", Encoding.ASCII.GetString(maildrop.ReadMessage(1).Span));
        Assert.AreEqual(0, maildrop.RemoveMessages([1]));
        CollectionAssert.AreEqual(new uint[] { 2 }, Uids(store, view, "INBOX"));
        maildrop.Dispose();
    }

    [TestMethod]
    public void RemoveMessages_RemovesThemFromTheInbox()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        Deliver(store, "1", "<alice@x>");
        Deliver(store, "2", "<alice@x>");
        Deliver(store, "3", "<alice@x>");
        store.LockMaildrop(view, out var maildrop);

        Assert.AreEqual(2, maildrop!.RemoveMessages([3, 1, 3]));

        CollectionAssert.AreEqual(new uint[] { 2 }, Uids(store, view, "INBOX"));
        Assert.AreEqual(4, store.ChangeCount);
        Assert.AreEqual(0, maildrop.RemoveMessages([]));
        Assert.AreEqual(4, store.ChangeCount);
        maildrop.Dispose();
    }

    [TestMethod]
    public void RemoveMessages_AfterInboxRenamed_LeavesTheMovedMessages()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        Deliver(store, "1", "<alice@x>");
        store.LockMaildrop(view, out var maildrop);
        store.RenameMailbox(view, "INBOX", "Moved");

        Assert.AreEqual(0, maildrop!.RemoveMessages([1]));

        Assert.AreEqual("1", Fetch(store, view, "Moved", 1));
        maildrop.Dispose();
    }

    [TestMethod]
    public void MaildropLock_NumberOutOfRange_Throws_AndRemovesNothing()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        Deliver(store, "1", "<alice@x>");
        store.LockMaildrop(view, out var maildrop);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => maildrop!.ReadMessage(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => maildrop!.ReadMessage(2));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => maildrop!.RemoveMessages([1, 2]));
        Assert.ThrowsExactly<ArgumentNullException>(() => maildrop!.RemoveMessages(null!));

        Assert.AreEqual(1, Read(store, view, "INBOX").Messages.Count);
        maildrop!.Dispose();
    }

    [TestMethod]
    public void MaildropLock_AfterRelease_Throws()
    {
        var store = NewStore("alice");
        Deliver(store, "1", "<alice@x>");
        store.LockMaildrop(store.ViewFor("alice"), out var maildrop);

        maildrop!.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => maildrop.ReadMessage(1));
        Assert.ThrowsExactly<ObjectDisposedException>(() => maildrop.RemoveMessages([1]));
    }

    [TestMethod]
    public void LockMaildrop_EmptyView_IsAnEmptyMaildropThatLocksNothing()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("carol");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(view, out var first));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(view, out var second));

        Assert.AreEqual(0, first!.Messages.Count);
        Assert.AreEqual(0, first.RemoveMessages([]));
        first.Dispose();
        second!.Dispose();
    }
}
