using static Surl.MailStore.PersistedStoreFixture;
using static Surl.MailStore.UnitTestIndexBytes;

namespace Surl.MailStore;

/// <summary>
/// RFC 6154 folder uses kept across a restart of a persisted store (ADR-0192).
/// </summary>
[TestClass]
public sealed class MailboxStoreFolderUsePersistenceTests
{
    private const string Ann = "ann@example.com";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LoadAsync_AfterASave_HoldsTheConfiguredUseAndTheUseGivenToCreateMailbox()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadWithUsesAsync(fileSystem, new() { ["Sent"] = "sent" });
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(store.ViewFor(Ann), "Bin", use: "trash"));
        await store.SaveChangesAsync(TestContext.CancellationToken);

        var loaded = await LoadWithUsesAsync(fileSystem, []);

        AssertUses(loaded, ("Sent", @"\Sent"), ("Bin", @"\Trash"));
        Assert.IsTrue(loaded.HoldsAnyFolderUse);
        Assert.AreEqual(MailStoreOutcome.FolderUseTaken, loaded.CreateMailbox(loaded.ViewFor(Ann), "Other", use: "trash"));
    }

    [TestMethod]
    public async Task LoadAsync_IndexWrittenBeforeUsesWereKept_GivesItsMailboxesWithNoUse()
    {
        var fileSystem = NewFileSystem();
        WriteFile(fileSystem, IndexPath, Index(0, 101, Owner(Ann, Mailbox("INBOX", 100, 1), Mailbox("Sent", 101, 4))));

        var loaded = await LoadWithUsesAsync(fileSystem, []);

        var sent = MailStoreFixture.Read(loaded, loaded.ViewFor(Ann), "Sent");
        Assert.AreEqual(101u, sent.UidValidity);
        Assert.AreEqual(4u, sent.NextUid);
        Assert.IsNull(sent.Use);
        Assert.IsFalse(loaded.HoldsAnyFolderUse);
        Assert.AreEqual(0, loaded.ChangeCount);
    }

    [TestMethod]
    public async Task LoadAsync_ConfiguredUseOfALoadedMailboxWithNone_IsGivenToItAndSaved()
    {
        var fileSystem = NewFileSystem();
        WriteFile(fileSystem, IndexPath, Index(0, 101, Owner(Ann, Mailbox("INBOX", 100, 1), Mailbox("Sent", 101, 4))));

        var configured = await LoadWithUsesAsync(fileSystem, new() { ["Sent"] = "sent" });
        Assert.AreEqual(1, configured.ChangeCount);
        await configured.SaveChangesAsync(TestContext.CancellationToken);
        var loaded = await LoadWithUsesAsync(fileSystem, []);

        AssertUses(loaded, ("Sent", @"\Sent"));
        Assert.AreEqual(101u, MailStoreFixture.Read(loaded, loaded.ViewFor(Ann), "Sent").UidValidity);
    }

    [TestMethod]
    public async Task LoadAsync_ConfiguredUseTheLoadedMailboxAlreadyHolds_IsNoChange()
    {
        var fileSystem = await SavedStoreAsync(("Sent", "sent"));

        var loaded = await LoadWithUsesAsync(fileSystem, new() { ["Sent"] = "sent" });

        AssertUses(loaded, ("Sent", @"\Sent"));
        Assert.AreEqual(0, loaded.ChangeCount);
    }

    [TestMethod]
    public async Task LoadAsync_ConfiguredUseOfALoadedMailboxWithAnotherUse_ReplacesIt()
    {
        var fileSystem = await SavedStoreAsync(("Sent", "archive"));

        var loaded = await LoadWithUsesAsync(fileSystem, new() { ["Sent"] = "sent" });

        AssertUses(loaded, ("Sent", @"\Sent"));
        Assert.AreEqual(1, loaded.ChangeCount);
    }

    [TestMethod]
    public async Task LoadAsync_ConfiguredUseHeldByAnotherLoadedMailbox_MovesToTheConfiguredOne()
    {
        var fileSystem = await SavedStoreAsync(("Old", "sent"), ("Sent", null));

        var loaded = await LoadWithUsesAsync(fileSystem, new() { ["Sent"] = "sent" });

        AssertUses(loaded, ("Sent", @"\Sent"));
        Assert.IsNull(MailStoreFixture.Read(loaded, loaded.ViewFor(Ann), "Old").Use);
    }

    [TestMethod]
    public async Task LoadAsync_ConfiguredUseOfAMissingMailbox_HeldByAnother_MovesToTheCreatedOne()
    {
        var fileSystem = await SavedStoreAsync(("Old", "sent"));

        var loaded = await LoadWithUsesAsync(fileSystem, new() { ["Sent"] = "sent" });
        Assert.AreEqual(1, loaded.ChangeCount);
        await loaded.SaveChangesAsync(TestContext.CancellationToken);
        var reloaded = await LoadWithUsesAsync(fileSystem, []);

        AssertUses(reloaded, ("Sent", @"\Sent"));
    }

    [TestMethod]
    public async Task LoadAsync_TwoUsesSwappedByConfiguration_BothApply()
    {
        var fileSystem = await SavedStoreAsync(("A", "drafts"), ("B", "sent"));

        var loaded = await LoadWithUsesAsync(fileSystem, new() { ["A"] = "sent", ["B"] = "drafts" });

        AssertUses(loaded, ("A", @"\Sent"), ("B", @"\Drafts"));
    }

    [TestMethod]
    public async Task LoadAsync_FolderConfiguredWithoutAUse_KeepsTheLoadedUse()
    {
        var fileSystem = await SavedStoreAsync(("Sent", "sent"));

        var loaded = await LoadWithUsesAsync(fileSystem, [], folders: ["Sent"]);

        AssertUses(loaded, ("Sent", @"\Sent"));
        Assert.AreEqual(0, loaded.ChangeCount);
    }

    [TestMethod]
    public async Task LoadAsync_AnonymousStore_KeepsTheLoadedUse()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, [], allowAnonymous: true);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(store.ViewFor("anyone"), "Junk", use: "junk"));
        await store.SaveChangesAsync(TestContext.CancellationToken);

        var loaded = await LoadAsync(fileSystem, [], allowAnonymous: true);

        Assert.AreEqual(@"\Junk", MailStoreFixture.Read(loaded, loaded.ViewFor("anyone"), "Junk").Use);
    }

    private async Task<Surl.Content.InMemoryContentFileSystem> SavedStoreAsync(params (string Name, string? Use)[] mailboxes)
    {
        var fileSystem = NewFileSystem();
        var store = await LoadWithUsesAsync(fileSystem, []);
        foreach (var (name, use) in mailboxes)
        {
            Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(store.ViewFor(Ann), name, use: use));
        }

        await store.SaveChangesAsync(TestContext.CancellationToken);
        return fileSystem;
    }

    private static Task<MailboxStore> LoadWithUsesAsync(
        Surl.Content.IContentFileSystem fileSystem,
        Dictionary<string, string> folderUses,
        string[]? folders = null) =>
        MailboxStore.LoadAsync(
            new MailStoreFiles(fileSystem, StateFolder),
            [],
            allowAnonymous: false,
            new SettableTimeProvider(),
            mailDomains: new MailDomainConfiguration(["example.com"], [Ann], [], [], [new MailboxSettings(Ann, null, folders ?? [], folderUses)]));

    private static void AssertUses(MailboxStore store, params (string Name, string Use)[] expected) =>
        CollectionAssert.AreEquivalent(
            expected.ToDictionary(pair => pair.Name, pair => pair.Use),
            store.ReadMailboxUses(store.ViewFor(Ann)).ToDictionary(pair => pair.Key, pair => pair.Value));
}
