using System.Text;
using static Surl.MailStore.MailStoreFixture;

namespace Surl.MailStore;

[TestClass]
public sealed class MailboxStoreMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Deliver_ToAnAccount_StoresTheBytesExactlyInItsInbox()
    {
        var store = NewStore("alice", "bob");

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "Subject: hi\r\n\r\n.body\r\n", "<alice@example.com>"));

        var inbox = Read(store, store.ViewFor("alice"), "INBOX");
        Assert.AreEqual(new MailMessageSummary(1, MailFlags.None, Now, 22), inbox.Messages.Single());
        Assert.AreEqual(2u, inbox.NextUid);
        Assert.AreEqual("Subject: hi\r\n\r\n.body\r\n", Fetch(store, store.ViewFor("alice"), "INBOX", 1));
        Assert.AreEqual(0, Read(store, store.ViewFor("bob"), "INBOX").Messages.Count);
        Assert.AreEqual(1, store.ChangeCount);
    }

    [TestMethod]
    public void Deliver_ToSeveralAccounts_StoresACopyInEachInbox()
    {
        var store = NewStore("alice", "bob");

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "m", "<alice@x>", "<bob@y>"));

        Assert.AreEqual("m", Fetch(store, store.ViewFor("alice"), "INBOX", 1));
        Assert.AreEqual("m", Fetch(store, store.ViewFor("bob"), "INBOX", 1));
        Assert.AreEqual(1, store.ChangeCount);
    }

    [TestMethod]
    public void Deliver_NoRecipients_StoresNothing()
    {
        var store = NewStore("alice");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([], "m"u8));

        Assert.AreEqual(0, Read(store, store.ViewFor("alice"), "INBOX").Messages.Count);
        Assert.AreEqual(0, store.ChangeCount);
    }

    [TestMethod]
    public void Deliver_NullRecipients_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => NewStore().Deliver(null!, "m"u8));
    }

    [TestMethod]
    public void Append_ToANamedMailbox_StoresWithTheGivenFlagsAndDate()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        store.CreateMailbox(view, "Sent");
        var date = new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.FromMinutes(-330));

        var outcome = store.Append(view, "Sent", "abc"u8, MailFlags.Seen | MailFlags.Draft, date, out var stored);

        Assert.AreEqual(MailStoreOutcome.Succeeded, outcome);
        var sent = Read(store, view, "Sent");
        Assert.AreEqual(new MailStoredUid(sent.UidValidity, 1), stored);
        Assert.AreEqual(new MailMessageSummary(1, MailFlags.Seen | MailFlags.Draft, date, 3), sent.Messages.Single());
        Assert.AreEqual(date.Offset, sent.Messages.Single().InternalDate.Offset);
        Assert.AreEqual("abc", Fetch(store, view, "Sent", 1));
    }

    [TestMethod]
    public void Append_WithNoDate_IsDatedNow()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(view, "inbox", "abc"u8, MailFlags.None, null, out var stored));

        Assert.AreEqual(1u, stored.Uid);
        Assert.AreEqual(Now, Read(store, view, "INBOX").Messages.Single().InternalDate);
    }

    [TestMethod]
    public void Append_ToAMissingMailboxOrAnEmptyView_IsMailboxMissing()
    {
        var store = NewStore("alice");

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.Append(store.ViewFor("alice"), "Sent", "m"u8, MailFlags.None, null, out var stored));
        Assert.AreEqual(default, stored);
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.Append(store.ViewFor("carol"), "INBOX", "m"u8, MailFlags.None, null, out _));
        Assert.AreEqual(0, store.ChangeCount);
    }

    [TestMethod]
    public void Append_BadArgument_Throws()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => store.Append(view, "INBOX", "m"u8, (MailFlags)32, null, out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.Append(null!, "INBOX", "m"u8, MailFlags.None, null, out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.Append(view, null!, "m"u8, MailFlags.None, null, out _));
    }

    [TestMethod]
    public void Uids_AscendAndAreNeverReusedAfterExpunge()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        Deliver(store, "1", "<alice@x>");
        Deliver(store, "2", "<alice@x>");
        Deliver(store, "3", "<alice@x>");

        store.ChangeFlags(view, "INBOX", 3, MailFlagChange.Add, MailFlags.Deleted, out _);
        store.Expunge(view, "INBOX", out _);
        Deliver(store, "4", "<alice@x>");

        CollectionAssert.AreEqual(new uint[] { 1, 2, 4 }, Uids(store, view, "INBOX"));
        Assert.AreEqual(5u, Read(store, view, "INBOX").NextUid);
    }

    [TestMethod]
    public void UidValidity_IsStableAcrossChangesAndNeverRepeated()
    {
        var clock = new SettableTimeProvider();
        var store = new MailboxStore(["alice", "bob"], false, clock);
        var view = store.ViewFor("alice");
        var seconds = (uint)clock.UtcNow.ToUnixTimeSeconds();
        var inbox = Read(store, view, "INBOX").UidValidity;

        Deliver(store, "m", "<alice@x>");
        store.CreateMailbox(view, "Sent");
        var sent = Read(store, view, "Sent").UidValidity;
        store.DeleteMailbox(view, "Sent");
        store.CreateMailbox(view, "Sent");
        var sentAgain = Read(store, view, "Sent").UidValidity;
        clock.UtcNow = clock.UtcNow.AddDays(1);
        store.CreateMailbox(view, "Later");

        Assert.AreEqual(seconds, inbox);
        Assert.AreEqual(seconds + 1, Read(store, store.ViewFor("bob"), "INBOX").UidValidity);
        Assert.AreEqual(inbox, Read(store, view, "INBOX").UidValidity);
        Assert.AreEqual(seconds + 2, sent);
        Assert.AreEqual(seconds + 3, sentAgain);
        Assert.AreEqual(seconds + 86_400, Read(store, view, "Later").UidValidity);
    }

    [TestMethod]
    public void FetchMessage_MissingMailboxOrMessage_SaysWhichAndReturnsNoBytes()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.FetchMessage(view, "Sent", 1, out var message));
        Assert.IsTrue(message.IsEmpty);
        Assert.AreEqual(MailStoreOutcome.MessageMissing, store.FetchMessage(view, "INBOX", 1, out message));
        Assert.IsTrue(message.IsEmpty);
    }

    [TestMethod]
    [DataRow(MailFlagChange.Add, MailFlags.Flagged, MailFlags.Seen | MailFlags.Answered | MailFlags.Flagged)]
    [DataRow(MailFlagChange.Remove, MailFlags.Seen, MailFlags.Answered)]
    [DataRow(MailFlagChange.Replace, MailFlags.Deleted | MailFlags.Draft, MailFlags.Deleted | MailFlags.Draft)]
    public void ChangeFlags_SetsClearsOrReplaces(MailFlagChange change, MailFlags flags, MailFlags expected)
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        store.Append(view, "INBOX", "m"u8, MailFlags.Seen | MailFlags.Answered, null, out _);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ChangeFlags(view, "INBOX", 1, change, flags, out var result));

        Assert.AreEqual(expected, result);
        Assert.AreEqual(expected, Read(store, view, "INBOX").Messages.Single().Flags);
        Assert.AreEqual(2, store.ChangeCount);
    }

    [TestMethod]
    public void ChangeFlags_ThatChangesNothing_IsNoChange()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        store.Append(view, "INBOX", "m"u8, MailFlags.Seen, null, out _);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ChangeFlags(view, "INBOX", 1, MailFlagChange.Add, MailFlags.Seen, out var result));

        Assert.AreEqual(MailFlags.Seen, result);
        Assert.AreEqual(1, store.ChangeCount);
    }

    [TestMethod]
    public void ChangeFlags_MissingMailboxOrMessage_SaysWhich()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.ChangeFlags(view, "Sent", 1, MailFlagChange.Add, MailFlags.Seen, out var result));
        Assert.AreEqual(MailFlags.None, result);
        Assert.AreEqual(MailStoreOutcome.MessageMissing, store.ChangeFlags(view, "INBOX", 1, MailFlagChange.Add, MailFlags.Seen, out result));
        Assert.AreEqual(MailFlags.None, result);
    }

    [TestMethod]
    public void ChangeFlags_UndefinedChangeOrFlag_Throws()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => store.ChangeFlags(view, "INBOX", 1, (MailFlagChange)3, MailFlags.Seen, out _));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => store.ChangeFlags(view, "INBOX", 1, (MailFlagChange)(-1), MailFlags.Seen, out _));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => store.ChangeFlags(view, "INBOX", 1, MailFlagChange.Add, (MailFlags)64, out _));
    }

    [TestMethod]
    public void Expunge_RemovesOnlyDeletedMessages()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        store.Append(view, "INBOX", "1"u8, MailFlags.Deleted, null, out _);
        store.Append(view, "INBOX", "2"u8, MailFlags.Seen, null, out _);
        store.Append(view, "INBOX", "3"u8, MailFlags.Deleted | MailFlags.Seen, null, out _);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Expunge(view, "INBOX", out var expunged));

        CollectionAssert.AreEqual(new uint[] { 1, 3 }, expunged.ToArray());
        CollectionAssert.AreEqual(new uint[] { 2 }, Uids(store, view, "INBOX"));
        Assert.AreEqual(MailStoreOutcome.MessageMissing, store.FetchMessage(view, "INBOX", 1, out _));
        Assert.AreEqual(4, store.ChangeCount);
    }

    [TestMethod]
    public void Expunge_NothingDeleted_IsNoChange()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        Deliver(store, "m", "<alice@x>");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Expunge(view, "INBOX", out var expunged));

        Assert.AreEqual(0, expunged.Count);
        Assert.AreEqual(1, store.ChangeCount);
    }

    [TestMethod]
    public void Expunge_MissingMailbox_IsMailboxMissing()
    {
        var store = NewStore("alice");

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.Expunge(store.ViewFor("alice"), "Sent", out var expunged));
        Assert.AreEqual(0, expunged.Count);
    }

    [TestMethod]
    public void Copy_GivesNewUidsAndKeepsFlagsDatesAndBytes()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        store.CreateMailbox(view, "Keep");
        store.Append(view, "Keep", "old"u8, MailFlags.None, null, out _);
        var date = new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.FromHours(2));
        store.Append(view, "INBOX", "one"u8, MailFlags.Seen, date, out _);
        store.Append(view, "INBOX", "two"u8, MailFlags.Flagged, date, out _);
        store.Append(view, "INBOX", "three"u8, MailFlags.None, date, out _);

        var outcome = store.Copy(view, "INBOX", [3, 1, 1, 99], "Keep", out var copied);

        Assert.AreEqual(MailStoreOutcome.Succeeded, outcome);
        var keep = Read(store, view, "Keep");
        Assert.AreEqual(keep.UidValidity, copied!.DestinationUidValidity);
        CollectionAssert.AreEqual(new uint[] { 1, 3 }, copied.SourceUids.ToArray());
        CollectionAssert.AreEqual(new uint[] { 2, 3 }, copied.CopyUids.ToArray());
        Assert.AreEqual(new MailMessageSummary(2, MailFlags.Seen, date, 3), keep.Messages[1]);
        Assert.AreEqual("three", Fetch(store, view, "Keep", 3));
        Assert.AreEqual(3, Read(store, view, "INBOX").Messages.Count);
    }

    [TestMethod]
    public void Copy_IntoTheSameMailbox_AddsCopies()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        Deliver(store, "m", "<alice@x>");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Copy(view, "inbox", [1], "INBOX", out _));

        CollectionAssert.AreEqual(new uint[] { 1, 2 }, Uids(store, view, "INBOX"));
    }

    [TestMethod]
    public void Copy_NothingFound_CopiesNothingAndIsNoChange()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Copy(view, "INBOX", [7], "INBOX", out var copied));

        Assert.AreEqual(0, copied!.CopyUids.Count);
        Assert.AreEqual(0, store.ChangeCount);
    }

    [TestMethod]
    public void Copy_MissingSourceOrDestination_IsMailboxMissing()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.Copy(view, "Sent", [1], "INBOX", out var copied));
        Assert.IsNull(copied);
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.Copy(view, "INBOX", [1], "Sent", out copied));
        Assert.IsNull(copied);
    }

    [TestMethod]
    public void Copy_NullUids_Throws()
    {
        var store = NewStore("alice");

        Assert.ThrowsExactly<ArgumentNullException>(() => store.Copy(store.ViewFor("alice"), "INBOX", null!, "INBOX", out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.Move(store.ViewFor("alice"), "INBOX", null!, "INBOX", out _));
    }

    [TestMethod]
    public void Move_CopiesThenRemovesTheOriginals()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        store.CreateMailbox(view, "Keep");
        var date = new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.FromHours(2));
        store.Append(view, "INBOX", "one"u8, MailFlags.Seen, date, out _);
        store.Append(view, "INBOX", "two"u8, MailFlags.Flagged, date, out _);
        store.Append(view, "INBOX", "three"u8, MailFlags.None, date, out _);

        var outcome = store.Move(view, "INBOX", [3, 1, 99], "Keep", out var moved);

        Assert.AreEqual(MailStoreOutcome.Succeeded, outcome);
        Assert.AreEqual(Read(store, view, "Keep").UidValidity, moved!.DestinationUidValidity);
        CollectionAssert.AreEqual(new uint[] { 1, 3 }, moved.SourceUids.ToArray());
        CollectionAssert.AreEqual(new uint[] { 1, 2 }, moved.CopyUids.ToArray());
        CollectionAssert.AreEqual(new uint[] { 2 }, Uids(store, view, "INBOX"));
        Assert.AreEqual(new MailMessageSummary(1, MailFlags.Seen, date, 3), Read(store, view, "Keep").Messages[0]);
        Assert.AreEqual("three", Fetch(store, view, "Keep", 2));
    }

    [TestMethod]
    public void Move_IntoTheSameMailbox_GivesNewUids()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        Deliver(store, "m", "<alice@x>");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Move(view, "INBOX", [1], "INBOX", out _));

        CollectionAssert.AreEqual(new uint[] { 2 }, Uids(store, view, "INBOX"));
        Assert.AreEqual("m", Fetch(store, view, "INBOX", 2));
    }

    [TestMethod]
    public void Move_NothingFoundOrMailboxMissing_MovesNothing()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        Deliver(store, "m", "<alice@x>");
        var changes = store.ChangeCount;

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Move(view, "INBOX", [7], "INBOX", out var moved));
        Assert.AreEqual(0, moved!.CopyUids.Count);
        Assert.AreEqual(changes, store.ChangeCount);
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.Move(view, "INBOX", [1], "Sent", out moved));
        Assert.IsNull(moved);
        CollectionAssert.AreEqual(new uint[] { 1 }, Uids(store, view, "INBOX"));
    }

    [TestMethod]
    public void ExpungeUids_RemovesOnlyDeletedMessagesNamed()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        store.Append(view, "INBOX", "1"u8, MailFlags.Deleted, null, out _);
        store.Append(view, "INBOX", "2"u8, MailFlags.Seen, null, out _);
        store.Append(view, "INBOX", "3"u8, MailFlags.Deleted, null, out _);
        var changes = store.ChangeCount;

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Expunge(view, "INBOX", [2, 9], out var expunged));
        Assert.AreEqual(0, expunged.Count);
        Assert.AreEqual(changes, store.ChangeCount);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Expunge(view, "INBOX", [3, 2], out expunged));

        CollectionAssert.AreEqual(new uint[] { 3 }, expunged.ToArray());
        CollectionAssert.AreEqual(new uint[] { 1, 2 }, Uids(store, view, "INBOX"));
    }

    [TestMethod]
    public void ExpungeUids_MissingMailboxOrNullUids_IsMailboxMissingOrThrows()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.Expunge(view, "Sent", [1], out var expunged));
        Assert.AreEqual(0, expunged.Count);
        Assert.ThrowsExactly<ArgumentNullException>(() => store.Expunge(view, "INBOX", null!, out _));
    }

    [TestMethod]
    public void ListMailboxes_IsInOrdinalOrderWithInboxInCapitals()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");
        foreach (var name in new[] { "b", "a", "B", "Inbox.Child" })
        {
            store.CreateMailbox(view, name);
        }

        CollectionAssert.AreEqual(new[] { "B", "INBOX", "Inbox.Child", "a", "b" }, store.ListMailboxes(view).ToArray());
    }

    [TestMethod]
    public void NullViewOrName_Throws()
    {
        var store = NewStore("alice");
        var view = store.ViewFor("alice");

        Assert.ThrowsExactly<ArgumentNullException>(() => store.ListMailboxes(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.ReadMailbox(null!, "INBOX", out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.ReadMailbox(view, null!, out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.LockMaildrop(null!, out _));
    }

    [TestMethod]
    public void Deliver_FromManyTasksAtOnce_LosesNothing()
    {
        var store = NewStore("alice", "bob");
        var recipients = new[] { Recipient(store, "<alice@x>"), Recipient(store, "<bob@x>") };

        Parallel.For(0, 400, index => store.Deliver(recipients, Encoding.ASCII.GetBytes($"message {index}")));

        foreach (var name in new[] { "alice", "bob" })
        {
            var view = store.ViewFor(name);
            CollectionAssert.AreEqual(Enumerable.Range(1, 400).Select(uid => (uint)uid).ToArray(), Uids(store, view, "INBOX"));
            var bodies = Enumerable.Range(1, 400).Select(uid => Fetch(store, view, "INBOX", (uint)uid)).Order().ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(0, 400).Select(index => $"message {index}").Order().ToArray(), bodies);
        }

        Assert.AreEqual(400, store.ChangeCount);
    }
}
