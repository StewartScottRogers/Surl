using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.MailStore.MailStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// How a test prepares and rewinds the mail store: <see cref="MailboxStore.Seed"/>,
/// <see cref="MailboxStore.Reset"/>, <see cref="MailboxStore.TakeSnapshot"/> and
/// <see cref="MailboxStore.Restore"/> (ADR-0124 decision 3; ADR-0131).
/// </summary>
[TestClass]
public sealed class MailboxStoreTests
{
    private const string Domain = "example.com";
    private const string Ann = "ann@example.com";

    // Bytes no text encoding round-trips, so only a byte-for-byte store passes.
    private static readonly byte[] RawMessage = [.. "Subject: hi\r\n\r\nbody\r\n"u8, 0x00, 0xFF, 0x80, (byte)'\n'];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Seed_ToAnAccount_IsReadByImapUnderTheNextUidWithItsExactBytes()
    {
        var store = NewStore("ann");
        var view = store.ViewFor("ann");
        Deliver(store, "first", "<ann@x>");

        var result = store.Seed("<ann@x>", RawMessage);

        Assert.AreEqual(new MailSeedResult(MailRecipientLookup.Deliverable, MailStoreOutcome.Succeeded), result);
        CollectionAssert.AreEqual(new uint[] { 1, 2 }, Uids(store, view, "INBOX"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.FetchMessage(view, "INBOX", 2, out var bytes));
        CollectionAssert.AreEqual(RawMessage, bytes.ToArray());
        Assert.AreEqual(MailFlags.None, Read(store, view, "INBOX").Messages[1].Flags);
    }

    [TestMethod]
    public void Seed_ToAnAccount_IsTakenByPop3WithItsExactBytes()
    {
        var store = NewStore("ann");
        store.Seed("<ann@x>", RawMessage);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(store.ViewFor("ann"), out var maildrop));
        using (maildrop)
        {
            Assert.AreEqual(new MaildropMessage(1, 1, RawMessage.Length), maildrop!.Messages.Single());
            CollectionAssert.AreEqual(RawMessage, maildrop.ReadMessage(1).ToArray());
        }
    }

    [TestMethod]
    [DataRow("<zed@x>")]
    [DataRow("<not an address")]
    [DataRow("<ann@other.org>")]
    [DataRow("<nobody@example.com>")]
    public void Seed_ToAnAddressNoOwnerHas_AnswersWhatSmtpsLookUpAnswersAndStoresNothing(string path)
    {
        var store = new MailboxStore(["zara"], allowAnonymous: false, new SettableTimeProvider(), mailDomains: Domains());
        var expected = store.LookUpRecipient(path, out _);

        var result = store.Seed(path, RawMessage);

        Assert.AreNotEqual(MailRecipientLookup.Deliverable, expected);
        Assert.AreEqual(new MailSeedResult(expected, null), result);
        Assert.AreEqual(0L, store.ChangeCount);
    }

    [TestMethod]
    public void Seed_ToAnAlias_DeliversToEveryOwnerAndPublishesSeededForEachCopyWithoutConsultingTheFaultPlan()
    {
        var events = new RecordingSurlEventPublisher();
        var outOfSpace = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace));
        var store = new MailboxStore(
            ["ann", "bob"],
            allowAnonymous: false,
            new SettableTimeProvider(),
            mailDomains: new MailDomainConfiguration([Domain], [], [new MailAlias("team@example.com", ["ann@example.com", "bob@example.com"])], []),
            events: events,
            faults: outOfSpace);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Seed("<team@example.com>", "hello"u8).Delivery);

        CollectionAssert.AreEqual(
            new SurlEvent[] { Change(StoreChange.Seeded, "ann/INBOX/;UID=1", 5), Change(StoreChange.Seeded, "bob/INBOX/;UID=1", 5) },
            events.Events.ToArray());
    }

    [TestMethod]
    public void Seed_PastTheMessageBoundOrTheQuota_AnswersAsADeliveryAndStoresNothing()
    {
        var bounded = new MailboxStore(["ann"], allowAnonymous: false, new SettableTimeProvider(), maxMessageBytes: 4);
        var quoted = new MailboxStore([], allowAnonymous: false, new SettableTimeProvider(), mailDomains: Domains(quotaBytes: 4));

        Assert.AreEqual(MailStoreOutcome.MessageTooLarge, bounded.Seed("<ann@x>", "hello"u8).Delivery);
        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, quoted.Seed("<" + Ann + ">", "hello"u8).Delivery);
        Assert.AreEqual(0L, bounded.ChangeCount);
        Assert.AreEqual(0L, quoted.ChangeCount);
    }

    [TestMethod]
    public void Reset_LeavesExactlyTheConfiguredMailboxesEmptyWithNewUidValiditiesAndNextUidOne()
    {
        var clock = new SettableTimeProvider();
        var events = new RecordingSurlEventPublisher();
        var store = new MailboxStore([], allowAnonymous: false, clock, mailDomains: Domains(quotaBytes: 100), events: events);
        var view = store.ViewFor(Ann);
        store.Seed("<" + Ann + ">", "one"u8);
        store.Append(view, "Archive", "two"u8, MailFlags.Seen, null, out _);
        store.Append(view, "Shared/team", "three"u8, MailFlags.None, null, out _);
        store.CreateMailbox(view, "Made");
        store.DeleteMailbox(view, "Archive");
        var before = new[] { "INBOX", "Shared/team" }.Select(name => Read(store, view, name).UidValidity).ToList();
        var seen = events.Events.Count;

        store.Reset();

        CollectionAssert.AreEqual(new[] { "Archive", "INBOX", "Shared/team" }, store.ListMailboxes(view).ToArray());
        foreach (var name in store.ListMailboxes(view))
        {
            var mailbox = Read(store, view, name);
            Assert.IsEmpty(mailbox.Messages);
            Assert.AreEqual(1u, mailbox.NextUid);
            CollectionAssert.DoesNotContain(before, mailbox.UidValidity);
        }

        CollectionAssert.AreEqual(new SurlEvent[] { Change(StoreChange.Reset, string.Empty, 0) }, events.Events.Skip(seen).ToArray());
        Assert.AreEqual(MailStoreOutcome.QuotaExceeded, store.Seed("<" + Ann + ">", new byte[101]).Delivery);
    }

    [TestMethod]
    public void Reset_FreesTheBoundsSoTheStoreFillsAgainToThem()
    {
        var store = new MailboxStore(["ann"], allowAnonymous: false, new SettableTimeProvider(), maxMessages: 1, maxMailboxes: 1);
        var view = store.ViewFor("ann");
        store.Seed("<ann@x>", "one"u8);
        store.CreateMailbox(view, "Made");

        store.Reset();

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Seed("<ann@x>", "one"u8).Delivery);
        Assert.AreEqual(MailStoreOutcome.StoreFull, store.Seed("<ann@x>", "two"u8).Delivery);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(view, "Made"));
        Assert.AreEqual(MailStoreOutcome.TooManyMailboxes, store.CreateMailbox(view, "More"));
    }

    [TestMethod]
    public void Reset_AnAnonymousStore_EmptiesItsInbox()
    {
        var store = new MailboxStore([], allowAnonymous: true, new SettableTimeProvider());
        store.Seed("<anyone@x>", "one"u8);

        store.Reset();

        Assert.IsEmpty(Read(store, store.ViewFor(null), "INBOX").Messages);
    }

    [TestMethod]
    public void Reset_WhenTooFewUidValiditiesAreLeft_ThrowsAndChangesNothing()
    {
        var clock = new SettableTimeProvider();
        var store = new MailboxStore(["ann", "bob"], allowAnonymous: false, clock);
        store.Seed("<ann@x>", "one"u8);
        clock.UtcNow = DateTimeOffset.FromUnixTimeSeconds(uint.MaxValue);

        Assert.ThrowsExactly<InvalidOperationException>(store.Reset);

        Assert.HasCount(1, Read(store, store.ViewFor("ann"), "INBOX").Messages);
    }

    [TestMethod]
    public void TakeSnapshot_CountsMailboxesMessagesAndDistinctBytes()
    {
        var store = NewStore("ann", "bob");
        Deliver(store, "hello", "<ann@x>", "<bob@x>");
        store.CreateMailbox(store.ViewFor("ann"), "Work");

        var snapshot = store.TakeSnapshot();

        Assert.AreEqual(3, snapshot.MailboxCount);
        Assert.AreEqual(2, snapshot.MessageCount);
        Assert.AreEqual(5L, snapshot.ByteCount);
    }

    [TestMethod]
    public void Restore_AfterChanges_GivesBackExactlyTheSnapshotsMailboxesMessagesFlagsAndUidsUnderNewUidValidities()
    {
        var clock = new SettableTimeProvider();
        var events = new RecordingSurlEventPublisher();
        var store = new MailboxStore(["ann"], allowAnonymous: false, clock, events: events);
        var view = store.ViewFor("ann");
        store.Seed("<ann@x>", "one"u8);
        store.Seed("<ann@x>", RawMessage);
        store.ChangeFlags(view, "INBOX", 2, MailFlagChange.Add, MailFlags.Flagged | MailFlags.Deleted, out _);
        store.CreateMailbox(view, "Work");
        store.Copy(view, "INBOX", [1], "Work", out _);
        var before = SnapshotsOf(store, view);
        var snapshot = store.TakeSnapshot();

        store.Expunge(view, "INBOX", out _);
        store.ChangeFlags(view, "INBOX", 1, MailFlagChange.Add, MailFlags.Seen, out _);
        store.DeleteMailbox(view, "Work");
        store.CreateMailbox(view, "Later");
        store.Seed("<ann@x>", "two"u8);
        clock.UtcNow += TimeSpan.FromHours(1);
        var seen = events.Events.Count;

        store.Restore(snapshot);

        var after = SnapshotsOf(store, view);
        CollectionAssert.AreEqual(before.Select(mailbox => mailbox.Name).ToArray(), after.Select(mailbox => mailbox.Name).ToArray());
        foreach (var (was, now) in before.Zip(after))
        {
            Assert.AreEqual(was.NextUid, now.NextUid);
            CollectionAssert.AreEqual(was.Messages.ToArray(), now.Messages.ToArray());
            Assert.AreNotEqual(was.UidValidity, now.UidValidity);
        }

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.FetchMessage(view, "INBOX", 2, out var bytes));
        CollectionAssert.AreEqual(RawMessage, bytes.ToArray());
        Assert.AreEqual("one", Fetch(store, view, "Work", 1));
        CollectionAssert.AreEqual(new SurlEvent[] { Change(StoreChange.Restored, string.Empty, snapshot.ByteCount) }, events.Events.Skip(seen).ToArray());
    }

    [TestMethod]
    public void Restore_ASnapshotTwiceAfterAReset_RecountsTheBounds()
    {
        var store = new MailboxStore(["ann"], allowAnonymous: false, new SettableTimeProvider(), maxMessages: 2, maxTotalMessageBytes: 6);
        var view = store.ViewFor("ann");
        store.Seed("<ann@x>", "one"u8);
        var snapshot = store.TakeSnapshot();

        store.Reset();
        store.Restore(snapshot);
        store.Restore(snapshot);

        Assert.AreEqual("one", Fetch(store, view, "INBOX", 1));
        Assert.AreEqual(MailStoreOutcome.StoreFull, store.Seed("<ann@x>", "four"u8).Delivery);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Seed("<ann@x>", "two"u8).Delivery);
        Assert.AreEqual(MailStoreOutcome.StoreFull, store.Seed("<ann@x>", "x"u8).Delivery);
    }

    [TestMethod]
    public void Restore_ASnapshotOfAnotherStoreOrNone_Throws()
    {
        var store = NewStore("ann");

        Assert.ThrowsExactly<ArgumentException>(() => store.Restore(NewStore("ann").TakeSnapshot()));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.Restore(null!));
    }

    [TestMethod]
    public void ResetAndRestore_ReleaseThePop3LockAndTheOldSessionRemovesNothingNewAtQuit()
    {
        var store = NewStore("ann");
        var view = store.ViewFor("ann");
        store.Seed("<ann@x>", "old"u8);
        var snapshot = store.TakeSnapshot();
        store.LockMaildrop(view, out var oldSession);

        store.Reset();
        store.Seed("<ann@x>", "new"u8);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(view, out var newSession));
        Assert.AreEqual("old", Encoding.ASCII.GetString(oldSession!.ReadMessage(1).Span));
        Assert.AreEqual(0, oldSession.RemoveMessages([1]));
        oldSession.Dispose();
        Assert.AreEqual(MailStoreOutcome.MaildropLocked, store.LockMaildrop(view, out _));

        store.Restore(snapshot);

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(view, out var restoredSession));
        Assert.AreEqual("old", Fetch(store, view, "INBOX", 1));
        newSession!.Dispose();
        restoredSession!.Dispose();
    }

    [TestMethod]
    public async Task OnDisk_SeedIsWrittenButResetSnapshotAndRestoreAreRefused()
    {
        var fileSystem = PersistedStoreFixture.NewFileSystem();
        var store = await PersistedStoreFixture.LoadAsync(fileSystem, ["ann"]);
        store.Seed("<ann@x>", RawMessage);
        await store.SaveChangesAsync(TestContext.CancellationToken);

        var reset = Assert.ThrowsExactly<InvalidOperationException>(store.Reset);
        Assert.ThrowsExactly<InvalidOperationException>(() => store.TakeSnapshot());

        StringAssert.Contains(reset.Message, "on disk");
        var reloaded = await PersistedStoreFixture.LoadAsync(fileSystem, ["ann"]);
        Assert.AreEqual(MailStoreOutcome.Succeeded, reloaded.FetchMessage(reloaded.ViewFor("ann"), "INBOX", 1, out var bytes));
        CollectionAssert.AreEqual(RawMessage, bytes.ToArray());
    }

    private static List<MailboxSnapshot> SnapshotsOf(MailboxStore store, MailView view) =>
        [.. store.ListMailboxes(view).Select(name => Read(store, view, name))];

    private static MailDomainConfiguration Domains(long? quotaBytes = null) =>
        new(
            [Domain],
            [Ann],
            [],
            [],
            [new MailboxSettings(Ann, quotaBytes, ["Archive"])],
            [new MailSharedFolder("team", [Ann])]);

    private static StoreChangedEvent Change(StoreChange change, string name, long? byteCount) =>
        new(StoreKind.Mail, change, name, byteCount, ExchangeId: null);
}
