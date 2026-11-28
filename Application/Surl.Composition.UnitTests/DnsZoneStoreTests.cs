using System.Text;
using Surl.Cli;
using Surl.Core;
using Surl.Extended.Protocol.DnsQuery;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The DNS zones under whole-instance seed, reset, snapshot and restore, shared by the instance's <c>dns</c>
/// server (ADR-0158 decisions 3 to 6; BL-1165), over <see cref="FakeListenerFactory"/> with no network. The
/// exchanges are DNS over TCP, because the fake listener feeds connections only; the zone set a UDP query reads is
/// the same one.
/// </summary>
[TestClass]
public sealed class DnsZoneStoreTests
{
    private const string ListenUrl = "dns://127.0.0.1:5300";
    private const string ExampleZone =
        "$TTL 3600\n@ IN SOA ns.example.com. host.example.com. 1 7200 3600 1209600 300\nwww IN A 192.0.2.1\n";

    private const int Refused = 5;

    // Length 33, id 0x1234, RD, one question: www.example.com. IN A.
    private static readonly byte[] WwwExampleComQuery =
    [
        0, 33, 0x12, 0x34, 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 0,
        3, (byte)'w', (byte)'w', (byte)'w', 7, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e',
        3, (byte)'c', (byte)'o', (byte)'m', 0, 0, 1, 0, 1,
    ];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Stores_SeedZoneAsync_IsAnsweredOverDnsOfTheSameInstanceAndByNoOtherInstance()
    {
        var (first, firstFactory) = await ComposeAsync();
        var (second, secondFactory) = await ComposeAsync();
        await using var running = first;
        await using var other = second;

        await running.Stores.SeedZoneAsync("example.com", ExampleZone, TestContext.CancellationToken);
        var answer = await QueryAsync(running, firstFactory);
        var otherAnswer = await QueryAsync(other, secondFactory);

        Assert.AreEqual(0, answer[5] & 0x0F);
        Assert.AreEqual(1, answer[9]);
        CollectionAssert.AreEqual(new byte[] { 192, 0, 2, 1 }, answer[^4..]);
        Assert.AreEqual(Refused, otherAnswer[5] & 0x0F);
    }

    [TestMethod]
    public async Task Stores_ResetAsync_DropsASeededZoneAndRestoreBringsItBack()
    {
        var (serving, factory) = await ComposeAsync();
        await using var running = serving;
        await running.Stores.SeedZoneAsync("example.com.", ExampleZone, TestContext.CancellationToken);
        var snapshot = await running.Stores.TakeSnapshotAsync(TestContext.CancellationToken);

        await running.Stores.ResetAsync(TestContext.CancellationToken);
        var afterReset = await QueryAsync(running, factory);
        await running.Stores.RestoreAsync(snapshot, TestContext.CancellationToken);
        var afterRestore = await QueryAsync(running, factory);

        Assert.AreEqual(Refused, afterReset[5] & 0x0F);
        Assert.AreEqual(0, afterRestore[5] & 0x0F);
        CollectionAssert.AreEqual(new byte[] { 192, 0, 2, 1 }, afterRestore[^4..]);
        CollectionAssert.AreEqual(
            new[] { StoreKind.Content, StoreKind.Mail, StoreKind.Bus, StoreKind.Accounts, StoreKind.Exchanges, StoreKind.Producers, StoreKind.Sessions, StoreKind.Zones, StoreKind.Uploads },
            running.Stores.RegisteredStoreOrder.ToArray());
    }

    [TestMethod]
    public async Task Stores_ResetAsync_KeepsAZoneSeededToBeKeptAndDropsOneSeededWithoutTheFlag()
    {
        var (serving, _) = await ComposeAsync();
        await using var running = serving;
        await running.Stores.SeedZoneAsync("example.com", ExampleZone, keepOnReset: true, TestContext.CancellationToken);
        await running.Stores.SeedZoneAsync(
            "dropped.test",
            ExampleZone.Replace("example.com", "dropped.test", StringComparison.Ordinal),
            TestContext.CancellationToken);

        await running.Stores.ResetAsync(StoreKind.Zones, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "example.com." }, running.Stores.ListZoneApexes().ToArray());
    }

    [TestMethod]
    public async Task Registration_Reset_ReturnsToTheZonesLoadedFromTheDirectory()
    {
        var zones = new DnsZoneSet();
        zones.Add("example.com.zone", Encoding.UTF8.GetBytes(ExampleZone));
        zones.SeedZone("seeded.test", ExampleZone.Replace("example.com", "seeded.test", StringComparison.Ordinal));
        var registration = DnsZoneStoreRegistration.For(zones);

        await registration.ResetAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "example.com." }, zones.Apexes.Select(apex => apex.ToString()).ToArray());
    }

    [TestMethod]
    public async Task Stores_SeedZoneAsync_PublishesItsChangesOnTheInstancesStream()
    {
        var (serving, _) = await ComposeAsync();
        await using var running = serving;
        await using var subscription = running.Events.Subscribe(new SurlEventSubscriptionOptions());

        await running.Stores.SeedZoneAsync("example.com", ExampleZone, TestContext.CancellationToken);
        var snapshot = await running.Stores.TakeSnapshotAsync(StoreKind.Zones, TestContext.CancellationToken);
        await running.Stores.ResetAsync(StoreKind.Zones, TestContext.CancellationToken);
        await running.Stores.RestoreAsync(snapshot, TestContext.CancellationToken);

        List<(StoreChange Change, string Name)> changes = [];
        while (subscription.TryRead(out var surlEvent))
        {
            var changed = (StoreChangedEvent)surlEvent!;
            changes.Add((changed.Change, changed.Name));
        }

        CollectionAssert.AreEqual(
            new[] { (StoreChange.Seeded, "example.com."), (StoreChange.Reset, string.Empty), (StoreChange.Restored, string.Empty) },
            changes);
    }

    [TestMethod]
    public async Task Stores_SeedZoneAsync_RefusesACancelledTokenAndAnInstanceWithoutDns()
    {
        var (serving, _) = await ComposeAsync();
        await using var running = serving;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var without = new ComposedStores(
            running.Stores.Content, running.Stores.MetadataIndex, running.Stores.Mail, running.Stores.Bus, running.Stores.Accounts);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => running.Stores.SeedZoneAsync("example.com", ExampleZone, cancelled.Token));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => without.SeedZoneAsync("example.com", ExampleZone, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ComposeAsync_WithExtensionsOff_RegistersNoZones()
    {
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", "http://127.0.0.1:0"]).CommandLine!,
            new StartTestSeams(new FakeListenerFactory()).Seams with { AreExtensionsTurnedOn = false },
            TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        await using var running = start.Serving!;

        CollectionAssert.DoesNotContain(running.Stores.RegisteredStoreOrder.ToArray(), StoreKind.Zones);
    }

    private async Task<(ComposedServing Serving, FakeListenerFactory Factory)> ComposeAsync()
    {
        var factory = new FakeListenerFactory();
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", ListenUrl], ["dns"]).CommandLine!,
            new StartTestSeams(factory).Seams with { AreExtensionsTurnedOn = true },
            TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        return (start.Serving!, factory);
    }

    private async Task<byte[]> QueryAsync(ComposedServing serving, FakeListenerFactory factory)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = serving.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        var connection = new FakeConnection(WwwExampleComQuery);
        await factory.ConnectionsFor(ListenUrl).WriteAsync(connection, TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;
        return connection.WrittenBytes;
    }
}
