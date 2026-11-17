using Surl.Cli;
using Surl.Control;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The control endpoint's three seeds over a composed instance (ADR-0158 decision 9, BL-1168): each reaches the Redis
/// keyspace, the LDAP directory or the DNS zones through <see cref="ComposedStores"/>, and each refusal is the
/// <see cref="ControlRefusalKind"/> the ADR names. No network: the instance is composed and never served.
/// </summary>
[TestClass]
public sealed class ComposedControlTargetSeedTests
{
    private const string Ldif = "dn: dc=example,dc=com\nobjectClass: domain\ndc: example\n";
    private const string Zone = "$TTL 3600\n@ IN SOA ns.example.com. host.example.com. 1 7200 3600 1209600 300\nwww IN A 192.0.2.1\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SeedKeyAsync_AKey_IsSeededAndPublishedNamedForItsDatabaseAndKey()
    {
        await using var serving = await ComposeAsync();
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        await TargetOf(serving).SeedKeyAsync(2, "k"u8.ToArray(), "v"u8.ToArray(), TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        Assert.IsTrue(subscription.TryRead(out var seeded));
        var change = (StoreChangedEvent)seeded!;
        Assert.AreEqual(StoreKind.Keyspace, change.Store);
        Assert.AreEqual(StoreChange.Seeded, change.Change);
        Assert.AreEqual("2/k", change.Name);
    }

    [TestMethod]
    public async Task SeedKeyAsync_ADatabaseOutOfRangeOrAnEmptyKey_IsABadRequest()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);

        var database = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => target.SeedKeyAsync(16, "k"u8.ToArray(), "v"u8.ToArray(), null, TestContext.CancellationToken));
        var key = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => target.SeedKeyAsync(0, ReadOnlyMemory<byte>.Empty, "v"u8.ToArray(), null, TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.BadRequest, database.Kind);
        Assert.AreEqual(ControlRefusalKind.BadRequest, key.Kind);
    }

    [TestMethod]
    public async Task SeedKeyAsync_ABoundPassedOrNoKeyspace_IsAConflict()
    {
        await using var serving = await ComposeAsync();
        var without = new ComposedControlTarget(
            serving.Events, serving.Faults, new ComposedStores(serving.Stores.Content, serving.Stores.MetadataIndex, serving.Stores.Mail, serving.Stores.Bus, serving.Stores.Accounts));

        var tooLarge = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => TargetOf(serving).SeedKeyAsync(0, "k"u8.ToArray(), new byte[ControlEndpoint.MaxBodyBytes], null, TestContext.CancellationToken));
        var noKeyspace = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => without.SeedKeyAsync(0, "k"u8.ToArray(), "v"u8.ToArray(), null, TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.Conflict, tooLarge.Kind);
        Assert.AreEqual(ControlRefusalKind.Conflict, noKeyspace.Kind);
    }

    [TestMethod]
    public async Task SeedDirectoryAsync_Records_AreSeededAndPublishedNamedForTheirDn()
    {
        await using var serving = await ComposeAsync();
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        await TargetOf(serving).SeedDirectoryAsync(Ldif, TestContext.CancellationToken);

        Assert.IsTrue(subscription.TryRead(out var seeded));
        var change = (StoreChangedEvent)seeded!;
        Assert.AreEqual(StoreKind.Directory, change.Store);
        Assert.AreEqual("dc=example,dc=com", change.Name);
    }

    [TestMethod]
    public async Task SeedDirectoryAsync_TextTheReaderRefusesOrNoDirectory_IsABadRequestWithTheReadersText()
    {
        await using var serving = await ComposeAsync();
        var without = new ComposedControlTarget(
            serving.Events, serving.Faults, new ComposedStores(serving.Stores.Content, serving.Stores.MetadataIndex, serving.Stores.Mail, serving.Stores.Bus, serving.Stores.Accounts));

        var malformed = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => TargetOf(serving).SeedDirectoryAsync("not ldif at all", TestContext.CancellationToken));
        var noDirectory = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => without.SeedDirectoryAsync(Ldif, TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.BadRequest, malformed.Kind);
        StringAssert.Contains(malformed.Message, "line 1");
        Assert.AreEqual(ControlRefusalKind.BadRequest, noDirectory.Kind);
    }

    [TestMethod]
    public async Task SeedZoneAsync_AZone_IsSeededAndPublishedNamedForItsApex()
    {
        await using var serving = await ComposeAsync();
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        await TargetOf(serving).SeedZoneAsync("example.com", Zone, TestContext.CancellationToken);

        Assert.IsTrue(subscription.TryRead(out var seeded));
        var change = (StoreChangedEvent)seeded!;
        Assert.AreEqual(StoreKind.Zones, change.Store);
        Assert.AreEqual("example.com.", change.Name);
    }

    [TestMethod]
    public async Task SeedZoneAsync_AZoneFileTheReaderRefuses_IsABadRequest()
    {
        await using var serving = await ComposeAsync();

        var refusal = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => TargetOf(serving).SeedZoneAsync("example.com", "this is not a zone file", TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.BadRequest, refusal.Kind);
    }

    [TestMethod]
    public async Task SeedZoneAsync_ASecondZoneForAnApexOrNoZones_IsAConflict()
    {
        await using var serving = await ComposeAsync();
        var target = TargetOf(serving);
        await target.SeedZoneAsync("example.com", Zone, TestContext.CancellationToken);
        var without = new ComposedControlTarget(
            serving.Events, serving.Faults, new ComposedStores(serving.Stores.Content, serving.Stores.MetadataIndex, serving.Stores.Mail, serving.Stores.Bus, serving.Stores.Accounts));

        var second = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => target.SeedZoneAsync("example.com", Zone, TestContext.CancellationToken));
        var noZones = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => without.SeedZoneAsync("example.com", Zone, TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.Conflict, second.Kind);
        Assert.AreEqual(ControlRefusalKind.Conflict, noZones.Kind);
    }

    [TestMethod]
    public async Task SeedZoneAsync_ZoneTextPastTheSeedBound_IsAConflict()
    {
        await using var serving = await ComposeAsync();
        var text = new string('a', Surl.Extended.Protocol.DnsQuery.DnsZoneSet.MaxSeedTextBytes + 1);

        var refusal = await Assert.ThrowsExactlyAsync<ControlRefusalException>(
            () => TargetOf(serving).SeedZoneAsync("example.com", text, TestContext.CancellationToken));

        Assert.AreEqual(ControlRefusalKind.Conflict, refusal.Kind);
        StringAssert.Contains(refusal.Message, "past the zones' bounds");
    }

    private static ComposedControlTarget TargetOf(ComposedServing serving) => new(serving.Events, serving.Faults, serving.Stores);

    private async Task<ComposedServing> ComposeAsync()
    {
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", "redis://127.0.0.1:6379", "dns://127.0.0.1:5300", "ldap://127.0.0.1:3389"], ["redis", "dns"]).CommandLine!,
            new StartTestSeams(new FakeListenerFactory()).Seams with { AreExtensionsTurnedOn = true },
            TestContext.CancellationToken);
        Assert.IsNull(start.Failure);

        return start.Serving!;
    }
}
