using Surl.Protocol.Abstractions;
using static Surl.MailStore.MailStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// Owners added and retired while the store runs (ADR-0185).
/// </summary>
[TestClass]
public sealed class MailboxStoreRunTimeOwnerTests
{
    [TestMethod]
    public void AddOwner_NewName_GivesAnEmptyInboxThatIsSelectableAndDeliverable()
    {
        var store = NewStore("ann");

        Assert.IsTrue(store.AddOwner("bob"));

        var view = store.ViewFor("bob");
        Assert.AreEqual("bob", view.OwnerName);
        var inbox = Read(store, view, "INBOX");
        Assert.AreEqual(1u, inbox.NextUid);
        Assert.AreEqual(0, inbox.Messages.Count);
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "hi", "<bob@x>"));
        Assert.AreEqual("hi", Fetch(store, view, "INBOX", 1));
    }

    [TestMethod]
    public void AddOwner_NewName_IsSeedable()
    {
        var store = NewStore();
        store.AddOwner("bob");

        var seeded = store.Seed("<bob@x>", "m"u8);

        Assert.AreEqual(MailRecipientLookup.Deliverable, seeded.Recipient);
    }

    [TestMethod]
    public void AddOwner_NewName_CountsOneChangeAndPublishesOneCreatedEvent()
    {
        var events = new RecordingSurlEventPublisher();
        var store = new MailboxStore(["ann"], allowAnonymous: false, new SettableTimeProvider(), events: events);

        store.AddOwner("bob");

        Assert.AreEqual(1, store.ChangeCount);
        CollectionAssert.AreEqual(
            new[] { new StoreChangedEvent(StoreKind.Mail, StoreChange.Created, "bob/INBOX", null, ExchangeId: null) },
            events.Events.ToArray());
    }

    [TestMethod]
    public void AddOwner_AtMaxMailboxes_StillSucceeds()
    {
        var store = new MailboxStore([], allowAnonymous: false, new SettableTimeProvider(), maxMailboxes: 0);

        Assert.IsTrue(store.AddOwner("bob"));
        Assert.AreEqual(MailRecipientLookup.Deliverable, store.LookUpRecipient("<bob@x>", out _));
    }

    [TestMethod]
    public void AddOwner_AlreadyReachedEmptyOrMailboxAddress_IsFalseAndPublishesNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var domains = new MailDomainConfiguration(["example.com"], ["box@example.com"], [], []);
        var store = new MailboxStore(["ann"], allowAnonymous: false, new SettableTimeProvider(), mailDomains: domains, events: events);

        Assert.IsFalse(store.AddOwner("ann"));
        Assert.IsFalse(store.AddOwner(string.Empty));
        Assert.IsFalse(store.AddOwner("box@example.com"));

        Assert.AreEqual(0, store.ChangeCount);
        Assert.AreEqual(0, events.Events.Count);
    }

    [TestMethod]
    public void AddOwner_AnonymousStore_IsFalseAndPublishesNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var store = new MailboxStore(["ann"], allowAnonymous: true, new SettableTimeProvider(), events: events);

        Assert.IsFalse(store.AddOwner("bob"));
        Assert.IsFalse(store.RetireOwner("ann"));

        Assert.AreEqual(0, events.Events.Count);
        Assert.AreEqual(0, store.ChangeCount);
    }

    [TestMethod]
    public void AddOwner_SharedFolderOwnerNameOrNull_Throws()
    {
        var store = NewStore();

        Assert.ThrowsExactly<ArgumentException>(() => store.AddOwner(MailboxStore.SharedFolderOwnerName));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.AddOwner(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.RetireOwner(null!));
    }

    [TestMethod]
    public void RetireOwner_ReachedOwner_IsNoSuchAccountToLookUpRecipientAndSeedAndEmptyToViewFor()
    {
        var store = NewStore("ann");
        Deliver(store, "kept", "<ann@x>");

        Assert.IsTrue(store.RetireOwner("ann"));

        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<ann@x>", out _));
        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.Seed("<ann@x>", "m"u8).Recipient);
        var view = store.ViewFor("ann");
        Assert.IsNull(view.OwnerName);
        Assert.IsEmpty(view.SharedFolders);
        Assert.IsEmpty(store.ListMailboxes(view));
    }

    [TestMethod]
    public void RetireOwner_OneOfTwoNamesDifferingByCase_LeavesTheOtherFoundByEither()
    {
        var store = NewStore("ann", "ANN");

        store.RetireOwner("ann");

        Assert.AreEqual("ANN", Recipient(store, "<ann@x>").OwnerName);
        Assert.AreEqual("ANN", Recipient(store, "<ANN@x>").OwnerName);
    }

    [TestMethod]
    public void RetireOwner_ThenAddOwner_ReachesTheOwnerAgainWithItsMailAndPublishesNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var store = new MailboxStore(["ann"], allowAnonymous: false, new SettableTimeProvider(), events: events);
        Deliver(store, "kept", "<ann@x>");
        var published = events.Events.Count;
        var changes = store.ChangeCount;

        store.RetireOwner("ann");
        Assert.IsTrue(store.AddOwner("ann"));

        Assert.AreEqual("kept", Fetch(store, store.ViewFor("ann"), "INBOX", 1));
        Assert.AreEqual(published, events.Events.Count);
        Assert.AreEqual(changes, store.ChangeCount);
    }

    [TestMethod]
    public void RetireOwner_UnknownOrAlreadyRetired_IsFalse()
    {
        var store = NewStore("ann");

        Assert.IsFalse(store.RetireOwner("bob"));
        Assert.IsTrue(store.RetireOwner("ann"));
        Assert.IsFalse(store.RetireOwner("ann"));
    }

    [TestMethod]
    public void RetireOwner_KeepsItsMailCountedAgainstTheBounds()
    {
        var store = new MailboxStore(["ann", "bob"], allowAnonymous: false, new SettableTimeProvider(), maxMessages: 1);
        Deliver(store, "kept", "<ann@x>");
        store.RetireOwner("ann");

        Assert.AreEqual(MailStoreOutcome.StoreFull, Deliver(store, "more", "<bob@x>"));
    }

    [TestMethod]
    [DataRow("ann@example.com", "bob@example.com", DisplayName = "retired target listed first")]
    [DataRow("bob@example.com", "ann@example.com", DisplayName = "retired target listed second")]
    public void RetireOwner_OneOfTwoAliasTargets_LeavesTheOtherDeliverable(string first, string second)
    {
        var store = AliasStore(["ann", "bob"], [first, second]);

        store.RetireOwner("ann");

        CollectionAssert.AreEqual(new[] { "bob" }, Recipient(store, "<team@example.com>").OwnerNames.ToArray());
    }

    [TestMethod]
    public void RetireOwner_EveryAliasTarget_LeavesTheAliasNoSuchAccount()
    {
        var store = AliasStore(["ann", "bob", "team"], ["ann@example.com", "bob@example.com"]);

        store.RetireOwner("ann");
        store.RetireOwner("bob");

        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<team@example.com>", out var recipient));
        Assert.IsNull(recipient);
    }

    [TestMethod]
    public void AddOwner_MakingOneAliasTargetAmbiguousByCase_LeavesTheOtherTargetsDeliverable()
    {
        var store = AliasStore(["Ann", "bob"], ["ann@example.com", "bob@example.com"]);

        store.AddOwner("ANN");

        CollectionAssert.AreEqual(new[] { "bob" }, Recipient(store, "<team@example.com>").OwnerNames.ToArray());
    }

    [TestMethod]
    public void RetireOwner_AliasTargetWhoseCatchAllLeadsBackToTheAlias_LeavesTheOtherTargetsDeliverable()
    {
        var store = AliasStore(["ann", "bob"], ["ann@example.com", "bob@example.com"], catchAllTarget: "team@example.com");

        store.RetireOwner("ann");

        CollectionAssert.AreEqual(new[] { "bob" }, Recipient(store, "<team@example.com>").OwnerNames.ToArray());
        CollectionAssert.AreEqual(new[] { "bob" }, Recipient(store, "<anyone@example.com>").OwnerNames.ToArray());
    }

    private static MailboxStore AliasStore(string[] accountNames, string[] targets, string? catchAllTarget = null) =>
        new(
            accountNames,
            allowAnonymous: false,
            new SettableTimeProvider(),
            mailDomains: new MailDomainConfiguration(
                ["example.com"],
                [],
                [new MailAlias("team@example.com", targets)],
                catchAllTarget is null ? [] : [new MailCatchAll("example.com", catchAllTarget)]));

    [TestMethod]
    [DataRow(false, DisplayName = "no mail domain")]
    [DataRow(true, DisplayName = "mail domain")]
    public async Task LookUpRecipientAndViewFor_WhileAnOwnerIsAddedAndRetiredOnAnotherThread_FindAConfiguredAccountEveryTime(bool withMailDomain)
    {
        // Retiring an owner rebuilds the account lookup in order, so the last of many accounts is absent longest.
        string[] accountNames = [.. Enumerable.Range(0, 200).Select(index => $"account{index}"), "ann"];
        var domains = withMailDomain ? new MailDomainConfiguration(["example.com"], [], [], []) : null;
        var store = new MailboxStore(accountNames, allowAnonymous: false, new SettableTimeProvider(), mailDomains: domains);
        using var churning = new ManualResetEventSlim();
        using var readersDone = new CancellationTokenSource();
        var churn = RunOnOwnThread(() =>
        {
            do
            {
                store.AddOwner("bob");
                store.RetireOwner("bob");
                churning.Set();
            }
            while (!readersDone.IsCancellationRequested);

            return 0;
        });
        churning.Wait();

        var readers = Enumerable.Range(0, 4).Select(_ => RunOnOwnThread(() => CountMissesOfAnn(store, lookups: 2000))).ToArray();
        int[] misses;
        try
        {
            misses = await Task.WhenAll(readers);
        }
        finally
        {
            await readersDone.CancelAsync();
            await churn;
        }

        CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, misses);
    }

    private static Task<int> RunOnOwnThread(Func<int> work) =>
        Task.Factory.StartNew(work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>
    /// How many of <paramref name="lookups"/> rounds of <c>LookUpRecipient</c> and <c>ViewFor</c> did not find <c>ann</c>.
    /// </summary>
    private static int CountMissesOfAnn(MailboxStore store, int lookups) =>
        Enumerable.Range(0, lookups).Count(_ =>
            store.LookUpRecipient("<ann@example.com>", out var recipient) != MailRecipientLookup.Deliverable
            || recipient!.OwnerNames.Single() != "ann"
            || store.ViewFor("ann").OwnerName != "ann");

    [TestMethod]
    public void Reset_DropsRunTimeOwnersAndReachesRetiredConfiguredOnesEmpty()
    {
        var store = NewStore("ann");
        Deliver(store, "old", "<ann@x>");
        store.AddOwner("bob");
        Deliver(store, "bobs", "<bob@x>");
        store.RetireOwner("ann");

        store.Reset();

        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<bob@x>", out _));
        Assert.IsNull(store.ViewFor("bob").OwnerName);
        Assert.AreEqual(0, Read(store, store.ViewFor("ann"), "INBOX").Messages.Count);
        Assert.IsTrue(store.AddOwner("bob"));
        Assert.AreEqual(0, Read(store, store.ViewFor("bob"), "INBOX").Messages.Count);
    }

    [TestMethod]
    public void TakeSnapshot_CountsOwners()
    {
        var store = NewStore("ann");
        store.AddOwner("bob");

        Assert.AreEqual(2, store.TakeSnapshot().OwnerCount);
    }

    [TestMethod]
    public void Restore_DropsOwnersAddedSinceAndReachesOnesRetiredSince()
    {
        var store = NewStore("ann");
        Deliver(store, "one", "<ann@x>");
        var snapshot = store.TakeSnapshot();
        store.AddOwner("bob");
        store.RetireOwner("ann");

        store.Restore(snapshot);

        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<bob@x>", out _));
        Assert.AreEqual("one", Fetch(store, store.ViewFor("ann"), "INBOX", 1));
    }

    [TestMethod]
    public void Restore_BringsBackADroppedRunTimeOwnerWithItsMailAndRetiresOneReachedSince()
    {
        var store = NewStore("ann");
        store.AddOwner("bob");
        Deliver(store, "bobs", "<bob@x>");
        store.RetireOwner("ann");
        var snapshot = store.TakeSnapshot();
        store.Reset();

        store.Restore(snapshot);

        Assert.AreEqual("bobs", Fetch(store, store.ViewFor("bob"), "INBOX", 1));
        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<ann@x>", out _));
    }
}
