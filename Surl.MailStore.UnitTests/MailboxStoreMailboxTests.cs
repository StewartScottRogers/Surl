using static Surl.MailStore.MailStoreFixture;

namespace Surl.MailStore;

[TestClass]
public sealed class MailboxStoreMailboxTests
{
    [TestMethod]
    public void CreateMailbox_NewName_IsAnEmptyMailbox()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(view, "Sent"));

        var sent = Read(store, view, "Sent");
        Assert.AreEqual("Sent", sent.Name);
        Assert.AreEqual(1u, sent.NextUid);
        Assert.AreEqual(0, sent.Messages.Count);
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.ReadMailbox(view, "sent", out _));
        Assert.AreEqual(1, store.ChangeCount);
    }

    [TestMethod]
    [DataRow("INBOX")]
    [DataRow("inbox")]
    [DataRow("Sent")]
    public void CreateMailbox_ExistingName_IsAlreadyExists(string name)
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        store.CreateMailbox(view, "Sent");

        Assert.AreEqual(MailStoreOutcome.AlreadyExists, store.CreateMailbox(view, name));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("bad\r\nname")]
    public void CreateMailbox_InvalidName_IsInvalidName(string name)
    {
        var store = NewStore("alice");

        Assert.AreEqual(MailStoreOutcome.InvalidName, store.CreateMailbox(store.ViewFor("alice"), name));
        Assert.AreEqual(0, store.ChangeCount);
    }

    [TestMethod]
    public void CreateMailbox_InAnEmptyView_IsMailboxMissing()
    {
        var store = NewStore("alice");

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.CreateMailbox(store.ViewFor("carol"), "Sent"));
    }

    [TestMethod]
    public void CreateMailbox_NullArgument_Throws()
    {
        var store = NewStore("alice");

        Assert.ThrowsExactly<ArgumentNullException>(() => store.CreateMailbox(null!, "Sent"));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.CreateMailbox(store.ViewFor("alice"), null!));
    }

    [TestMethod]
    public void DeleteMailbox_RemovesItAndItsMessages()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        store.CreateMailbox(view, "Sent");
        store.Append(view, "Sent", "m"u8, MailFlags.None, null, out _);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.DeleteMailbox(view, "Sent"));

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.ReadMailbox(view, "Sent", out _));
        CollectionAssert.AreEqual(new[] { "INBOX" }, store.ListMailboxes(view).ToArray());
        Assert.AreEqual(3, store.ChangeCount);
    }

    [TestMethod]
    [DataRow("INBOX")]
    [DataRow("Inbox")]
    public void DeleteMailbox_Inbox_IsRefused(string name)
    {
        var store = NewStore("alice");

        Assert.AreEqual(MailStoreOutcome.InboxCannotBeDeleted, store.DeleteMailbox(store.ViewFor("alice"), name));
    }

    [TestMethod]
    public void DeleteMailbox_MissingMailboxOrEmptyView_IsMailboxMissing()
    {
        var store = NewStore("alice");

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.DeleteMailbox(store.ViewFor("alice"), "Sent"));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.DeleteMailbox(store.ViewFor("carol"), "INBOX"));
    }

    [TestMethod]
    public void RenameMailbox_KeepsMessagesUidsAndNextUidUnderANewUidValidity()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        store.CreateMailbox(view, "Old");
        store.Append(view, "Old", "1"u8, MailFlags.Seen, null, out _);
        store.Append(view, "Old", "2"u8, MailFlags.None, null, out _);
        var old = Read(store, view, "Old");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.RenameMailbox(view, "Old", "New"));

        var renamed = Read(store, view, "New");
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.ReadMailbox(view, "Old", out _));
        CollectionAssert.AreEqual(old.Messages.ToArray(), renamed.Messages.ToArray());
        Assert.AreEqual(old.NextUid, renamed.NextUid);
        Assert.IsGreaterThan(old.UidValidity, renamed.UidValidity);
        Assert.AreEqual("2", Fetch(store, view, "New", 2));
        CollectionAssert.AreEqual(new[] { "INBOX", "New" }, store.ListMailboxes(view).ToArray());
    }

    [TestMethod]
    public void RenameMailbox_Inbox_MovesItsMessagesAndLeavesItEmptyAndUnchanged()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        Deliver(store, "1", "<alice@x>");
        Deliver(store, "2", "<alice@x>");
        var inbox = Read(store, view, "INBOX");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.RenameMailbox(view, "inbox", "Old Mail"));

        var moved = Read(store, view, "Old Mail");
        CollectionAssert.AreEqual(inbox.Messages.ToArray(), moved.Messages.ToArray());
        Assert.AreEqual(3u, moved.NextUid);
        Assert.AreNotEqual(inbox.UidValidity, moved.UidValidity);
        var emptied = Read(store, view, "INBOX");
        Assert.AreEqual(0, emptied.Messages.Count);
        Assert.AreEqual(inbox.UidValidity, emptied.UidValidity);
        Assert.AreEqual(3u, emptied.NextUid);
        Deliver(store, "3", "<alice@x>");
        CollectionAssert.AreEqual(new uint[] { 3 }, Uids(store, view, "INBOX"));
    }

    [TestMethod]
    [DataRow("Sent", "INBOX", MailStoreOutcome.AlreadyExists)]
    [DataRow("Sent", "inbox", MailStoreOutcome.AlreadyExists)]
    [DataRow("Sent", "Drafts", MailStoreOutcome.AlreadyExists)]
    [DataRow("Sent", "Sent", MailStoreOutcome.AlreadyExists)]
    [DataRow("INBOX", "INBOX", MailStoreOutcome.AlreadyExists)]
    [DataRow("Sent", "", MailStoreOutcome.InvalidName)]
    [DataRow("Sent", "a\u0000b", MailStoreOutcome.InvalidName)]
    [DataRow("Missing", "Other", MailStoreOutcome.MailboxMissing)]
    public void RenameMailbox_Refused_ChangesNothing(string from, string to, MailStoreOutcome expected)
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        store.CreateMailbox(view, "Sent");
        store.CreateMailbox(view, "Drafts");

        Assert.AreEqual(expected, store.RenameMailbox(view, from, to));

        CollectionAssert.AreEqual(new[] { "Drafts", "INBOX", "Sent" }, store.ListMailboxes(view).ToArray());
        Assert.AreEqual(2, store.ChangeCount);
    }

    [TestMethod]
    public void RenameMailbox_InAnEmptyView_IsMailboxMissing()
    {
        var store = NewStore("alice");

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.RenameMailbox(store.ViewFor("carol"), "INBOX", "Other"));
    }

    [TestMethod]
    public void RenameMailbox_NullNewName_Throws()
    {
        var store = NewStore("alice");

        Assert.ThrowsExactly<ArgumentNullException>(() => store.RenameMailbox(store.ViewFor("alice"), "INBOX", null!));
    }
}
