namespace Surl.Api;

// BL-1220: reading back the DNS zones (ADR-0164, decision 9).
public sealed partial class SurlServerTests
{
    private const string ZoneToReadBack = "$TTL 60\n@ SOA ns hostmaster 1 2 3 4 30\nwww A 192.0.2.7\n";

    [TestMethod]
    public async Task Zones_ReadAsyncAndListAsync_ReadBackWhatAddZoneAsyncAdded_WithoutPublishingAnything()
    {
        var token = TestContext.CancellationToken;
        await using var server = await SurlServer.StartAsync(LookupOptions(DnsUrl), new ApiTestSeams().Host, token);
        await server.Zones.AddZoneAsync("example.com", ZoneToReadBack, token);
        var before = server.FindEvents(new SurlEventQuery()).Count;

        var text = await server.Zones.ReadAsync("EXAMPLE.com.", token);
        var apexes = await server.Zones.ListAsync(token);

        Assert.AreEqual(
            "example.com. 60 IN SOA ns.example.com. hostmaster.example.com. 1 2 3 4 30\nwww.example.com. 60 IN A 192.0.2.7\n", text);
        CollectionAssert.AreEqual(new[] { "example.com." }, apexes.ToArray());
        Assert.HasCount(before, server.FindEvents(new SurlEventQuery()));
    }

    [TestMethod]
    public async Task Zones_ReadAsync_AnApexNoZoneHasOrNoDnsServer_ThrowsNotFound()
    {
        var token = TestContext.CancellationToken;
        await using var dns = await SurlServer.StartAsync(LookupOptions(DnsUrl), new ApiTestSeams().Host, token);
        await using var redis = await SurlServer.StartAsync(LookupOptions(RedisUrl), new ApiTestSeams().Host, token);

        await Assert.ThrowsExactlyAsync<SurlNotFoundException>(() => dns.Zones.ReadAsync("other.test", token));
        await Assert.ThrowsExactlyAsync<SurlNotFoundException>(() => redis.Zones.ReadAsync("other.test", token));
        Assert.IsEmpty(await redis.Zones.ListAsync(token));
    }

    [TestMethod]
    public async Task Zones_ReadAsyncAndListAsync_NullApexACancelledTokenAndAfterStop_Throw()
    {
        var token = TestContext.CancellationToken;
        var server = await SurlServer.StartAsync(LookupOptions(DnsUrl), new ApiTestSeams().Host, token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Zones.ReadAsync(null!, token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => server.Zones.ReadAsync("a.test", cancelled.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => server.Zones.ListAsync(cancelled.Token));
        _ = await server.StopAsync(token);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => server.Zones.ReadAsync("a.test", token));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => server.Zones.ListAsync(token));
    }
}
