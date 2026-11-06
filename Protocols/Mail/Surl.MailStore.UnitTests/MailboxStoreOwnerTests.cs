using static Surl.MailStore.MailStoreFixture;

namespace Surl.MailStore;

[TestClass]
public sealed class MailboxStoreOwnerTests
{
    [TestMethod]
    public void Constructor_Defaults_AreTheAdrsBounds()
    {
        var store = NewStore();

        Assert.AreEqual(0, store.MaxMessageBytes);
        Assert.AreEqual(100_000, store.MaxMessages);
        Assert.AreEqual(268_435_456, store.MaxTotalMessageBytes);
        Assert.AreEqual(10_000, store.MaxMailboxes);
        Assert.AreEqual(0, store.ChangeCount);
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailboxStore(null!, false, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailboxStore([], false, null!));
    }

    [TestMethod]
    [DataRow(-1L, 1, 1L, 0)]
    [DataRow(0L, 0, 1L, 0)]
    [DataRow(0L, 1, 0L, 0)]
    [DataRow(0L, 1, 1L, -1)]
    public void Constructor_BoundOutOfRange_Throws(long maxMessageBytes, int maxMessages, long maxTotalMessageBytes, int maxMailboxes)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new MailboxStore([], false, TimeProvider.System, maxMessageBytes, maxMessages, maxTotalMessageBytes, maxMailboxes));
    }

    [TestMethod]
    public void ViewFor_EveryAccount_HasAnEmptyInbox()
    {
        var store = NewStore("alice", "bob");

        foreach (var name in new[] { "alice", "bob" })
        {
            var view = store.ViewFor(name);
            Assert.AreEqual(name, view.OwnerName);
            CollectionAssert.AreEqual(new[] { "INBOX" }, store.ListMailboxes(view).ToArray());
            var inbox = Read(store, view, "INBOX");
            Assert.AreEqual(1u, inbox.NextUid);
            Assert.AreEqual(0, inbox.Messages.Count);
        }
    }

    [TestMethod]
    public void ViewFor_EmptyNameUnknownNameOrNoName_IsAnEmptyView()
    {
        var store = NewStore("alice", string.Empty);

        foreach (var name in new[] { string.Empty, "carol", "Alice", null })
        {
            var view = store.ViewFor(name);
            Assert.IsNull(view.OwnerName);
            Assert.AreEqual(0, store.ListMailboxes(view).Count);
            Assert.AreEqual(MailStoreOutcome.MailboxMissing, store.ReadMailbox(view, "INBOX", out var snapshot));
            Assert.IsNull(snapshot);
        }
    }

    [TestMethod]
    public void Constructor_RepeatedAccountName_IsOneOwner()
    {
        var store = NewStore("alice", "alice");

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "m", "<alice@x>"));

        Assert.AreEqual(1, Read(store, store.ViewFor("alice"), "INBOX").Messages.Count);
    }

    [TestMethod]
    public void LookUpRecipient_ExactLocalPart_IsTheAccountWhateverTheDomain()
    {
        var store = NewStore("alice", "bob");

        Assert.AreEqual("alice", Recipient(store, "<alice@example.com>").OwnerName);
        Assert.AreEqual("bob", Recipient(store, "<bob@[192.0.2.1]>").OwnerName);
    }

    [TestMethod]
    public void LookUpRecipient_LocalPartInAnotherCase_IsTheOneAccountEqualIgnoringCase()
    {
        var store = NewStore("Alice");

        Assert.AreEqual("Alice", Recipient(store, "<ALICE@example.com>").OwnerName);
    }

    [TestMethod]
    public void LookUpRecipient_TwoAccountsEqualIgnoringCase_MatchesOnlyExactly()
    {
        var store = NewStore("Alice", "ALICE");

        Assert.AreEqual("Alice", Recipient(store, "<Alice@x>").OwnerName);
        Assert.AreEqual("ALICE", Recipient(store, "<ALICE@x>").OwnerName);
        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<alice@x>", out var recipient));
        Assert.IsNull(recipient);
    }

    [TestMethod]
    public void LookUpRecipient_UnknownLocalPart_IsNoSuchAccount()
    {
        var store = NewStore("alice");

        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<carol@example.com>", out var recipient));
        Assert.IsNull(recipient);
        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<postmaster>", out _));
        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<\"\"@example.com>", out _));
    }

    [TestMethod]
    public void LookUpRecipient_Postmaster_IsAnAccountLikeAnyOther()
    {
        var store = NewStore("postmaster");

        Assert.AreEqual("postmaster", Recipient(store, "<Postmaster>").OwnerName);
    }

    [TestMethod]
    public void LookUpRecipient_InvalidPath_IsInvalidAddress()
    {
        foreach (var store in new[] { NewStore("alice"), new MailboxStore([], true, TimeProvider.System) })
        {
            Assert.AreEqual(MailRecipientLookup.InvalidAddress, store.LookUpRecipient("<alice>", out var recipient));
            Assert.IsNull(recipient);
        }
    }

    [TestMethod]
    public void LookUpRecipient_NullPath_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => NewStore().LookUpRecipient(null!, out _));
    }

    [TestMethod]
    public void AllowAnonymous_EveryRecipientAndEveryView_IsTheAnonymousOwner()
    {
        var store = new MailboxStore(["alice"], allowAnonymous: true, new SettableTimeProvider());

        Assert.AreEqual(string.Empty, Recipient(store, "<anyone@example.com>").OwnerName);
        Assert.AreEqual(string.Empty, Recipient(store, "<alice@example.com>").OwnerName);
        foreach (var name in new[] { "alice", "carol", null })
        {
            Assert.AreEqual(string.Empty, store.ViewFor(name).OwnerName);
        }
    }

    [TestMethod]
    public void AllowAnonymous_OneCopyPerRecipient_ReachesEverySession()
    {
        var store = new MailboxStore(["alice"], allowAnonymous: true, new SettableTimeProvider());

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "hello", "<a@x>", "<b@y>"));

        CollectionAssert.AreEqual(new uint[] { 1, 2 }, Uids(store, store.ViewFor(null), "INBOX"));
        Assert.AreEqual("hello", Fetch(store, store.ViewFor("alice"), "inbox", 2));
    }
}
