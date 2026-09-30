using System.Text;
using static Surl.MailStore.PersistedStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// Streaming a message body into its pending file as a server reads it, refusing it with
/// <see cref="MailStoreOutcome.StorageFailed"/> when that file cannot be written, and reading a
/// message's bytes from its file on fetch (ADR-0050, decision 7).
/// </summary>
[TestClass]
public sealed class MailboxStorePendingMessageTests
{
    private const string PendingFragment = ".pending-";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Deliver_PendingMessage_LandsInItsMessageFileWhenTheDeliveryCommits()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al", "bo"]);
        var pending = store.CreatePendingMessage();
        pending.Body.Write("streamed"u8);
        Assert.HasCount(1, fileSystem.EnumerateDirectoryEntryNames(MessagesFolder));
        Assert.IsFalse(Exists(fileSystem, MessagePath(0)));

        var outcome = store.Deliver([MailStoreFixture.Recipient(store, "al@x"), MailStoreFixture.Recipient(store, "bo@x")], pending);

        Assert.AreEqual(MailStoreOutcome.Succeeded, outcome);
        Assert.AreEqual("streamed", Encoding.ASCII.GetString(ReadFile(fileSystem, MessagePath(0))));
        CollectionAssert.AreEqual(new[] { "0000000000000000" }, fileSystem.EnumerateDirectoryEntryNames(MessagesFolder).ToArray(), "The pending file is renamed, not copied.");
        Assert.AreEqual("streamed", MailStoreFixture.Fetch(store, store.ViewFor("bo"), "INBOX", 1));
        await store.SaveChangesAsync(TestContext.CancellationToken);
        var reloaded = await LoadAsync(fileSystem, ["al", "bo"]);
        Assert.AreEqual("streamed", MailStoreFixture.Fetch(reloaded, reloaded.ViewFor("al"), "INBOX", 1));
    }

    [TestMethod]
    public async Task Dispose_PendingMessageAbandonedBeforeCommit_LeavesNoMessageAndDeletesItsPendingFile()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"]);
        var pending = store.CreatePendingMessage();
        await pending.Body.WriteAsync("half a bo"u8.ToArray(), TestContext.CancellationToken);

        pending.Dispose();
        pending.Dispose();

        Assert.IsEmpty(fileSystem.EnumerateDirectoryEntryNames(MessagesFolder));
        Assert.IsEmpty(MailStoreFixture.Uids(store, store.ViewFor("al"), "INBOX"));
        Assert.ThrowsExactly<InvalidOperationException>(() => store.Deliver([MailStoreFixture.Recipient(store, "al@x")], pending));
    }

    [TestMethod]
    public async Task Dispose_PendingFileCannotBeDeleted_IsLeftBehindAndIgnoredAtTheNextLoad()
    {
        var fileSystem = new UnitTestFaultingContentFileSystem { FailDeletesOf = PendingFragment };
        var store = await LoadAsync(fileSystem, ["al"]);
        var pending = store.CreatePendingMessage();
        pending.Body.Write("left"u8);

        pending.Dispose();

        Assert.HasCount(1, fileSystem.Files.EnumerateDirectoryEntryNames(MessagesFolder));
        var reloaded = await LoadAsync(fileSystem.Files, ["al"]);
        Assert.IsEmpty(MailStoreFixture.Uids(reloaded, reloaded.ViewFor("al"), "INBOX"));
    }

    [TestMethod]
    public async Task DeliverAndAppend_PendingFileCannotBeCreated_AreStorageFailedWithTheExceptionMessageAndStoreNothing()
    {
        var fileSystem = new UnitTestFaultingContentFileSystem { FailCreatesOf = PendingFragment };
        var store = await LoadAsync(fileSystem, ["al"]);
        var al = store.ViewFor("al");
        var delivered = store.CreatePendingMessage();
        delivered.Body.Write("lost"u8);
        var appended = store.CreatePendingMessage();
        appended.Body.Write("lost"u8);

        Assert.AreEqual("The disk failed.", delivered.StorageFailure);
        Assert.AreEqual(4, delivered.Length);
        Assert.AreEqual(MailStoreOutcome.StorageFailed, store.Deliver([MailStoreFixture.Recipient(store, "al@x")], delivered));
        Assert.AreEqual(MailStoreOutcome.StorageFailed, store.Append(al, "INBOX", appended, MailFlags.None, null, out var stored));

        Assert.AreEqual(default, stored);
        Assert.AreEqual("The disk failed.", appended.StorageFailure);
        Assert.IsEmpty(MailStoreFixture.Uids(store, al, "INBOX"));
    }

    [TestMethod]
    public async Task DeliverAndAppend_PendingFileCannotBeWritten_AreStorageFailedWithTheExceptionMessageAndDeleteIt()
    {
        var fileSystem = new UnitTestFaultingContentFileSystem { FailWritesOf = PendingFragment, Failure = new UnauthorizedAccessException("Access denied.") };
        var store = await LoadAsync(fileSystem, ["al"]);
        var al = store.ViewFor("al");
        var delivered = store.CreatePendingMessage();
        delivered.Body.Write("lost"u8.ToArray(), 0, 4);
        delivered.Body.Write("more"u8);
        var appended = store.CreatePendingMessage();
        await appended.Body.WriteAsync("lost"u8.ToArray(), 0, 4, TestContext.CancellationToken);

        Assert.AreEqual(MailStoreOutcome.StorageFailed, store.Deliver([MailStoreFixture.Recipient(store, "al@x")], delivered));
        Assert.AreEqual(MailStoreOutcome.StorageFailed, store.Append(al, "INBOX", appended, MailFlags.None, null, out _));

        Assert.AreEqual("Access denied.", delivered.StorageFailure);
        Assert.AreEqual(8, delivered.Length, "The rest of the body is counted and dropped.");
        Assert.AreEqual("Access denied.", appended.StorageFailure);
        Assert.IsEmpty(fileSystem.Files.EnumerateDirectoryEntryNames(MessagesFolder));
        Assert.IsEmpty(MailStoreFixture.Uids(store, al, "INBOX"));
    }

    [TestMethod]
    public async Task DeliverAndAppend_PendingFileCannotBeClosed_AreStorageFailed()
    {
        var fileSystem = new UnitTestFaultingContentFileSystem { FailWritesOf = PendingFragment };
        var store = await LoadAsync(fileSystem, ["al"]);
        var empty = store.CreatePendingMessage();

        Assert.AreEqual(MailStoreOutcome.StorageFailed, store.Deliver([MailStoreFixture.Recipient(store, "al@x")], empty));

        Assert.AreEqual("The disk failed.", empty.StorageFailure);
    }

    [TestMethod]
    public async Task DeliverAndAppend_PendingFileCannotBeRenamedIntoPlace_AreStorageFailedAndDeleteIt()
    {
        var fileSystem = new UnitTestFaultingContentFileSystem { FailMovesTo = "messages" };
        var store = await LoadAsync(fileSystem, ["al"]);
        var al = store.ViewFor("al");
        var delivered = store.CreatePendingMessage();
        delivered.Body.Write("lost"u8);
        var appended = store.CreatePendingMessage();
        appended.Body.Write("lost"u8);

        Assert.AreEqual(MailStoreOutcome.StorageFailed, store.Deliver([MailStoreFixture.Recipient(store, "al@x")], delivered));
        Assert.AreEqual(MailStoreOutcome.StorageFailed, store.Append(al, "INBOX", appended, MailFlags.None, null, out _));

        Assert.AreEqual("The disk failed.", delivered.StorageFailure);
        Assert.IsEmpty(fileSystem.Files.EnumerateDirectoryEntryNames(MessagesFolder));
        Assert.IsEmpty(MailStoreFixture.Uids(store, al, "INBOX"));
        fileSystem.FailMovesTo = null;
        var next = store.CreatePendingMessage();
        next.Body.Write("kept"u8);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([MailStoreFixture.Recipient(store, "al@x")], next));
        Assert.AreEqual("kept", Encoding.ASCII.GetString(ReadFile(fileSystem, MessagePath(0))), "A refused message gives up no file number.");
    }

    [TestMethod]
    public async Task Append_PendingMessage_LandsInItsMessageFileWithItsFlagsAndDate()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"]);
        var al = store.ViewFor("al");
        var date = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.FromHours(2));
        var pending = store.CreatePendingMessage();
        await pending.Body.WriteAsync("appended"u8.ToArray().AsMemory(), TestContext.CancellationToken);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(al, "INBOX", pending, MailFlags.Seen, date, out var stored));

        Assert.AreEqual(1u, stored.Uid);
        Assert.AreEqual("appended", Encoding.ASCII.GetString(ReadFile(fileSystem, MessagePath(0))));
        var summary = MailStoreFixture.Read(store, al, "INBOX").Messages.Single();
        Assert.AreEqual(MailFlags.Seen, summary.Flags);
        Assert.AreEqual(date, summary.InternalDate);
        Assert.AreEqual(8, summary.Size);
    }

    [TestMethod]
    public async Task Append_PendingMessageToAMissingMailbox_IsMailboxMissingAndDeletesItsPendingFile()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"]);
        var pending = store.CreatePendingMessage();
        pending.Body.Write("nowhere"u8);

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.Append(store.ViewFor("al"), "Nowhere", pending, MailFlags.None, null, out _));

        Assert.IsEmpty(fileSystem.EnumerateDirectoryEntryNames(MessagesFolder));
    }

    [TestMethod]
    public async Task Append_PendingMessageWithABitThatIsNoFlag_ThrowsAndDeletesItsPendingFile()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"]);
        var pending = store.CreatePendingMessage();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => store.Append(store.ViewFor("al"), "INBOX", pending, (MailFlags)0x80, null, out _));

        Assert.IsEmpty(fileSystem.EnumerateDirectoryEntryNames(MessagesFolder));
    }

    [TestMethod]
    public async Task DeliverAndAppend_PendingMessagePastMaxMessageBytes_AreMessageTooLargeAndDropTheBytesPastIt()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"], maxMessageBytes: 3);
        var delivered = store.CreatePendingMessage();
        delivered.Body.Write("ab"u8);
        delivered.Body.Write("cd"u8);
        var appended = store.CreatePendingMessage();
        appended.Body.Write("abcd"u8);

        Assert.AreEqual(MailStoreOutcome.MessageTooLarge, store.Deliver([MailStoreFixture.Recipient(store, "al@x")], delivered));
        Assert.AreEqual(MailStoreOutcome.MessageTooLarge, store.Append(store.ViewFor("al"), "INBOX", appended, MailFlags.None, null, out _));

        Assert.AreEqual(4, delivered.Length);
        Assert.IsNull(delivered.StorageFailure);
        Assert.IsEmpty(fileSystem.EnumerateDirectoryEntryNames(MessagesFolder));
    }

    [TestMethod]
    public async Task Deliver_PendingMessagePastTheStoresRoom_IsStoreFull()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"], maxTotalMessageBytes: 3);
        var pending = store.CreatePendingMessage();
        pending.Body.Write("abcd"u8);

        Assert.AreEqual(MailStoreOutcome.StoreFull, store.Deliver([MailStoreFixture.Recipient(store, "al@x")], pending));

        Assert.IsEmpty(fileSystem.EnumerateDirectoryEntryNames(MessagesFolder));
    }

    [TestMethod]
    public async Task Deliver_PendingMessageToNoRecipient_SucceedsStoringNothing()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"]);
        var pending = store.CreatePendingMessage();
        pending.Body.Write("nobody"u8);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([], pending));

        Assert.IsEmpty(fileSystem.EnumerateDirectoryEntryNames(MessagesFolder));
    }

    [TestMethod]
    public void DeliverAndAppend_NullArgument_Throws()
    {
        var store = MailStoreFixture.NewStore("al");
        using var pending = store.CreatePendingMessage();

        Assert.ThrowsExactly<ArgumentNullException>(() => store.Deliver(null!, pending));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.Deliver([], (PendingMessage)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.Append(store.ViewFor("al"), "INBOX", (PendingMessage)null!, MailFlags.None, null, out _));
    }

    [TestMethod]
    public void Deliver_PendingMessageStoredTwice_Throws()
    {
        var store = MailStoreFixture.NewStore("al");
        var pending = store.CreatePendingMessage();
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([MailStoreFixture.Recipient(store, "al@x")], pending));

        Assert.ThrowsExactly<InvalidOperationException>(() => store.Deliver([MailStoreFixture.Recipient(store, "al@x")], pending));
    }

    [TestMethod]
    public void Deliver_PendingMessageWithoutFiles_IsHeldInMemory()
    {
        var store = MailStoreFixture.NewStore("al");
        var pending = store.CreatePendingMessage();
        pending.Body.Write("in memory"u8);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([MailStoreFixture.Recipient(store, "al@x")], pending));

        Assert.AreEqual("in memory", MailStoreFixture.Fetch(store, store.ViewFor("al"), "INBOX", 1));
    }

    [TestMethod]
    public async Task FetchMessage_WithADataDirectory_ReadsTheBytesFromItsMessageFile()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"]);
        var pending = store.CreatePendingMessage();
        pending.Body.Write("original"u8);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([MailStoreFixture.Recipient(store, "al@x")], pending));

        WriteFile(fileSystem, MessagePath(0), "replaced"u8.ToArray());

        Assert.AreEqual("replaced", MailStoreFixture.Fetch(store, store.ViewFor("al"), "INBOX", 1));
    }

    [TestMethod]
    public async Task FetchMessage_LoadedStore_ReadsTheBytesFromItsMessageFile()
    {
        var fileSystem = NewFileSystem();
        var first = await LoadAsync(fileSystem, ["al"]);
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(first, "original", "al@x"));
        await first.SaveChangesAsync(TestContext.CancellationToken);
        var store = await LoadAsync(fileSystem, ["al"]);

        WriteFile(fileSystem, MessagePath(0), "replaced"u8.ToArray());

        Assert.AreEqual("replaced", MailStoreFixture.Fetch(store, store.ViewFor("al"), "INBOX", 1));
    }

    [TestMethod]
    public void FetchMessage_WithoutADataDirectory_HoldsTheBytesInMemory()
    {
        var store = MailStoreFixture.NewStore("al");
        var bytes = "original"u8.ToArray();
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([MailStoreFixture.Recipient(store, "al@x")], bytes));

        bytes[0] = (byte)'X';

        Assert.AreEqual("original", MailStoreFixture.Fetch(store, store.ViewFor("al"), "INBOX", 1));
    }

    [TestMethod]
    public async Task OpenMessage_MessageFileRemovedBehindTheStore_Throws()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"]);
        var pending = store.CreatePendingMessage();
        pending.Body.Write("gone"u8);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([MailStoreFixture.Recipient(store, "al@x")], pending));
        fileSystem.DeleteFile(MessagePath(0));

        Assert.Throws<IOException>(() => store.OpenMessage(store.ViewFor("al"), "INBOX", 1, out _));
    }

    [TestMethod]
    public async Task OpenMessage_MissingMessage_IsMessageMissingWithNoStream()
    {
        var store = await LoadAsync(NewFileSystem(), ["al"]);

        Assert.AreEqual(MailStoreOutcome.MessageMissing, store.OpenMessage(store.ViewFor("al"), "INBOX", 1, out var message));

        Assert.IsNull(message);
    }

    [TestMethod]
    public async Task LockMaildrop_MessageExpungedWhileLocked_KeepsItsFileUntilTheLockIsReleased()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"]);
        var al = store.ViewFor("al");
        var pending = store.CreatePendingMessage();
        pending.Body.Write("pinned"u8);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([MailStoreFixture.Recipient(store, "al@x")], pending));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(al, out var maildrop));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ChangeFlags(al, "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Expunge(al, "INBOX", out _));

        await store.SaveChangesAsync(TestContext.CancellationToken);

        Assert.IsTrue(Exists(fileSystem, MessagePath(0)));
        using (var stream = maildrop!.OpenMessage(1))
        {
            var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes, TestContext.CancellationToken);
            Assert.AreEqual("pinned", Encoding.ASCII.GetString(bytes.ToArray()));
        }

        maildrop.Dispose();
        await store.SaveChangesAsync(TestContext.CancellationToken);
        Assert.IsFalse(Exists(fileSystem, MessagePath(0)));
    }

    [TestMethod]
    public void Body_IsAWriteOnlyStreamThatSeeksNothing()
    {
        var store = MailStoreFixture.NewStore("al");
        using var pending = store.CreatePendingMessage();
        var body = pending.Body;

        Assert.IsFalse(body.CanRead);
        Assert.IsFalse(body.CanSeek);
        Assert.IsTrue(body.CanWrite);
        body.Flush();
        Assert.ThrowsExactly<NotSupportedException>(() => body.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => body.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => body.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => body.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => body.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => body.SetLength(0));
    }

    [TestMethod]
    public async Task Body_Disposed_ClosesItAndTheMessageCanStillBeDelivered()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"]);
        var pending = store.CreatePendingMessage();
        pending.Body.Write("closed"u8);

        pending.Body.Dispose();

        Assert.IsFalse(pending.Body.CanWrite);
        Assert.ThrowsExactly<ObjectDisposedException>(() => pending.Body.Write("x"u8));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await pending.Body.WriteAsync("x"u8.ToArray().AsMemory(), TestContext.CancellationToken));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([MailStoreFixture.Recipient(store, "al@x")], pending));
        Assert.AreEqual("closed", MailStoreFixture.Fetch(store, store.ViewFor("al"), "INBOX", 1));
    }
}
