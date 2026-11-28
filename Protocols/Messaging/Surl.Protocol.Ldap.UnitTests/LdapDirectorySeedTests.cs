using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ldap.LdapDirectoryFixture;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Seeds, resets, snapshots and restores a directory and checks what it publishes (ADR-0158).
/// </summary>
[TestClass]
public sealed class LdapDirectorySeedTests
{
    private const string AliceLdif = "dn: cn=alice,dc=example,dc=com\nobjectClass: person\ncn: alice\n";

    private const string BobLdif = "dn: cn=bob,dc=example,dc=com\ncn: bob\n";

    private const string StaffLdif = "dn: ou=staff,dc=example,dc=com\nou: staff\n";

    private static (LdapDirectory Directory, RecordingSurlEventPublisher Events) Seeded(int maxEntries = LdapDirectory.DefaultMaxEntries)
    {
        var events = new RecordingSurlEventPublisher();
        var directory = new LdapDirectory(
            [Entry("dc=example,dc=com", ("objectClass", "domain"), ("dc", "example"))],
            TimeProvider.System,
            maxEntries,
            events: events);
        return (directory, events);
    }

    private static string HeldDns(LdapDirectory directory) => string.Join('|', directory.Entries.Select(entry => entry.Dn.Text));

    /// <summary>
    /// A kept seed of <paramref name="ldif"/> is refused with <paramref name="message"/>, publishes nothing, and
    /// changes neither the live entries nor what a reset returns to.
    /// </summary>
    private static void AssertKeptSeedRefused(
        LdapDirectory directory, RecordingSurlEventPublisher events, string ldif, string message, string heldAfterReset)
    {
        var heldBefore = HeldDns(directory);
        var publishedBefore = events.Events.Count;

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => directory.SeedEntries(ldif, keepOnReset: true));

        Assert.AreEqual(message, exception.Message);
        Assert.AreEqual(heldBefore, HeldDns(directory));
        Assert.HasCount(publishedBefore, events.Events);
        directory.Reset();
        Assert.AreEqual(heldAfterReset, HeldDns(directory));
    }

    [TestMethod]
    public void SeedEntries_FoundAtEachScope()
    {
        var (directory, _) = Seeded();
        directory.SeedEntries("version: 1\n\n" + AliceLdif);

        Assert.AreEqual("cn=alice,dc=example,dc=com", DnsOf(directory.Search(Search("cn=alice,dc=example,dc=com", LdapSearchScope.BaseObject))));
        Assert.AreEqual("cn=alice,dc=example,dc=com", DnsOf(directory.Search(Search("dc=example,dc=com", LdapSearchScope.SingleLevel))));
        Assert.AreEqual(
            "dc=example,dc=com|cn=alice,dc=example,dc=com",
            DnsOf(directory.Search(Search("dc=example,dc=com", LdapSearchScope.WholeSubtree))));
    }

    [TestMethod]
    public void SeedEntries_PublishesSeededPerEntry()
    {
        var (directory, events) = Seeded();
        directory.SeedEntries(AliceLdif + "\ndn: cn=bob,dc=example,dc=com\ncn: bob\n");

        var published = events.Events.Cast<StoreChangedEvent>().ToArray();
        Assert.HasCount(2, published);
        Assert.IsTrue(published.All(e => e is { Store: StoreKind.Directory, Change: StoreChange.Seeded, ExchangeId: null }));
        Assert.AreEqual("cn=alice,dc=example,dc=com", published[0].Name);
        Assert.AreEqual(LdapDirectoryContents.SizeOf(directory.Entries[1]), published[0].ByteCount);
    }

    [TestMethod]
    public void SeedEntries_AnExistingDn_IsRefusedAndChangesNothing()
    {
        var (directory, events) = Seeded();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => directory.SeedEntries("dn: DC=Example,DC=Com\ndc: example\n"));

        Assert.AreEqual("line 1: a duplicate DN", exception.Message);
        Assert.HasCount(1, directory.Entries);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public void SeedEntries_ASecondDnInTheText_IsRefusedWhole()
    {
        var (directory, _) = Seeded();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => directory.SeedEntries(AliceLdif + "\n" + AliceLdif));

        Assert.AreEqual("line 5: a duplicate DN", exception.Message);
        Assert.HasCount(1, directory.Entries);
    }

    [TestMethod]
    public void SeedEntries_AMissingParent_IsRefused()
    {
        var (directory, _) = Seeded();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => directory.SeedEntries("dn: cn=a,ou=gone,dc=example,dc=com\ncn: a\n"));

        Assert.AreEqual("line 1: a parent DN that is not in the file", exception.Message);
    }

    [TestMethod]
    public void SeedEntries_ASuperiorThatLeavesAnExistingEntryParentless_NamesThatEntry()
    {
        var directory = new LdapDirectory([Entry("cn=a,ou=x,dc=com", ("cn", "a"))], TimeProvider.System);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => directory.SeedEntries("dn: dc=com\ndc: com\n"));

        Assert.AreEqual("the entry cn=a,ou=x,dc=com: a parent DN that is not in the file", exception.Message);
    }

    [TestMethod]
    public void SeedEntries_TextThatIsNotLdif_IsRefusedWithTheReadersMessage()
    {
        var (directory, _) = Seeded();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => directory.SeedEntries("not ldif"));

        Assert.AreEqual("line 1: a line that is not <description>: <value>", exception.Message);
    }

    [TestMethod]
    public void SeedEntries_PastTheTextBound_IsRefused()
    {
        var (directory, _) = Seeded();

        Assert.ThrowsExactly<InvalidOperationException>(() => directory.SeedEntries(new string('a', LdapDirectory.MaxSeedBytes + 1)));
        Assert.ThrowsExactly<ArgumentNullException>(() => directory.SeedEntries(null!));
    }

    [TestMethod]
    public void SeedEntries_PastTheEntryLimit_IsRefusedAndChangesNothing()
    {
        var (directory, _) = Seeded(maxEntries: 2);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => directory.SeedEntries(AliceLdif + "\ndn: cn=bob,dc=example,dc=com\ncn: bob\n"));

        Assert.AreEqual("line 5: past the directory's bounds", exception.Message);
        Assert.HasCount(1, directory.Entries);
    }

    [TestMethod]
    public void Reset_ReturnsToTheEntriesItWasBuiltWith_AndPublishesReset()
    {
        var (directory, events) = Seeded();
        directory.SeedEntries(AliceLdif);

        directory.Reset();

        Assert.HasCount(1, directory.Entries);
        var reset = (StoreChangedEvent)events.Events[^1];
        Assert.AreEqual((StoreKind.Directory, StoreChange.Reset, string.Empty, 0L), (reset.Store, reset.Change, reset.Name, reset.ByteCount));
    }

    [TestMethod]
    public void Reset_AfterAKeptAndAnUnkeptSeed_LeavesTheBuiltEntriesAndTheKeptOnes_EveryTime()
    {
        var (directory, _) = Seeded();
        directory.SeedEntries(AliceLdif, keepOnReset: true);
        directory.SeedEntries(BobLdif);

        directory.Reset();
        Assert.AreEqual("dc=example,dc=com|cn=alice,dc=example,dc=com", HeldDns(directory));

        directory.SeedEntries(BobLdif);
        directory.Reset();
        Assert.AreEqual("dc=example,dc=com|cn=alice,dc=example,dc=com", HeldDns(directory));
    }

    [TestMethod]
    public void SeedEntries_KeptUnderAnUnkeptSeededParent_IsRefusedAndChangesNothing()
    {
        var (directory, events) = Seeded();
        directory.SeedEntries(StaffLdif);

        AssertKeptSeedRefused(
            directory,
            events,
            "dn: cn=carol,ou=staff,dc=example,dc=com\ncn: carol\n",
            "line 1: a parent DN that is not in the file",
            heldAfterReset: "dc=example,dc=com");
    }

    [TestMethod]
    public void SeedEntries_KeptUnderAnUnkeptSeededNamingContext_IsRefusedAndChangesNothing()
    {
        var (directory, events) = Seeded();
        directory.SeedEntries("dn: o=other\no: other\n");

        AssertKeptSeedRefused(
            directory,
            events,
            AliceLdif + "\ndn: cn=carol,o=other\ncn: carol\n",
            "line 5: a parent DN that is not in the file",
            heldAfterReset: "dc=example,dc=com");
    }

    [TestMethod]
    public void SeedEntries_KeptADnAnUnkeptSeedHolds_IsRefusedAndChangesNothing()
    {
        var (directory, events) = Seeded();
        directory.SeedEntries(AliceLdif);

        AssertKeptSeedRefused(directory, events, AliceLdif, "line 1: a duplicate DN", heldAfterReset: "dc=example,dc=com");
    }

    [TestMethod]
    public void SeedEntries_KeptADnResetAlreadyReturnsTo_IsRefusedAndChangesNothing()
    {
        var (directory, events) = Seeded();
        var beforeTheKeptSeed = directory.TakeSnapshot();
        directory.SeedEntries(AliceLdif, keepOnReset: true);
        directory.Restore(beforeTheKeptSeed);

        AssertKeptSeedRefused(
            directory,
            events,
            AliceLdif,
            "line 1: a duplicate DN",
            heldAfterReset: "dc=example,dc=com|cn=alice,dc=example,dc=com");
    }

    [TestMethod]
    public void SeedEntries_KeptPastTheEntryLimit_IsRefusedAndChangesNothing()
    {
        var (directory, events) = Seeded(maxEntries: 2);
        directory.SeedEntries(AliceLdif);

        AssertKeptSeedRefused(directory, events, BobLdif, "line 1: past the directory's bounds", heldAfterReset: "dc=example,dc=com");
    }

    [TestMethod]
    public void SeedEntries_KeptPastTheEntryLimitOfWhatResetReturnsTo_IsRefusedAndChangesNothing()
    {
        var (directory, events) = Seeded(maxEntries: 2);
        var beforeTheKeptSeed = directory.TakeSnapshot();
        directory.SeedEntries(AliceLdif, keepOnReset: true);
        directory.Restore(beforeTheKeptSeed);

        AssertKeptSeedRefused(
            directory,
            events,
            BobLdif,
            "line 1: past the directory's bounds",
            heldAfterReset: "dc=example,dc=com|cn=alice,dc=example,dc=com");
    }

    [TestMethod]
    public void SeedEntries_Kept_PublishesSeededPerEntry_AndResetPublishesResetAlone()
    {
        var (directory, events) = Seeded();

        directory.SeedEntries(AliceLdif + "\n" + BobLdif, keepOnReset: true);

        CollectionAssert.AreEqual(
            new[] { (StoreChange.Seeded, "cn=alice,dc=example,dc=com"), (StoreChange.Seeded, "cn=bob,dc=example,dc=com") },
            events.Events.Cast<StoreChangedEvent>().Select(e => (e.Change, e.Name)).ToArray());

        directory.Reset();

        Assert.HasCount(3, events.Events);
        Assert.AreEqual(StoreChange.Reset, ((StoreChangedEvent)events.Events[^1]).Change);
    }

    [TestMethod]
    public void Reset_AfterRestoringASnapshotTakenBeforeAKeptSeed_ReturnsToTheKeptEntriesToo()
    {
        var (directory, _) = Seeded();
        var beforeTheKeptSeed = directory.TakeSnapshot();
        directory.SeedEntries(AliceLdif, keepOnReset: true);

        directory.Restore(beforeTheKeptSeed);
        Assert.AreEqual("dc=example,dc=com", HeldDns(directory));

        directory.Reset();
        Assert.AreEqual("dc=example,dc=com|cn=alice,dc=example,dc=com", HeldDns(directory));
    }

    [TestMethod]
    public void Restore_GivesBackTheSnapshot_AndPublishesRestored()
    {
        var (directory, events) = Seeded();
        directory.SeedEntries(AliceLdif);
        var snapshot = directory.TakeSnapshot();
        directory.Reset();

        directory.Restore(snapshot);

        Assert.HasCount(2, directory.Entries);
        var restored = (StoreChangedEvent)events.Events[^1];
        Assert.AreEqual(StoreChange.Restored, restored.Change);
        Assert.AreEqual(snapshot.ByteCount, restored.ByteCount);
    }

    [TestMethod]
    public void Restore_ASnapshotFromAnotherDirectory_IsRefused()
    {
        var (directory, _) = Seeded();
        var (other, _) = Seeded();

        Assert.ThrowsExactly<InvalidOperationException>(() => directory.Restore(other.TakeSnapshot()));
        Assert.ThrowsExactly<ArgumentNullException>(() => directory.Restore(null!));
    }
}
