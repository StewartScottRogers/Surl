using Surl.Cli;
using Surl.Control;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// A test reads the DNS zones back as zone-file text through <see cref="ComposedStores"/> and the control target
/// (ADR-0164 decision 9, BL-1220), with no DNS client.
/// </summary>
[TestClass]
public sealed class ComposedStoresZoneReadTests
{
    private const string Zone = "$TTL 60\n@ SOA ns hostmaster 1 2 3 4 30\nwww A 192.0.2.7\n";
    private const string ZoneBack = "example.com. 60 IN SOA ns.example.com. hostmaster.example.com. 1 2 3 4 30\nwww.example.com. 60 IN A 192.0.2.7\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadZoneAndListZoneApexes_ASeededZone_ReadBackAndPublishNothing()
    {
        await using var running = await ComposeAsync();
        await running.Stores.SeedZoneAsync("example.com", Zone, TestContext.CancellationToken);
        await using var subscription = running.Events.Subscribe(new SurlEventSubscriptionOptions());

        var read = running.Stores.ReadZone("Example.COM");
        var apexes = running.Stores.ListZoneApexes();

        Assert.IsTrue(read.IsFound);
        Assert.AreEqual(ZoneBack, read.Value);
        CollectionAssert.AreEqual(new[] { "example.com." }, apexes.ToArray());
        Assert.IsFalse(subscription.TryRead(out _));
    }

    [TestMethod]
    public async Task ReadZone_NoSuchZone_IsNotFound()
    {
        await using var running = await ComposeAsync();

        Assert.AreEqual(ComposedReadOutcome.NotFound, running.Stores.ReadZone("other.test").Outcome);
        Assert.ThrowsExactly<ArgumentNullException>(() => running.Stores.ReadZone(null!));
    }

    [TestMethod]
    public async Task ReadZoneAndListZoneApexes_AnInstanceWithoutDns_AreNotFoundAndEmpty()
    {
        await using var running = await ComposeAsync();
        var without = new ComposedStores(
            running.Stores.Content, running.Stores.MetadataIndex, running.Stores.Mail, running.Stores.Bus, running.Stores.Accounts);

        Assert.AreEqual(ComposedReadOutcome.NotFound, without.ReadZone("example.com").Outcome);
        Assert.IsEmpty(without.ListZoneApexes());
    }

    [TestMethod]
    public async Task ReadZone_MoreThanTheReadBound_IsTooLarge()
    {
        await using var running = await ComposeAsync();
        var records = string.Concat(Enumerable.Range(0, 135_000).Select(index => $"t{index} TXT \"{new string('a', 100)}\"\n"));

        // About 15 MiB of zone file, under the 16 MiB seed limit; written back with absolute names it is over 16 MiB.
        await running.Stores.SeedZoneAsync("big.test", "$TTL 60\n@ SOA ns hostmaster 1 2 3 4 30\n" + records, TestContext.CancellationToken);

        Assert.AreEqual(ComposedReadOutcome.TooLarge, running.Stores.ReadZone("big.test").Outcome);
    }

    [TestMethod]
    public async Task ControlTarget_ReadsZonesBackAndRefusesAnAbsentOne()
    {
        await using var serving = await ComposeAsync();
        var target = new ComposedControlTarget(serving.Events, serving.Faults, serving.Stores);
        await target.SeedZoneAsync("example.com", Zone, TestContext.CancellationToken);

        var text = await target.ReadZoneAsync("example.com", TestContext.CancellationToken);
        var apexes = await target.ListZonesAsync(TestContext.CancellationToken);
        var refusal = await Assert.ThrowsExactlyAsync<ControlRefusalException>(() => target.ReadZoneAsync("other.test", TestContext.CancellationToken));

        Assert.AreEqual(ZoneBack, text);
        CollectionAssert.AreEqual(new[] { "example.com." }, apexes.ToArray());
        Assert.AreEqual(ControlRefusalKind.NotFound, refusal.Kind);
    }

    private async Task<ComposedServing> ComposeAsync()
    {
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", "dns://127.0.0.1:5300"], ["dns"]).CommandLine!,
            new StartTestSeams(new FakeListenerFactory()).Seams with { AreExtensionsTurnedOn = true },
            TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        return start.Serving!;
    }
}
