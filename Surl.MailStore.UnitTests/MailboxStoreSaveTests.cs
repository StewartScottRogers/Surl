using System.Text;
using static Surl.MailStore.PersistedStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// Writing the store back to its files (ADR-0050, decision 7): what a second store loads, when
/// message files come and go, and what a write that fails leaves behind.
/// </summary>
[TestClass]
public sealed class MailboxStoreSaveTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SaveChangesAsync_StoreWithoutFiles_DoesNothing()
    {
        var store = MailStoreFixture.NewStore("al");
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(store, "hi", "al@x"));

        await store.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreEqual("hi", MailStoreFixture.Fetch(store, store.ViewFor("al"), "INBOX", 1));
    }

    [TestMethod]
    public async Task SaveChangesAsync_StoreThenLoaded_HoldsTheSameMailboxesMessagesFlagsUidsAndUidValidity()
    {
        var fileSystem = NewFileSystem();
        var clock = new SettableTimeProvider();
        var first = await LoadAsync(fileSystem, ["al", "bo"], timeProvider: clock);
        var al = first.ViewFor("al");
        Assert.AreEqual(MailStoreOutcome.Succeeded, first.CreateMailbox(al, "Sent"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(first, "to both", "al@x", "bo@x"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(first, "second", "al@x"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, first.Append(al, "Sent", "sent"u8, MailFlags.Answered, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-7)), out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, first.ChangeFlags(al, "INBOX", 1, MailFlagChange.Add, MailFlags.Seen | MailFlags.Deleted, out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, first.ChangeFlags(al, "INBOX", 2, MailFlagChange.Add, MailFlags.Flagged, out _));
        await first.SaveChangesAsync(TestContext.CancellationToken);

        var second = await LoadAsync(fileSystem, ["al", "bo"], timeProvider: clock);

        Assert.AreEqual(0, second.ChangeCount);
        foreach (var (owner, mailbox) in new[] { ("al", "INBOX"), ("al", "Sent"), ("bo", "INBOX") })
        {
            var expected = MailStoreFixture.Read(first, first.ViewFor(owner), mailbox);
            var actual = MailStoreFixture.Read(second, second.ViewFor(owner), mailbox);
            Assert.AreEqual(expected.Name, actual.Name);
            Assert.AreEqual(expected.UidValidity, actual.UidValidity);
            Assert.AreEqual(expected.NextUid, actual.NextUid);
            CollectionAssert.AreEqual(expected.Messages.ToArray(), actual.Messages.ToArray());
            foreach (var message in expected.Messages)
            {
                Assert.AreEqual(
                    MailStoreFixture.Fetch(first, first.ViewFor(owner), mailbox, message.Uid),
                    MailStoreFixture.Fetch(second, second.ViewFor(owner), mailbox, message.Uid));
            }
        }

        CollectionAssert.AreEqual(first.ListMailboxes(al).ToArray(), second.ListMailboxes(second.ViewFor("al")).ToArray());
        Assert.AreEqual(TimeSpan.FromHours(-7), MailStoreFixture.Read(second, second.ViewFor("al"), "Sent").Messages[0].InternalDate.Offset);
    }

    [TestMethod]
    public async Task LoadAsync_StoreLoaded_GivesUidValiditiesAndMessageFilesItHasNeverGiven()
    {
        var fileSystem = NewFileSystem();
        var first = await LoadAsync(fileSystem, ["al"]);
        Assert.AreEqual(MailStoreOutcome.Succeeded, first.CreateMailbox(first.ViewFor("al"), "Sent"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(first, "one", "al@x"));
        await first.SaveChangesAsync(TestContext.CancellationToken);
        var sentValidity = MailStoreFixture.Read(first, first.ViewFor("al"), "Sent").UidValidity;

        var second = await LoadAsync(fileSystem, ["al"]);
        Assert.AreEqual(MailStoreOutcome.Succeeded, second.CreateMailbox(second.ViewFor("al"), "Drafts"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(second, "two", "al@x"));
        await second.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreEqual(sentValidity + 1, MailStoreFixture.Read(second, second.ViewFor("al"), "Drafts").UidValidity);
        Assert.AreEqual("one", Encoding.ASCII.GetString(ReadFile(fileSystem, MessagePath(0))));
        Assert.AreEqual("two", Encoding.ASCII.GetString(ReadFile(fileSystem, MessagePath(1))));
    }

    [TestMethod]
    public async Task LoadAsync_OwnerNoLongerAnAccount_IsKeptUnreachedAndRestoredWhenTheAccountReturns()
    {
        var fileSystem = NewFileSystem();
        var first = await LoadAsync(fileSystem, ["al", "bo"]);
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(first, "for bo", "bo@x"));
        await first.SaveChangesAsync(TestContext.CancellationToken);

        var withoutBo = await LoadAsync(fileSystem, ["al"]);
        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, withoutBo.LookUpRecipient("bo@x", out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(withoutBo, "for al", "al@x"));
        await withoutBo.SaveChangesAsync(TestContext.CancellationToken);

        var withBo = await LoadAsync(fileSystem, ["al", "bo"]);
        Assert.AreEqual("for bo", MailStoreFixture.Fetch(withBo, withBo.ViewFor("bo"), "INBOX", 1));
        Assert.AreEqual("for al", MailStoreFixture.Fetch(withBo, withBo.ViewFor("al"), "INBOX", 1));
    }

    [TestMethod]
    public async Task SaveChangesAsync_AnonymousOwner_IsWrittenAndLoaded()
    {
        var fileSystem = NewFileSystem();
        var first = await LoadAsync(fileSystem, ["al"], allowAnonymous: true);
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(first, "anyone", "who@x"));
        await first.SaveChangesAsync(TestContext.CancellationToken);

        var second = await LoadAsync(fileSystem, ["al"], allowAnonymous: true);

        Assert.AreEqual(0, second.ChangeCount);
        Assert.AreEqual("anyone", MailStoreFixture.Fetch(second, second.ViewFor(null), "INBOX", 1));
    }

    [TestMethod]
    public async Task SaveChangesAsync_NothingChangedSinceTheLastSave_WritesNothing()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"]);
        await store.SaveChangesAsync(TestContext.CancellationToken);
        fileSystem.DeleteFile(IndexPath);

        await store.SaveChangesAsync(TestContext.CancellationToken);

        Assert.IsFalse(Exists(fileSystem, IndexPath));
    }

    [TestMethod]
    public async Task SaveChangesAsync_ExpungedMessage_ItsFileIsDeletedAfterTheIndexNoLongerNamesIt()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"]);
        var al = store.ViewFor("al");
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(store, "bye", "al@x"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Copy(al, "INBOX", [1], "INBOX", out _));
        await store.SaveChangesAsync(TestContext.CancellationToken);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ChangeFlags(al, "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Expunge(al, "INBOX", out _));

        await store.SaveChangesAsync(TestContext.CancellationToken);

        Assert.IsTrue(Exists(fileSystem, MessagePath(0)), "The copy still refers to the file.");
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ChangeFlags(al, "INBOX", 2, MailFlagChange.Add, MailFlags.Deleted, out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Expunge(al, "INBOX", out _));
        await store.SaveChangesAsync(TestContext.CancellationToken);
        Assert.IsFalse(Exists(fileSystem, MessagePath(0)));
        var reloaded = await LoadAsync(fileSystem, ["al"]);
        Assert.IsEmpty(MailStoreFixture.Read(reloaded, reloaded.ViewFor("al"), "INBOX").Messages);
    }

    [TestMethod]
    public async Task SaveChangesAsync_MessageRemovedBeforeASave_IsNeverWritten()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"]);
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(store, "brief", "al@x"));
        store.LockMaildrop(store.ViewFor("al"), out var maildrop);
        using (maildrop)
        {
            Assert.AreEqual(1, maildrop!.RemoveMessages([1]));
        }

        await store.SaveChangesAsync(TestContext.CancellationToken);

        Assert.IsFalse(Exists(fileSystem, MessagePath(0)));
        Assert.IsTrue(Exists(fileSystem, IndexPath));
    }

    [TestMethod]
    public async Task SaveChangesAsync_IndexWriteThrows_LeavesTheStoreChangedAndTheNextChangeRewritesTheIndex()
    {
        var fileSystem = new UnitTestFaultingContentFileSystem { FailMovesTo = "index", Failure = new UnauthorizedAccessException("Access denied.") };
        var store = await LoadAsync(fileSystem, ["al"]);
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(store, "kept", "al@x"));

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => store.SaveChangesAsync(TestContext.CancellationToken));

        Assert.AreEqual("kept", MailStoreFixture.Fetch(store, store.ViewFor("al"), "INBOX", 1));
        Assert.IsFalse(Exists(fileSystem, IndexPath));
        Assert.HasCount(1, fileSystem.Files.EnumerateDirectoryEntryNames(StateFolder), "Only the messages folder: the temporary index is deleted.");
        fileSystem.FailMovesTo = null;
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ChangeFlags(store.ViewFor("al"), "INBOX", 1, MailFlagChange.Add, MailFlags.Seen, out _));
        await store.SaveChangesAsync(TestContext.CancellationToken);
        var reloaded = await LoadAsync(fileSystem.Files, ["al"]);
        Assert.AreEqual(MailFlags.Seen, MailStoreFixture.Read(reloaded, reloaded.ViewFor("al"), "INBOX").Messages.Single().Flags);
        Assert.AreEqual("kept", MailStoreFixture.Fetch(reloaded, reloaded.ViewFor("al"), "INBOX", 1));
    }

    [TestMethod]
    public async Task SaveChangesAsync_MessageFilesCannotBeDeleted_AreReportedLeftBehindAndIgnoredAtTheNextLoad()
    {
        var fileSystem = new UnitTestFaultingContentFileSystem();
        var store = await LoadAsync(fileSystem, ["al"]);
        var al = store.ViewFor("al");
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(store, "one", "al@x"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(store, "two", "al@x"));
        await store.SaveChangesAsync(TestContext.CancellationToken);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ChangeFlags(al, "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ChangeFlags(al, "INBOX", 2, MailFlagChange.Add, MailFlags.Deleted, out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Expunge(al, "INBOX", out _));
        fileSystem.FailDeletesOf = "messages";

        var failure = await Assert.ThrowsExactlyAsync<IOException>(() => store.SaveChangesAsync(TestContext.CancellationToken));

        Assert.AreEqual("The disk failed.", failure.Message);
        Assert.IsTrue(Exists(fileSystem, MessagePath(0)));
        Assert.IsTrue(Exists(fileSystem, MessagePath(1)));
        fileSystem.FailDeletesOf = null;
        await store.SaveChangesAsync(TestContext.CancellationToken);
        Assert.IsTrue(Exists(fileSystem, MessagePath(0)), "Left behind: no later save retries it.");
        var reloaded = await LoadAsync(fileSystem.Files, ["al"]);
        Assert.IsEmpty(MailStoreFixture.Read(reloaded, reloaded.ViewFor("al"), "INBOX").Messages);
    }

    [TestMethod]
    public async Task SaveChangesAsync_LeftoverTemporaryFiles_AreIgnoredAndLeftAlone()
    {
        var fileSystem = NewFileSystem();
        var leftover = Path.Join(MessagesFolder, ".pending-0123456789abcdef0123456789abcdef");
        WriteFile(fileSystem, leftover, "partial"u8.ToArray());
        WriteFile(fileSystem, MessagePath(0), "crashed before its index"u8.ToArray());
        var store = await LoadAsync(fileSystem, ["al"]);
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(store, "new", "al@x"));

        await store.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreEqual("new", Encoding.ASCII.GetString(ReadFile(fileSystem, MessagePath(0))), "A later message given the number replaces the file.");
        Assert.IsTrue(Exists(fileSystem, leftover));
    }
}
