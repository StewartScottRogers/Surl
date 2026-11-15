using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.DnsQuery.DnsTestZones;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Seeding, resetting, snapshotting and restoring the zones, and the events each publishes (ADR-0158 decisions 2 to 5).
/// </summary>
[TestClass]
public sealed class DnsZoneSetSeedResetSnapshotTests
{
    private const string Seeded = "$TTL 60\n@ SOA ns hostmaster 1 2 3 4 30\nwww A 192.0.2.7";

    private static (DnsZoneSet Zones, RecordingSurlEventPublisher Events) Make()
    {
        var events = new RecordingSurlEventPublisher();
        return (new DnsZoneSet(events: events), events);
    }

    private static StoreChangedEvent Only(RecordingSurlEventPublisher events) =>
        (StoreChangedEvent)events.Events.Single();

    [TestMethod]
    public void SeedZone_ZoneFileText_IsAnsweredAuthoritativelyForItsNames()
    {
        var (zones, _) = Make();

        zones.SeedZone("seeded.test", Seeded);

        var answer = zones.Answer(Question("www.seeded.test", DnsRecordType.A));
        Assert.AreEqual(DnsResponseCode.NoError, answer.ResponseCode);
        Assert.AreEqual("192.0.2.7", ((DnsAddressData)answer.Answers.Single().Data).Address.ToString());
    }

    [TestMethod]
    public void SeedZone_ApexWithFinalDot_IsTheSameApex()
    {
        var (zones, _) = Make();

        zones.SeedZone("seeded.test.", Seeded);

        Assert.AreEqual("seeded.test.", zones.Apexes.Single().ToString());
    }

    [TestMethod]
    public void SeedZone_Seeded_PublishesSeededNamedForTheApexWithTheZonesBytes()
    {
        var (zones, events) = Make();

        zones.SeedZone("seeded.test", Seeded);

        var change = Only(events);
        Assert.AreEqual(StoreKind.Zones, change.Store);
        Assert.AreEqual(StoreChange.Seeded, change.Change);
        Assert.AreEqual("seeded.test.", change.Name);
        Assert.IsGreaterThan(0L, change.ByteCount!.Value);
        Assert.IsNull(change.ExchangeId);
    }

    [TestMethod]
    public void SeedZone_TextThatDoesNotParse_IsRefusedWithTheParsersMessageAndChangesNothing()
    {
        var (zones, events) = Make();

        var thrown = Assert.ThrowsExactly<DnsZoneFileException>(() => zones.SeedZone("seeded.test", "www A 192.0.2.7"));

        Assert.AreEqual(FaultOf("seeded.test.zone", Encoding.UTF8.GetBytes("www A 192.0.2.7")), thrown.Message);
        Assert.IsEmpty(zones.Apexes);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public void SeedZone_PastTheZoneBound_IsRefusedAndChangesNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var zones = new DnsZoneSet(maxZones: 1, events: events);
        zones.SeedZone("a.test", Seeded);

        Assert.ThrowsExactly<DnsZoneFileException>(() => zones.SeedZone("b.test", Seeded));

        Assert.AreEqual("a.test.", zones.Apexes.Single().ToString());
        Assert.HasCount(1, events.Events);
    }

    [TestMethod]
    public void SeedZone_TextOverSixteenMebibytes_IsRefused()
    {
        var (zones, _) = Make();
        var text = Seeded + "\n; " + new string('x', DnsZoneSet.MaxSeedTextBytes);

        var thrown = Assert.ThrowsExactly<DnsZoneFileException>(() => zones.SeedZone("seeded.test", text));

        StringAssert.Contains(thrown.Message, "bounds");
        Assert.IsEmpty(zones.Apexes);
    }

    [TestMethod]
    public void SeedZone_ApexAlreadyHeld_IsRefusedAsASecondZoneAndKeepsTheFirst()
    {
        var (zones, events) = Make();
        zones.SeedZone("seeded.test", Seeded);

        var thrown = Assert.ThrowsExactly<DnsZoneFileException>(() =>
            zones.SeedZone("seeded.test", "$TTL 60\n@ SOA ns hostmaster 1 2 3 4 30\nwww A 192.0.2.99"));

        StringAssert.Contains(thrown.Message, "a second zone for seeded.test.");
        Assert.AreEqual("192.0.2.7", ((DnsAddressData)zones.Answer(Question("www.seeded.test", DnsRecordType.A)).Answers.Single().Data).Address.ToString());
        Assert.HasCount(1, events.Events);
    }

    [TestMethod]
    public void SeedZone_NullArguments_Throw()
    {
        var (zones, _) = Make();

        Assert.ThrowsExactly<ArgumentNullException>(() => zones.SeedZone(null!, Seeded));
        Assert.ThrowsExactly<ArgumentNullException>(() => zones.SeedZone("a.test", null!));
    }

    [TestMethod]
    public void Reset_AfterSeeds_ReturnsToTheZonesTheSetWasBuiltWith()
    {
        var (zones, events) = Make();
        zones.Add("loaded.test.zone", Encoding.UTF8.GetBytes(Seeded));
        zones.SeedZone("seeded.test", Seeded);

        zones.Reset();

        Assert.AreEqual("loaded.test.", zones.Apexes.Single().ToString());
        Assert.AreEqual(DnsResponseCode.Refused, zones.Answer(Question("www.seeded.test", DnsRecordType.A)).ResponseCode);
        var change = (StoreChangedEvent)events.Events.Last();
        Assert.AreEqual(StoreChange.Reset, change.Change);
        Assert.AreEqual(StoreKind.Zones, change.Store);
        Assert.AreEqual(string.Empty, change.Name);
        Assert.AreEqual(0L, change.ByteCount);
    }

    [TestMethod]
    public void Reset_SeedKeptOnReset_StaysAndCanBeSeededAgainNever()
    {
        var (zones, _) = Make();
        zones.SeedZone("kept.test", Seeded, keepOnReset: true);
        zones.SeedZone("dropped.test", Seeded);

        zones.Reset();

        Assert.AreEqual("kept.test.", zones.Apexes.Single().ToString());
        Assert.ThrowsExactly<DnsZoneFileException>(() => zones.SeedZone("kept.test", Seeded));
    }

    [TestMethod]
    public void Add_ApexOfAConfiguredZoneLostByRestore_IsRefused()
    {
        var (zones, _) = Make();
        var empty = zones.TakeSnapshot();
        zones.Add("loaded.test.zone", Encoding.UTF8.GetBytes(Seeded));
        zones.Restore(empty);

        var thrown = Assert.ThrowsExactly<DnsZoneFileException>(() => zones.Add("loaded.test.zone", Encoding.UTF8.GetBytes(Seeded)));

        StringAssert.Contains(thrown.Message, "a second zone");
    }

    [TestMethod]
    public void Restore_SnapshotChangeRestore_GivesBackTheSnapshotsZones()
    {
        var (zones, events) = Make();
        zones.SeedZone("one.test", Seeded);
        var snapshot = zones.TakeSnapshot();
        zones.SeedZone("two.test", Seeded);

        zones.Restore(snapshot);

        Assert.AreEqual("one.test.", zones.Apexes.Single().ToString());
        var change = (StoreChangedEvent)events.Events.Last();
        Assert.AreEqual(StoreChange.Restored, change.Change);
        Assert.AreEqual(string.Empty, change.Name);
        Assert.AreEqual(snapshot.ByteCount, change.ByteCount);
        Assert.IsGreaterThan(0L, snapshot.ByteCount);
    }

    [TestMethod]
    public void TakeSnapshot_PublishesNothing()
    {
        var (zones, events) = Make();

        zones.TakeSnapshot();

        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public void Restore_SnapshotFromAnotherSet_IsRefused()
    {
        var (zones, _) = Make();
        var other = new DnsZoneSet().TakeSnapshot();

        Assert.ThrowsExactly<ArgumentException>(() => zones.Restore(other));
        Assert.ThrowsExactly<ArgumentNullException>(() => zones.Restore(null!));
    }

    [TestMethod]
    public void TakeSnapshot_HoldingMoreThanMaxBytes_IsRefused()
    {
        var (zones, _) = Make();
        zones.SeedZone("one.test", Seeded);

        Assert.ThrowsExactly<InvalidOperationException>(() => zones.TakeSnapshot(1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => zones.TakeSnapshot(-1));
    }

    [TestMethod]
    public void Answer_QueryTakenBeforeAChange_SeesTheZonesItStartedWith()
    {
        var (zones, _) = Make();
        zones.SeedZone("one.test", Seeded);
        var before = zones.CurrentView;

        zones.Reset();

        Assert.IsNotNull(before.ZoneFor(DnsName.Parse("www.one.test")));
        Assert.IsNull(zones.CurrentView.ZoneFor(DnsName.Parse("www.one.test")));
    }
}
