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
