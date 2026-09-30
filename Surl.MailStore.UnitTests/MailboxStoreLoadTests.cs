using System.Text;
using Surl.Content;
using static Surl.MailStore.PersistedStoreFixture;
using static Surl.MailStore.UnitTestIndexBytes;

namespace Surl.MailStore;

/// <summary>
/// Loading the persisted store (ADR-0050, decision 7): what is loaded, and every malformed
/// store the ADR lists refused with its file and reason.
/// </summary>
[TestClass]
public sealed class MailboxStoreLoadTests
{
    private const uint LastUidValidity = 100;

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> MalformedIndexes =>
    [
        ["wrong header", Join("SURL-MAIL-INDEX-2\n"u8.ToArray(), Valid()[Header.Length..])],
        ["truncated", Valid()[..^1]],
        ["truncated header", Header[..5]],
        ["trailing bytes", Join(Valid(), [0])],
        ["owner name not UTF-8", Index(1, LastUidValidity, Owner([0xFF], Inbox()))],
        ["owner name with a control character", Index(1, LastUidValidity, Owner("a\u0001", Inbox()))],
        ["owner repeated", Index(1, LastUidValidity, Owner("al", Inbox()), Owner("al", Mailbox("Sent", 99, 1)))],
        ["mailbox name not UTF-8", Index(1, LastUidValidity, Owner("al", Mailbox([0xC3], 100, 1)))],
        ["mailbox name with a control character", Index(1, LastUidValidity, Owner("al", Mailbox("a\u007F", 100, 1)))],
        ["mailbox repeated as INBOX in another case", Index(1, LastUidValidity, Owner("al", Inbox(), Mailbox("inbox", 99, 1)))],
        ["mailbox name empty", Index(1, LastUidValidity, Owner("al", Mailbox("", 100, 1)))],
        ["mailbox name past 1024 bytes", Index(1, LastUidValidity, Owner("al", Mailbox(new string('m', 1025), 100, 1)))],
        ["UIDVALIDITY 0", Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 0, 1)))],
        ["UIDVALIDITY above the last given", Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 101, 1)))],
        ["UIDVALIDITY repeated within an owner", Index(1, LastUidValidity, Owner("al", Inbox(), Mailbox("Sent", 100, 1)))],
        ["next UID 0", Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 100, 0)))],
        ["UID 0", Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 100, 2, Message(0, 0, 0, 0, 3, 0))))],
        ["UIDs not ascending", Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 100, 3, Message(2, 0, 0, 0, 3, 0), Message(1, 0, 0, 0, 3, 0))))],
        ["UID not below the next UID", Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 100, 2, Message(2, 0, 0, 0, 3, 0))))],
        ["flag bit above bit 4", Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 100, 2, Message(1, 0x20, 0, 0, 3, 0))))],
        ["offset above 1439 minutes", Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 100, 2, Message(1, 0, 0, 1440, 3, 0))))],
        ["offset below -1439 minutes", Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 100, 2, Message(1, 0, 0, -1440, 3, 0))))],
        ["internal date past year 9999", Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 100, 2, Message(1, 0, 253402300800, 0, 3, 0))))],
        ["internal date past year 9999 in its offset", Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 100, 2, Message(1, 0, 253402300799, 1439, 3, 0))))],
        ["message file number not below the next", Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 100, 2, Message(1, 0, 0, 0, 3, 1))))],
        ["message file given two sizes", Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 100, 3, Message(1, 0, 0, 0, 3, 0), Message(2, 0, 0, 0, 4, 0))))],
        ["message size past a byte array", Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 100, 2, Message(1, 0, 0, 0, (ulong)int.MaxValue + 1, 0))))],
    ];

    [TestMethod]
    public async Task LoadAsync_NullFiles_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => MailboxStore.LoadAsync(null!, [], false, new SettableTimeProvider(), cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadAsync_NullAccountNames_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => MailboxStore.LoadAsync(new MailStoreFiles(NewFileSystem(), StateFolder), null!, false, new SettableTimeProvider(), cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadAsync_NullTimeProvider_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => MailboxStore.LoadAsync(new MailStoreFiles(NewFileSystem(), StateFolder), [], false, null!, cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public void MailStoreFiles_NullFileSystem_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailStoreFiles(null!, StateFolder));
    }

    [TestMethod]
    public void MailStoreFiles_EmptyStateFolder_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MailStoreFiles(NewFileSystem(), ""));
    }

    [TestMethod]
    public void MailStoreFiles_NamesTheIndexAndMessageFilesInTheStateFolder()
    {
        var files = new MailStoreFiles(NewFileSystem(), StateFolder);

        Assert.AreEqual(StateFolder, files.StateFolderPath);
        Assert.AreEqual(IndexPath, files.IndexPath);
        Assert.AreEqual(MessagesFolder, files.MessagesFolderPath);
        Assert.AreEqual(Path.Join(MessagesFolder, "00000000000000ab"), files.MessageFilePath(0xAB));
    }

    [TestMethod]
    public async Task LoadAsync_MissingIndex_IsAnEmptyStoreWithAnInboxPerAccountAndWritesNothingUntilSaved()
    {
        var fileSystem = NewFileSystem();
        PersistedStoreFixture.WriteFile(fileSystem, MessagePath(0), "stray"u8.ToArray());

        var store = await LoadAsync(fileSystem, ["al", "bo"]);

        Assert.AreEqual(2, store.ChangeCount);
        Assert.IsEmpty(MailStoreFixture.Read(store, store.ViewFor("al"), "INBOX").Messages);
        Assert.IsFalse(Exists(fileSystem, IndexPath));
        await store.SaveChangesAsync(TestContext.CancellationToken);
        Assert.IsTrue(Exists(fileSystem, IndexPath));
    }

    [TestMethod]
    public async Task LoadAsync_MissingIndexAndNoAccounts_IsAnEmptyStoreThatWritesNothing()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, []);

        await store.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, store.ChangeCount);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(StateFolder));
    }

    [TestMethod]
    public async Task LoadAsync_ValidIndex_LoadsItsMessageAndChangesNothing()
    {
        var fileSystem = WithValidStore(Valid());

        var store = await LoadAsync(fileSystem, ["al"]);

        Assert.AreEqual(0, store.ChangeCount);
        var inbox = MailStoreFixture.Read(store, store.ViewFor("al"), "INBOX");
        Assert.AreEqual(LastUidValidity, inbox.UidValidity);
        Assert.AreEqual(2u, inbox.NextUid);
        Assert.AreEqual(new MailMessageSummary(1, MailFlags.Seen, DateTimeOffset.FromUnixTimeSeconds(1000).ToOffset(TimeSpan.FromMinutes(-300)), 3), inbox.Messages.Single());
        Assert.AreEqual("abc", MailStoreFixture.Fetch(store, store.ViewFor("al"), "INBOX", 1));
    }

    [TestMethod]
    public async Task LoadAsync_InboxInLowerCase_IsLoadedAsInbox()
    {
        var fileSystem = WithValidStore(Index(1, LastUidValidity, Owner("al", Mailbox("inbox", 100, 1))));

        var store = await LoadAsync(fileSystem, ["al"]);

        Assert.AreEqual(0, store.ChangeCount);
        CollectionAssert.AreEqual(new[] { "INBOX" }, store.ListMailboxes(store.ViewFor("al")).ToArray());
    }

    [TestMethod]
    public async Task LoadAsync_AccountWhoseOwnerLacksAnInbox_GetsOneAsAChange()
    {
        var fileSystem = WithValidStore(Index(0, LastUidValidity, Owner("al", Mailbox("Sent", 100, 1))));

        var store = await LoadAsync(fileSystem, ["al"]);

        Assert.AreEqual(1, store.ChangeCount);
        var inbox = MailStoreFixture.Read(store, store.ViewFor("al"), "INBOX");
        Assert.IsGreaterThan(LastUidValidity, inbox.UidValidity);
        CollectionAssert.AreEqual(new[] { "INBOX", "Sent" }, store.ListMailboxes(store.ViewFor("al")).ToArray());
    }

    [TestMethod]
    public async Task LoadAsync_LeftoverTemporaryAndUnnamedFiles_AreIgnored()
    {
        var fileSystem = WithValidStore(Valid());
        PersistedStoreFixture.WriteFile(fileSystem, Path.Join(StateFolder, ".index-0123456789abcdef0123456789abcdef"), "junk"u8.ToArray());
        PersistedStoreFixture.WriteFile(fileSystem, Path.Join(MessagesFolder, ".pending-0123456789abcdef0123456789abcdef"), "junk"u8.ToArray());
        PersistedStoreFixture.WriteFile(fileSystem, MessagePath(7), "unnamed"u8.ToArray());

        var store = await LoadAsync(fileSystem, ["al"]);

        Assert.AreEqual("abc", MailStoreFixture.Fetch(store, store.ViewFor("al"), "INBOX", 1));
        Assert.HasCount(1, MailStoreFixture.Read(store, store.ViewFor("al"), "INBOX").Messages);
    }

    [TestMethod]
    [DynamicData(nameof(MalformedIndexes))]
    public async Task LoadAsync_MalformedIndex_IsRefusedAsNotAMailStoreIndex(string malformation, byte[] index)
    {
        var fileSystem = WithValidStore(index);

        var refusal = await Assert.ThrowsExactlyAsync<MailStoreLoadException>(() => LoadAsync(fileSystem, ["al"]), malformation);

        Assert.AreEqual(IndexPath, refusal.FilePath);
        Assert.AreEqual("not a mail store index", refusal.Message);
        Assert.IsInstanceOfType<InvalidDataException>(refusal.InnerException);
    }

    [TestMethod]
    [DataRow(1, MailboxStore.DefaultMaxTotalMessageBytes, MailboxStore.DefaultMaxMailboxes, DisplayName = "more messages than the bound")]
    [DataRow(MailboxStore.DefaultMaxMessages, 5L, MailboxStore.DefaultMaxMailboxes, DisplayName = "more bytes than the bound")]
    [DataRow(MailboxStore.DefaultMaxMessages, MailboxStore.DefaultMaxTotalMessageBytes, 0, DisplayName = "more mailboxes than the bound")]
    public async Task LoadAsync_IndexPastABound_IsRefusedAsNotAMailStoreIndex(int maxMessages, long maxTotalMessageBytes, int maxMailboxes)
    {
        var fileSystem = WithValidStore(Index(
            2,
            LastUidValidity,
            Owner("al", Mailbox("INBOX", 99, 2, Message(1, 0, 0, 0, 3, 0)), Mailbox("Sent", 100, 2, Message(1, 0, 0, 0, 3, 1)))));
        PersistedStoreFixture.WriteFile(fileSystem, MessagePath(1), "def"u8.ToArray());

        var refusal = await Assert.ThrowsExactlyAsync<MailStoreLoadException>(
            () => LoadAsync(fileSystem, ["al"], maxMessages: maxMessages, maxTotalMessageBytes: maxTotalMessageBytes, maxMailboxes: maxMailboxes));

        Assert.AreEqual(IndexPath, refusal.FilePath);
        Assert.AreEqual("not a mail store index", refusal.Message);
    }

    [TestMethod]
    public async Task LoadAsync_IndexAtEveryBound_Loads()
    {
        var fileSystem = WithValidStore(Index(
            2,
            LastUidValidity,
            Owner("al", Mailbox("INBOX", 99, 2, Message(1, 0, 0, 0, 3, 0)), Mailbox("Sent", 100, 2, Message(1, 0, 0, 0, 3, 1)))));
        PersistedStoreFixture.WriteFile(fileSystem, MessagePath(1), "def"u8.ToArray());

        var store = await LoadAsync(fileSystem, ["al"], maxMessages: 2, maxTotalMessageBytes: 6, maxMailboxes: 1);

        Assert.AreEqual("def", MailStoreFixture.Fetch(store, store.ViewFor("al"), "Sent", 1));
    }

    [TestMethod]
    public async Task LoadAsync_MessageFileMissing_IsRefusedNamingIt()
    {
        var fileSystem = NewFileSystem();
        PersistedStoreFixture.WriteFile(fileSystem, IndexPath, Valid());

        var refusal = await Assert.ThrowsExactlyAsync<MailStoreLoadException>(() => LoadAsync(fileSystem, ["al"]));

        Assert.AreEqual(MessagePath(0), refusal.FilePath);
        Assert.AreEqual("missing or not the size the index gives", refusal.Message);
    }

    [TestMethod]
    public async Task LoadAsync_MessageFileOfAnotherSize_IsRefusedNamingIt()
    {
        var fileSystem = NewFileSystem();
        PersistedStoreFixture.WriteFile(fileSystem, IndexPath, Valid());
        PersistedStoreFixture.WriteFile(fileSystem, MessagePath(0), "abcd"u8.ToArray());

        var refusal = await Assert.ThrowsExactlyAsync<MailStoreLoadException>(() => LoadAsync(fileSystem, ["al"]));

        Assert.AreEqual(MessagePath(0), refusal.FilePath);
        Assert.AreEqual("missing or not the size the index gives", refusal.Message);
    }

    [TestMethod]
    public async Task LoadAsync_MessageFileADirectory_IsRefusedNamingIt()
    {
        var fileSystem = NewFileSystem();
        PersistedStoreFixture.WriteFile(fileSystem, IndexPath, Valid());
        fileSystem.CreateDirectory(MessagePath(0));

        var refusal = await Assert.ThrowsExactlyAsync<MailStoreLoadException>(() => LoadAsync(fileSystem, ["al"]));

        Assert.AreEqual(MessagePath(0), refusal.FilePath);
    }

    [TestMethod]
    public async Task LoadAsync_IndexUnreadableAsIOException_IsRefusedWithTheExceptionMessage()
    {
        await AssertUnreadableAsync(new IOException("The disk failed."), "index", IndexPath);
    }

    [TestMethod]
    public async Task LoadAsync_IndexUnreadableAsAccessDenied_IsRefusedWithTheExceptionMessage()
    {
        await AssertUnreadableAsync(new UnauthorizedAccessException("Access denied."), "index", IndexPath);
    }

    [TestMethod]
    public async Task LoadAsync_MessageFileUnreadable_IsRefusedWithTheExceptionMessage()
    {
        await AssertUnreadableAsync(new IOException("The disk failed."), "messages", MessagePath(0));
    }

    [TestMethod]
    public async Task LoadAsync_IndexReadFailingOtherwise_IsNotCaught()
    {
        var fileSystem = new UnitTestFaultingContentFileSystem { FailReadsOf = "index", Failure = new InvalidOperationException("Other.") };
        PersistedStoreFixture.WriteFile(fileSystem.Files, IndexPath, Valid());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => LoadAsync(fileSystem, ["al"]));
    }

    [TestMethod]
    public async Task LoadAsync_MessageFileReadFailingOtherwise_IsNotCaught()
    {
        var fileSystem = new UnitTestFaultingContentFileSystem { FailReadsOf = "messages", Failure = new InvalidOperationException("Other.") };
        PersistedStoreFixture.WriteFile(fileSystem.Files, IndexPath, Valid());
        PersistedStoreFixture.WriteFile(fileSystem.Files, MessagePath(0), "abc"u8.ToArray());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => LoadAsync(fileSystem, ["al"]));
    }

    private static async Task AssertUnreadableAsync(Exception failure, string failingFragment, string expectedPath)
    {
        var fileSystem = new UnitTestFaultingContentFileSystem { FailReadsOf = failingFragment, Failure = failure };
        PersistedStoreFixture.WriteFile(fileSystem.Files, IndexPath, Valid());
        PersistedStoreFixture.WriteFile(fileSystem.Files, MessagePath(0), "abc"u8.ToArray());

        var refusal = await Assert.ThrowsExactlyAsync<MailStoreLoadException>(() => LoadAsync(fileSystem, ["al"]));

        Assert.AreEqual(expectedPath, refusal.FilePath);
        Assert.AreEqual(failure.Message, refusal.Message);
        Assert.AreSame(failure, refusal.InnerException);
    }

    /// <summary>
    /// A well-formed index: owner <c>al</c>, whose <c>INBOX</c> holds one 3-byte message in file 0.
    /// </summary>
    private static byte[] Valid() =>
        Index(1, LastUidValidity, Owner("al", Mailbox("INBOX", 100, 2, Message(1, (byte)MailFlags.Seen, 1000, -300, 3, 0))));

    private static byte[] Inbox() => Mailbox("INBOX", 100, 1);

    private static InMemoryContentFileSystem WithValidStore(byte[] index)
    {
        var fileSystem = NewFileSystem();
        PersistedStoreFixture.WriteFile(fileSystem, IndexPath, index);
        PersistedStoreFixture.WriteFile(fileSystem, MessagePath(0), Encoding.ASCII.GetBytes("abc"));
        return fileSystem;
    }
}
