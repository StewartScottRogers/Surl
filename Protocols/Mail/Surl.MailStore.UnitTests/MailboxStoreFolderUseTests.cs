namespace Surl.MailStore;

/// <summary>
/// RFC 6154 folder uses held by the store (ADR-0055, Amendment 1, decision 6).
/// </summary>
[TestClass]
public sealed class MailboxStoreFolderUseTests
{
    private const string Ann = "ann@example.com";

    [TestMethod]
    public void Canonical_AcceptsEachUseWithOrWithoutBackslashInAnyCase_AndRefusesOthers()
    {
        Assert.AreEqual(@"\Sent", MailFolderUse.Canonical("sent"));
        Assert.AreEqual(@"\Trash", MailFolderUse.Canonical(@"\TRASH"));
        Assert.AreEqual(7, MailFolderUse.All.Count);
        Assert.IsNull(MailFolderUse.Canonical("Important"));
        Assert.IsNull(MailFolderUse.Canonical(null));
    }

    [TestMethod]
    public void DefaultSettings_HaveNoUses()
    {
        var store = NewStore(new Dictionary<string, string>());

        Assert.AreEqual(0, store.ReadMailboxUses(store.ViewFor(Ann)).Count);
        Assert.IsNull(Read(store, "INBOX").Use);
    }

    [TestMethod]
    public void ConfiguredFolder_IsCreatedWithItsUse_AndReadable()
    {
        var store = NewStore(new Dictionary<string, string> { ["Sent"] = "sent", ["Old"] = @"\Archive" });
        var view = store.ViewFor(Ann);

        Assert.AreEqual(@"\Sent", Read(store, "Sent").Use);
        CollectionAssert.AreEquivalent(
            new Dictionary<string, string> { ["Sent"] = @"\Sent", ["Old"] = @"\Archive" },
            store.ReadMailboxUses(view).ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    [TestMethod]
    public void ConfiguredFolder_AlsoListedInFolders_TakesTheUse()
    {
        var store = NewStore(new Dictionary<string, string> { ["Sent"] = "sent" }, folders: ["Sent"]);

        Assert.AreEqual(@"\Sent", Read(store, "Sent").Use);
    }

    [TestMethod]
    public void Configuration_RefusesUnknownUseInboxAndRepeatedUse()
    {
        Assert.ThrowsExactly<ArgumentException>(() => NewStore(new Dictionary<string, string> { ["A"] = "nonsense" }));
        Assert.ThrowsExactly<ArgumentException>(() => NewStore(new Dictionary<string, string> { ["inbox"] = "sent" }));
        Assert.ThrowsExactly<ArgumentException>(() => NewStore(new Dictionary<string, string> { ["A"] = "sent", ["B"] = @"\Sent" }));
    }

    [TestMethod]
    public void CreateMailbox_WithUse_HoldsIt_AndRefusesASecondFolderWithTheSameUse()
    {
        var store = NewStore(null);
        var view = store.ViewFor(Ann);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(view, "Out", use: "sent"));
        Assert.AreEqual(@"\Sent", Read(store, "Out").Use);
        Assert.AreEqual(MailStoreOutcome.FolderUseTaken, store.CreateMailbox(view, "Out2", use: @"\SENT"));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.ReadMailbox(view, "Out2", out _));
    }

    [TestMethod]
    public void CreateMailbox_WithUseOutsideRfc6154_IsRefused_AndWithoutUseStaysPlain()
    {
        var store = NewStore(null);
        var view = store.ViewFor(Ann);

        Assert.AreEqual(MailStoreOutcome.FolderUseUnknown, store.CreateMailbox(view, "X", use: "Important"));
        Assert.AreEqual(MailStoreOutcome.AlreadyExists, store.CreateMailbox(view, "INBOX", use: "sent"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(view, "Plain"));
        Assert.IsNull(Read(store, "Plain").Use);
    }

    [TestMethod]
    public void Reset_RecreatesConfiguredUses_AndDropsAUseGainedAtRunTimeWithItsMailbox()
    {
        var store = NewStore(new Dictionary<string, string> { ["Sent"] = "sent" });
        var view = store.ViewFor(Ann);
        store.CreateMailbox(view, "Mine", use: "trash");
        store.DeleteMailbox(view, "Sent");

        store.Reset();

        Assert.AreEqual(@"\Sent", Read(store, "Sent").Use);
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.ReadMailbox(view, "Mine", out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(view, "Mine", use: "trash"));
    }

    [TestMethod]
    public void SnapshotAndRestore_CopyEachUse()
    {
        var store = NewStore(null);
        var view = store.ViewFor(Ann);
        store.CreateMailbox(view, "Out", use: "sent");
        var snapshot = store.TakeSnapshot();
        store.DeleteMailbox(view, "Out");
        store.CreateMailbox(view, "Other", use: "sent");

        store.Restore(snapshot);

        Assert.AreEqual(@"\Sent", Read(store, "Out").Use);
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.ReadMailbox(view, "Other", out _));
    }

    [TestMethod]
    public void RenameMailbox_CarriesTheUse_AndDeleteEndsIt()
    {
        var store = NewStore(null);
        var view = store.ViewFor(Ann);
        store.CreateMailbox(view, "Out", use: "sent");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.RenameMailbox(view, "Out", "Gone"));
        Assert.AreEqual(@"\Sent", Read(store, "Gone").Use);
        Assert.AreEqual(1, store.ReadMailboxUses(view).Count);

        store.DeleteMailbox(view, "Gone");
        Assert.AreEqual(0, store.ReadMailboxUses(view).Count);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(view, "Again", use: "sent"));
    }

    [TestMethod]
    public void RenameInbox_LeavesInboxWithoutUse()
    {
        var store = NewStore(null);
        var view = store.ViewFor(Ann);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.RenameMailbox(view, "INBOX", "Moved"));
        Assert.IsNull(Read(store, "Moved").Use);
        Assert.IsNull(Read(store, "INBOX").Use);
    }

    [TestMethod]
    public void ReadMailboxUses_EmptyViewHasNone_AndNullViewThrows()
    {
        var store = NewStore(null);

        Assert.AreEqual(0, store.ReadMailboxUses(store.ViewFor("nobody")).Count);
        Assert.ThrowsExactly<ArgumentNullException>(() => store.ReadMailboxUses(null!));
    }

    [TestMethod]
    public void HoldsAnyFolderUse_IsTrueOnlyWhileAMailboxHoldsAUse()
    {
        var none = NewStore(null);
        var some = NewStore(new Dictionary<string, string> { ["Sent"] = "sent" });

        Assert.IsFalse(none.HoldsAnyFolderUse);
        Assert.IsTrue(some.HoldsAnyFolderUse);
        Assert.AreEqual(MailStoreOutcome.Succeeded, some.DeleteMailbox(some.ViewFor(Ann), "Sent"));
        Assert.IsFalse(some.HoldsAnyFolderUse);
    }

    private static MailboxStore NewStore(IReadOnlyDictionary<string, string>? folderUses, string[]? folders = null) =>
        new(
            [],
            allowAnonymous: false,
            new SettableTimeProvider(),
            mailDomains: new MailDomainConfiguration(["example.com"], [Ann], [], [], [new MailboxSettings(Ann, null, folders ?? [], folderUses)]));

    private static MailboxSnapshot Read(MailboxStore store, string mailboxName) =>
        MailStoreFixture.Read(store, store.ViewFor(Ann), mailboxName);
}
