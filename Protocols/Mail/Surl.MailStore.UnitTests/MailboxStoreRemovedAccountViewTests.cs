using static Surl.MailStore.MailStoreFixture;
using static Surl.MailStore.PersistedStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// The empty view a removed account's name gets, shared folders included (ADR-0185 Amendment 1).
/// </summary>
[TestClass]
public sealed class MailboxStoreRemovedAccountViewTests
{
    private const string Team = "Shared/team";
    private const string Mailbox = "ann@example.com";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("al", true, DisplayName = "account")]
    [DataRow(Mailbox, false, DisplayName = "--mailbox account")]
    [DataRow("nobody", false, DisplayName = "member with no owner")]
    public void RetireOwner_SharedFolderMember_GivesAViewWithNoMailboxAndNoSharedFolder(string name, bool retired)
    {
        var store = SharedStore();

        Assert.AreEqual(retired, store.RetireOwner(name));

        AssertEmptyView(store, name);
    }

    [TestMethod]
    public void RetireOwner_AccountInNoSharedFolder_GivesAnEmptyView()
    {
        var store = SharedStore();

        store.RetireOwner("cy");

        AssertEmptyView(store, "cy");
    }

    [TestMethod]
    public void RetireOwner_MailboxAddress_StaysDeliverableWhileItsViewIsEmpty()
    {
        var store = SharedStore();

        store.RetireOwner(Mailbox);

        Assert.AreEqual(MailRecipientLookup.Deliverable, store.LookUpRecipient($"<{Mailbox}>", out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "late", $"<{Mailbox}>"));
        AssertEmptyView(store, Mailbox);
    }

    [TestMethod]
    public void RetireOwner_OneMember_LeavesTheOtherMembersTheirSharedFolders()
    {
        var store = SharedStore();

        store.RetireOwner("al");

        CollectionAssert.AreEqual(new[] { "INBOX", Team }, store.ListMailboxes(store.ViewFor(Mailbox)).ToArray());
    }

    [TestMethod]
    public void AddOwner_RemovedAccount_IsTrueAndGivesBackItsMailboxesAndSharedFolders()
    {
        var store = SharedStore();
        Deliver(store, "kept", "<al@example.com>");
        store.RetireOwner("al");

        Assert.IsTrue(store.AddOwner("al"));

        var view = store.ViewFor("al");
        CollectionAssert.AreEqual(new[] { "INBOX", Team }, store.ListMailboxes(view).ToArray());
        Assert.AreEqual("kept", Fetch(store, view, "INBOX", 1));
    }

    [TestMethod]
    public void AddOwner_RemovedMailboxAccount_IsFalseAndGivesBackItsMailboxesAndSharedFolders()
    {
        var store = SharedStore();
        Deliver(store, "kept", $"<{Mailbox}>");
        store.RetireOwner(Mailbox);
        var changes = store.ChangeCount;

        Assert.IsFalse(store.AddOwner(Mailbox));

        var view = store.ViewFor(Mailbox);
        CollectionAssert.AreEqual(new[] { "INBOX", Team }, store.ListMailboxes(view).ToArray());
        Assert.AreEqual("kept", Fetch(store, view, "INBOX", 1));
        Assert.AreEqual(changes, store.ChangeCount);
    }

    [TestMethod]
    public void AddOwner_RemovedMemberWithNoOwner_IsTrueAndGivesAnInboxAndItsSharedFolders()
    {
        var store = SharedStore();
        store.RetireOwner("nobody");

        Assert.IsTrue(store.AddOwner("nobody"));

        CollectionAssert.AreEqual(new[] { "INBOX", Team }, store.ListMailboxes(store.ViewFor("nobody")).ToArray());
    }

    [TestMethod]
    public void AddOwner_ThrowingForARemovedName_KeepsTheRemoval()
    {
        var clock = new SettableTimeProvider();
        var store = new MailboxStore(["al"], allowAnonymous: false, clock, mailDomains: SharedConfiguration());
        store.RetireOwner("nobody");
        clock.UtcNow = DateTimeOffset.FromUnixTimeSeconds(uint.MaxValue + 1L);

        Assert.ThrowsExactly<InvalidOperationException>(() => store.AddOwner("nobody"));

        AssertEmptyView(store, "nobody");
    }

    [TestMethod]
    public void ViewFor_MemberWithNoOwnerNeverRemoved_ReachesItsSharedFolders()
    {
        var store = SharedStore();
        store.RetireOwner("al");

        CollectionAssert.AreEqual(new[] { Team }, store.ListMailboxes(store.ViewFor("nobody")).ToArray());
    }

    [TestMethod]
    public async Task ViewFor_MemberWhoseOwnerLoadedFromFilesIsUnreached_ReachesItsSharedFolders()
    {
        var fileSystem = NewFileSystem();
        var saved = await LoadAsync(fileSystem, ["nobody"]);
        await saved.SaveChangesAsync(TestContext.CancellationToken);

        var store = await MailboxStore.LoadAsync(
            new MailStoreFiles(fileSystem, StateFolder),
            ["al"],
            allowAnonymous: false,
            new SettableTimeProvider(),
            mailDomains: SharedConfiguration(),
            cancellationToken: TestContext.CancellationToken);

        var view = store.ViewFor("nobody");
        Assert.IsNull(view.OwnerName);
        CollectionAssert.AreEqual(new[] { Team }, store.ListMailboxes(view).ToArray());
    }

    [TestMethod]
    public void RetireOwner_ViewAndRecipientGivenBefore_StillWork()
    {
        var store = SharedStore();
        var view = store.ViewFor("al");
        var recipient = Recipient(store, "<al@example.com>");

        store.RetireOwner("al");

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([recipient], Pending(store, "after"u8)));
        Assert.AreEqual("after", Fetch(store, view, "INBOX", 1));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(view, Team, "x"u8, MailFlags.None, null, out _));
        CollectionAssert.AreEqual(new[] { "INBOX", Team }, store.ListMailboxes(view).ToArray());
    }

    [TestMethod]
    public void RetireOwner_AnonymousStore_LeavesTheAnonymousView()
    {
        var store = new MailboxStore(["al"], allowAnonymous: true, new SettableTimeProvider());

        store.RetireOwner("al");

        Assert.AreEqual(string.Empty, store.ViewFor("al").OwnerName);
    }

    [TestMethod]
    public void Reset_ForgetsRemovals()
    {
        var store = SharedStore();
        store.RetireOwner("al");
        store.RetireOwner(Mailbox);
        store.RetireOwner("nobody");

        store.Reset();

        CollectionAssert.AreEqual(new[] { "INBOX", Team }, store.ListMailboxes(store.ViewFor("al")).ToArray());
        CollectionAssert.AreEqual(new[] { "INBOX", Team }, store.ListMailboxes(store.ViewFor(Mailbox)).ToArray());
        CollectionAssert.AreEqual(new[] { Team }, store.ListMailboxes(store.ViewFor("nobody")).ToArray());
    }

    [TestMethod]
    public void Restore_PutsBackExactlyTheRemovalsInForceAtTheSnapshot()
    {
        var store = SharedStore();
        store.RetireOwner(Mailbox);
        store.RetireOwner("nobody");
        var snapshot = store.TakeSnapshot();
        store.AddOwner(Mailbox);
        store.AddOwner("nobody");
        store.RetireOwner("al");

        store.Restore(snapshot);

        AssertEmptyView(store, Mailbox);
        AssertEmptyView(store, "nobody");
        CollectionAssert.AreEqual(new[] { "INBOX", Team }, store.ListMailboxes(store.ViewFor("al")).ToArray());
    }

    private static void AssertEmptyView(MailboxStore store, string name)
    {
        var view = store.ViewFor(name);
        Assert.IsNull(view.OwnerName);
        Assert.IsEmpty(view.SharedFolders);
        Assert.IsEmpty(store.ListMailboxes(view));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.ReadMailbox(view, "INBOX", out _));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.ReadMailbox(view, Team, out _));
        Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.Append(view, Team, "x"u8, MailFlags.None, null, out _));
    }

    private static MailboxStore SharedStore() =>
        new(["al", "cy"], allowAnonymous: false, new SettableTimeProvider(), mailDomains: SharedConfiguration());

    private static MailDomainConfiguration SharedConfiguration() =>
        new(["example.com"], [Mailbox], [], [], null, [new MailSharedFolder("team", ["al", Mailbox, "nobody"])]);
}
