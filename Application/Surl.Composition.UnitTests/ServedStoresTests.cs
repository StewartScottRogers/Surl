using System.Text;
using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The Redis keyspace, the DNS zones and the LDAP directory exist on an instance only when a listen URL serves them
/// (ADR-0158 Amendment 1, BL-1410), so a seed of a store nobody serves is refused with the store named.
/// </summary>
[TestClass]
public sealed class ServedStoresTests
{
    private const string Zone =
        "$TTL 3600\n@ IN SOA ns.example.com. host.example.com. 1 7200 3600 1209600 300\nwww IN A 192.0.2.1\n";

    private const string Ldif = "dn: dc=example,dc=com\nobjectClass: domain\ndc: example\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SeedKeyAsync_WithOnlyAnHttpListenUrl_IsRefusedAsHoldingNoKeyspace()
    {
        await using var serving = await StartAsync("http://127.0.0.1:0/");

        var refusal = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => SeedKeyAsync(serving));

        StringAssert.Contains(refusal.Message, "Redis keyspace");
    }

    [TestMethod]
    public async Task SeedKeyAsync_WithARedisListenUrl_Seeds()
    {
        await using var serving = await StartAsync("http://127.0.0.1:0/", "redis://127.0.0.1:6379/");

        await SeedKeyAsync(serving);
    }

    [TestMethod]
    public async Task SeedZoneAsync_WithOnlyAnHttpListenUrl_IsRefusedAsHoldingNoZones()
    {
        await using var serving = await StartAsync("http://127.0.0.1:0/");

        var refusal = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => SeedZoneAsync(serving));

        StringAssert.Contains(refusal.Message, "DNS zones");
    }

    [TestMethod]
    public async Task SeedZoneAsync_WithADnsListenUrl_Seeds()
    {
        await using var serving = await StartAsync("http://127.0.0.1:0/", "dns://127.0.0.1:5300/");

        await SeedZoneAsync(serving);
    }

    [TestMethod]
    public async Task SeedEntriesAsync_WithOnlyAnHttpListenUrl_IsRefusedAsHoldingNoDirectory()
    {
        await using var serving = await StartAsync("http://127.0.0.1:0/");

        var refusal = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => serving.Stores.SeedEntriesAsync(Ldif, TestContext.CancellationToken));

        StringAssert.Contains(refusal.Message, "LDAP directory");
    }

    [TestMethod]
    public async Task SeedEntriesAsync_WithAnLdapListenUrl_Seeds()
    {
        await using var serving = await StartAsync("http://127.0.0.1:0/", "ldap://127.0.0.1:0/");

        await serving.Stores.SeedEntriesAsync(Ldif, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task SeedKeyAsync_KeptOnResetWithOnlyAnHttpListenUrl_IsRefusedAsHoldingNoKeyspace()
    {
        await using var serving = await StartAsync("http://127.0.0.1:0/");

        var refusal = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => serving.Stores.SeedKeyAsync(0, new byte[1], new byte[1], null, keepOnReset: true, TestContext.CancellationToken));

        Assert.AreEqual("This instance holds no Redis keyspace.", refusal.Message);
    }

    [TestMethod]
    public async Task SeedEntriesAsync_KeptOnResetWithOnlyAnHttpListenUrl_IsRefusedAsHoldingNoDirectory()
    {
        await using var serving = await StartAsync("http://127.0.0.1:0/");

        var refusal = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => serving.Stores.SeedEntriesAsync(Ldif, keepOnReset: true, TestContext.CancellationToken));

        Assert.AreEqual("This instance holds no LDAP directory.", refusal.Message);
    }

    [TestMethod]
    public async Task SeedZoneAsync_KeptOnResetWithOnlyAnHttpListenUrl_IsRefusedAsHoldingNoZones()
    {
        await using var serving = await StartAsync("http://127.0.0.1:0/");

        var refusal = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => serving.Stores.SeedZoneAsync("example.com.", Zone, keepOnReset: true, TestContext.CancellationToken));

        Assert.AreEqual("This instance holds no DNS zones.", refusal.Message);
    }

    [TestMethod]
    public async Task RegisteredStoreOrder_ForAnHttpOnlyInstance_HoldsNoKeyspaceDirectoryOrZones()
    {
        await using var serving = await StartAsync("http://127.0.0.1:0/");

        CollectionAssert.DoesNotContain(serving.Stores.RegisteredStoreOrder.ToArray(), StoreKind.Keyspace);
        CollectionAssert.DoesNotContain(serving.Stores.RegisteredStoreOrder.ToArray(), StoreKind.Directory);
        CollectionAssert.DoesNotContain(serving.Stores.RegisteredStoreOrder.ToArray(), StoreKind.Zones);
    }

    [TestMethod]
    public async Task RegisteredStoreOrder_ForAnInstanceServingRedisDnsAndLdap_HoldsAllThree()
    {
        await using var serving = await StartAsync("redis://127.0.0.1:6379/", "dns://127.0.0.1:5300/", "ldap://127.0.0.1:3389/");

        var order = serving.Stores.RegisteredStoreOrder.ToArray();
        CollectionAssert.Contains(order, StoreKind.Keyspace);
        CollectionAssert.Contains(order, StoreKind.Directory);
        CollectionAssert.Contains(order, StoreKind.Zones);
    }

    private async Task<ComposedServing> StartAsync(params string[] listenUrls)
    {
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(["--allow-anonymous", .. listenUrls], ["redis", "dns"]).CommandLine!, new StartTestSeams().Seams with { AreExtensionsTurnedOn = true }, TestContext.CancellationToken);
        return start.Serving ?? throw new AssertFailedException("The start was refused: " + string.Join(" ", start.Failure?.Lines ?? []));
    }

    private Task SeedKeyAsync(ComposedServing serving) =>
        serving.Stores.SeedKeyAsync(0, Encoding.ASCII.GetBytes("k"), Encoding.ASCII.GetBytes("v"), null, TestContext.CancellationToken);

    private Task SeedZoneAsync(ComposedServing serving) =>
        serving.Stores.SeedZoneAsync("example.com.", Zone, TestContext.CancellationToken);
}
