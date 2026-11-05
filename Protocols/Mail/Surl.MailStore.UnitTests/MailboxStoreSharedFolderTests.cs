using static Surl.MailStore.MailStoreFixture;
using static Surl.MailStore.PersistedStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// <c>--shared-folder</c>: mailboxes of the reserved owner every member sees as
/// <c>Shared/</c>&lt;name&gt; (ADR-0102, decision 1.6).
/// </summary>
[TestClass]
public sealed class MailboxStoreSharedFolderTests
{
    private const string Team = "Shared/team";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ListMailboxes_SharedFolder_IsListedForEachMemberAndForNoOneElse()
    {
        var store = SharedStore();

        CollectionAssert.AreEqual(new[] { "INBOX", Team }, store.ListMailboxes(store.ViewFor("al")).ToArray());
        CollectionAssert.AreEqual(new[] { "INBOX", Team }, store.ListMailboxes(store.ViewFor("ann@example.com")).ToArray());
        CollectionAssert.AreEqual(new[] { "INBOX" }, store.ListMailboxes(store.ViewFor("cy")).ToArray());
        CollectionAssert.AreEqual(new[] { Team }, store.ViewFor("al").SharedFolders.ToArray());
        Assert.IsEmpty(store.ViewFor("cy").SharedFolders);
        Assert.AreEqual("al", store.ViewFor("al").OwnerName);
    }

    [TestMethod]
    public void Append_ByAMember_IsSeenByAnotherMemberWithItsFlags()
    {
        var store = SharedStore();

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(store.ViewFor("al"), Team, "hello"u8, MailFlags.Flagged, null, out var stored));

        var ann = store.ViewFor("ann@example.com");
        Assert.AreEqual("hello", Fetch(store, ann, Team, stored.Uid));
        Assert.AreEqual(MailFlags.Flagged, Read(store, ann, Team).Messages[0].Flags);
        Assert.IsEmpty(Uids(store, store.ViewFor("al"), "INBOX"));
    }

    [TestMethod]
    public void SharedFolder_NonMember_GetsTheMissingMailboxAnswers()
    {
        var store = SharedStore();
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(store.ViewFor("al"), Team, "x"u8, MailFlags.None, null, out _));
        var cy = store.ViewFor("cy");

        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.ReadMailbox(cy, Team, out _));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.Append(cy, Team, "x"u8, MailFlags.None, null, out _));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.Copy(cy, "INBOX", [1], Team, out _));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.DeleteMailbox(cy, Team));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.DeleteMailbox(cy, "Shared"));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.RenameMailbox(cy, Team, "Mine"));
    }

    [TestMethod]
    public void SharedFolder_EveryMemberMayDeleteItsMessages()
    {
        var store = SharedStore();
        var al = store.ViewFor("al");
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(al, Team, "x"u8, MailFlags.None, null, out _));
        var ann = store.ViewFor("ann@example.com");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ChangeFlags(ann, Team, 1, MailFlagChange.Add, MailFlags.Deleted, out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Expunge(ann, Team, out var expunged));

        CollectionAssert.AreEqual(new uint[] { 1 }, expunged.ToArray());
        Assert.IsEmpty(Uids(store, al, Team));
    }

    [TestMethod]
    public void SharedFolder_NoMemberMayDeleteRenameOrCreateUnderIt()
    {
        var store = SharedStore();
        var al = store.ViewFor("al");

        Assert.AreEqual(MailStoreOutcome.SharedFolderProtected, store.DeleteMailbox(al, Team));
        Assert.AreEqual(MailStoreOutcome.SharedFolderProtected, store.DeleteMailbox(al, "Shared"));
        Assert.AreEqual(MailStoreOutcome.SharedFolderProtected, store.RenameMailbox(al, Team, "Mine"));
        Assert.AreEqual(MailStoreOutcome.SharedFolderProtected, store.RenameMailbox(al, "INBOX", "Shared/mine"));
        Assert.AreEqual(MailStoreOutcome.SharedFolderProtected, store.CreateMailbox(al, "Shared/team/sub"));
        Assert.AreEqual(MailStoreOutcome.SharedFolderProtected, store.CreateMailbox(store.ViewFor("cy"), "Shared"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(al, "Sharedness"));
        CollectionAssert.AreEqual(new[] { "INBOX", Team, "Sharedness" }, store.ListMailboxes(al).ToArray());
    }

    [TestMethod]
    public void SharedFolder_CountsAgainstNoMembersQuota_AndAMoveOutOfItCountsAgainstTheDestinationOwner()
    {
        var store = new MailboxStore(
            [],
            allowAnonymous: false,
            new SettableTimeProvider(),
            mailDomains: new MailDomainConfiguration(
                ["example.com"], ["ann@example.com"], [], [], [new MailboxSettings("ann@example.com", 3, [])], [new MailSharedFolder("team", ["ann@example.com"])]));
        var ann = store.ViewFor("ann@example.com");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(ann, Team, "12345"u8, MailFlags.None, null, out _));
        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, store.Move(ann, Team, [1], "INBOX", out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(ann, "INBOX", "123"u8, MailFlags.None, null, out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Copy(ann, "INBOX", [1], Team, out _));
        Assert.HasCount(2, Uids(store, ann, Team));
    }

    [TestMethod]
    public async Task LoadAsync_SharedFolders_PersistUnderTheReservedOwnerAndAreNotCreatedAgain()
    {
        var fileSystem = NewFileSystem();
        var first = await LoadSharedStoreAsync(fileSystem, SharedConfiguration());
        Assert.AreEqual(MailStoreOutcome.Succeeded, first.Append(first.ViewFor("al"), Team, "kept"u8, MailFlags.None, null, out _));
        await first.SaveChangesAsync(TestContext.CancellationToken);

        var store = await LoadSharedStoreAsync(fileSystem, SharedConfiguration());

        Assert.AreEqual(0, store.ChangeCount);
        Assert.AreEqual("kept", Fetch(store, store.ViewFor("ann@example.com"), Team, 1));
        var index = MailStoreIndex.Decode(ReadFile(fileSystem, IndexPath), MailboxStore.DefaultMaxMessages, MailboxStore.DefaultMaxTotalMessageBytes, MailboxStore.DefaultMaxMailboxes);
        var shared = index.Owners.Single(owner => owner.Name == MailboxStore.SharedFolderOwnerName);
        CollectionAssert.AreEqual(new[] { "team" }, shared.Mailboxes.Keys.ToArray());
    }

    [TestMethod]
    public async Task LoadAsync_PersonalMailboxUnderShared_IsKeptUnreachedWhileSharedFoldersAreConfigured()
    {
        var fileSystem = NewFileSystem();
        var first = await LoadSharedStoreAsync(fileSystem, null);
        var before = first.ViewFor("al");
        Assert.AreEqual(MailStoreOutcome.Succeeded, first.CreateMailbox(before, "Shared/old"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, first.CreateMailbox(before, "Shared"));
        await first.SaveChangesAsync(TestContext.CancellationToken);

        var store = await LoadSharedStoreAsync(fileSystem, SharedConfiguration());
        var al = store.ViewFor("al");

        CollectionAssert.AreEqual(new[] { "INBOX", Team }, store.ListMailboxes(al).ToArray());
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.ReadMailbox(al, "Shared/old", out _));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.ReadMailbox(store.ViewFor("cy"), "Shared/old", out _));
        await store.SaveChangesAsync(TestContext.CancellationToken);

        var without = await LoadSharedStoreAsync(fileSystem, null);
        CollectionAssert.AreEqual(new[] { "INBOX", "Shared", "Shared/old" }, without.ListMailboxes(without.ViewFor("al")).ToArray());
    }

    [TestMethod]
    public async Task LoadAsync_IndexWrittenBeforeSharedFoldersAndQuotas_LoadsUnchanged()
    {
        var fileSystem = NewFileSystem();
        var first = await LoadSharedStoreAsync(fileSystem, null);
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(first, "old mail", "<al@localhost>"));
        await first.SaveChangesAsync(TestContext.CancellationToken);
        var written = ReadFile(fileSystem, IndexPath);

        var store = await LoadSharedStoreAsync(fileSystem, new MailDomainConfiguration([], [], [], []));
        await store.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, store.ChangeCount);
        Assert.AreEqual("old mail", Fetch(store, store.ViewFor("al"), "INBOX", 1));
        CollectionAssert.AreEqual(written, ReadFile(fileSystem, IndexPath));
        Assert.IsEmpty(store.ViewFor("al").SharedFolders);
    }

    [TestMethod]
    public void SharedFolders_UnderAllowAnonymous_AreNotReached()
    {
        var store = new MailboxStore(["al"], allowAnonymous: true, new SettableTimeProvider(), mailDomains: SharedConfiguration());
        var view = store.ViewFor("al");

        CollectionAssert.AreEqual(new[] { "INBOX" }, store.ListMailboxes(view).ToArray());
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(view, Team));
    }

    [TestMethod]
    public void ViewFor_NoAccountName_IsEmptyAndSeesNoSharedFolder()
    {
        var store = SharedStore();
        var view = store.ViewFor(null);

        Assert.IsNull(view.OwnerName);
        Assert.IsEmpty(store.ListMailboxes(view));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.ReadMailbox(view, Team, out _));
    }

    [TestMethod]
    public void Constructor_AccountNamedLikeTheReservedOwner_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MailboxStore([MailboxStore.SharedFolderOwnerName], allowAnonymous: false, new SettableTimeProvider()));
    }

    [TestMethod]
    public void Constructor_SharedFoldersPastMaxMailboxes_Throws()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => new MailboxStore(
            ["al"], allowAnonymous: false, new SettableTimeProvider(), maxMailboxes: 0, mailDomains: SharedConfiguration()));
    }

    private static MailboxStore SharedStore() =>
        new(["al", "cy"], allowAnonymous: false, new SettableTimeProvider(), mailDomains: SharedConfiguration());

    private Task<MailboxStore> LoadSharedStoreAsync(Content.IContentFileSystem fileSystem, MailDomainConfiguration? mailDomains) =>
        MailboxStore.LoadAsync(
            new MailStoreFiles(fileSystem, StateFolder),
            ["al", "cy"],
            allowAnonymous: false,
            new SettableTimeProvider(),
            mailDomains: mailDomains,
            cancellationToken: TestContext.CancellationToken);

    private static MailDomainConfiguration SharedConfiguration() =>
        new(["example.com"], ["ann@example.com"], [], [], null, [new MailSharedFolder("team", ["al", "ann@EXAMPLE.com", "nobody"])]);
}
