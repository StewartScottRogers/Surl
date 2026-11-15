using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-1166: seeding a running server's Redis keys, LDAP entries and DNS zones, and resetting, snapshotting and
// restoring them (ADR-0158, decision 9), each proved by a fake client of the protocol that reads the store.
public sealed partial class SurlServerTests
{
    private const string RedisUrl = "redis://127.0.0.1:0";
    private const string LdapUrl = "ldap://127.0.0.1:0";
    private const string DnsUrl = "dns://127.0.0.1:0";
    private const string DirectoryLdif = "dn: dc=example,dc=com\nobjectClass: domain\ndc: example\n";
    private const string ExampleZone =
        "$TTL 3600\n@ IN SOA ns.example.com. host.example.com. 1 7200 3600 1209600 300\nwww IN A 192.0.2.1\n";

    // A base search of dc=example,dc=com, filter (objectClass=*), then an unbind, with no bind first.
    private static readonly byte[] LdapSearchThenUnbind = Convert.FromHexString(
        "30840000003E020102638400000035041164633D6578616D706C652C64633D636F6D0A01000A0100020100"
        + "020100010100870B6F626A656374436C617373308400000000"
        + "30840000000502010342" + "00");

    private static readonly byte[] ExampleDn = Encoding.ASCII.GetBytes("dc=example,dc=com");

    // Length 33, id 0x1234, RD, one question: www.example.com. IN A.
    private static readonly byte[] WwwExampleComQuery =
    [
        0, 33, 0x12, 0x34, 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 0,
        3, (byte)'w', (byte)'w', (byte)'w', 7, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e',
        3, (byte)'c', (byte)'o', (byte)'m', 0, 0, 1, 0, 1,
    ];

    private static readonly byte[] ExampleAddress = [192, 0, 2, 1];

    [TestMethod]
    public async Task Keys_SetAsync_TheSeededKeyIsReadOverRedisAndPublishesKeyspaceSeeded()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(LookupOptions(RedisUrl), seams.Host, TestContext.CancellationToken);

        await server.Keys.SetAsync("seeded", "yes", cancellationToken: TestContext.CancellationToken);
        await server.Keys.SetAsync("raw"u8.ToArray(), "bytes"u8.ToArray(), TimeSpan.FromHours(1), 2, TestContext.CancellationToken);
        var answer = await ServeAsync(seams, "redis", "GET seeded\r\nSELECT 2\r\nGET raw\r\nQUIT\r\n");
        var seeded = await WaitForStoreChangeAsync(server, StoreKind.Keyspace);

        StringAssert.Contains(answer, "$3\r\nyes\r\n");
        StringAssert.Contains(answer, "$5\r\nbytes\r\n");
        Assert.AreEqual(StoreChange.Seeded, seeded.Change);
    }

    [TestMethod]
    public async Task Directory_AddEntriesAsync_TheSeededEntryIsFoundOverLdapAndPublishesDirectorySeeded()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(LookupOptions(LdapUrl), seams.Host, TestContext.CancellationToken);

        await server.Directory.AddEntriesAsync(DirectoryLdif, TestContext.CancellationToken);
        var answer = await ServeBytesAsync(seams, "ldap", LdapSearchThenUnbind);
        var seeded = await WaitForStoreChangeAsync(server, StoreKind.Directory);

        Assert.IsTrue(Contains(answer, ExampleDn), Convert.ToHexString(answer));
        Assert.AreEqual(StoreChange.Seeded, seeded.Change);
    }

    [TestMethod]
    public async Task Zones_AddZoneAsync_TheSeededZoneIsAnsweredOverDnsAndPublishesZonesSeeded()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(LookupOptions(DnsUrl), seams.Host, TestContext.CancellationToken);

        await server.Zones.AddZoneAsync("example.com", ExampleZone, TestContext.CancellationToken);
        var answer = await ServeBytesAsync(seams, "dns", WwwExampleComQuery);
        var seeded = await WaitForStoreChangeAsync(server, StoreKind.Zones);

        Assert.IsTrue(Contains(answer, ExampleAddress), Convert.ToHexString(answer));
        Assert.AreEqual(StoreChange.Seeded, seeded.Change);
        Assert.AreEqual("example.com.", seeded.Name);
    }

    [TestMethod]
    public async Task ResetAsync_AfterSeedingKeyEntryAndZone_RemovesAllThree()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(LookupOptions(RedisUrl, LdapUrl, DnsUrl), seams.Host, TestContext.CancellationToken);
        await SeedLookupsAsync(server);

        await server.ResetAsync(TestContext.CancellationToken);
        var redis = await ServeAsync(seams, "redis", "GET seeded\r\nQUIT\r\n");
        var ldap = await ServeBytesAsync(seams, "ldap", LdapSearchThenUnbind);
        var dns = await ServeBytesAsync(seams, "dns", WwwExampleComQuery);

        StringAssert.Contains(redis, "$-1\r\n");
        Assert.IsFalse(Contains(ldap, ExampleDn), Convert.ToHexString(ldap));
        Assert.IsFalse(Contains(dns, ExampleAddress), Convert.ToHexString(dns));
    }

    [TestMethod]
    public async Task RestoreAsync_SnapshotResetRestore_BringsTheKeyEntryAndZoneBack()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(LookupOptions(RedisUrl, LdapUrl, DnsUrl), seams.Host, TestContext.CancellationToken);
        await SeedLookupsAsync(server);

        var snapshot = await server.SnapshotAsync(TestContext.CancellationToken);
        await server.ResetAsync(TestContext.CancellationToken);
        await server.RestoreAsync(snapshot, TestContext.CancellationToken);
        var redis = await ServeAsync(seams, "redis", "GET seeded\r\nQUIT\r\n");
        var ldap = await ServeBytesAsync(seams, "ldap", LdapSearchThenUnbind);
        var dns = await ServeBytesAsync(seams, "dns", WwwExampleComQuery);

        StringAssert.Contains(redis, "$3\r\nyes\r\n");
        Assert.IsTrue(Contains(ldap, ExampleDn), Convert.ToHexString(ldap));
        Assert.IsTrue(Contains(dns, ExampleAddress), Convert.ToHexString(dns));
    }

    [TestMethod]
    [DataRow("key-too-large", "Keyspace", DisplayName = "a key past the size bound")]
    [DataRow("entry-bad", "Directory", DisplayName = "an LDIF change record")]
    [DataRow("zone-bad", "Zones", DisplayName = "text that is not a zone")]
    [DataRow("zone-twice", "Zones", DisplayName = "a second zone for one apex")]
    public async Task Seeding_WhatTheStoreRefuses_ThrowsSurlSeedRefusedExceptionNamingTheStore(string seed, string store)
    {
        var seams = new ApiTestSeams();
        var urls = new[] { RedisUrl, LdapUrl, DnsUrl };
        await using var server = await SurlServer.StartAsync(LookupOptions(urls), seams.Host, TestContext.CancellationToken);
        await server.Zones.AddZoneAsync("example.com", ExampleZone, TestContext.CancellationToken);
        var token = TestContext.CancellationToken;
        Func<Task> seeding = seed switch
        {
            "key-too-large" => () => server.Keys.SetAsync("k"u8.ToArray(), new byte[16 * 1024 * 1024], cancellationToken: token),
            "entry-bad" => () => server.Directory.AddEntriesAsync("dn: dc=x\nchangetype: delete\n", token),
            "zone-bad" => () => server.Zones.AddZoneAsync("other.com", "not a zone", token),
            _ => () => server.Zones.AddZoneAsync("example.com.", ExampleZone, token),
        };

        var exception = await Assert.ThrowsExactlyAsync<SurlSeedRefusedException>(seeding);

        Assert.AreEqual(Enum.Parse<StoreKind>(store), exception.Store);
        StringAssert.StartsWith(exception.Message, $"{store}: ");
    }

    [TestMethod]
    public async Task Keys_SetAsyncOutOfRange_ThrowsArgumentExceptions()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(LookupOptions(RedisUrl), seams.Host, TestContext.CancellationToken);
        var token = TestContext.CancellationToken;

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => server.Keys.SetAsync("k", "v", database: 16, cancellationToken: token));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => server.Keys.SetAsync("k", "v", TimeSpan.Zero, cancellationToken: token));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => server.Keys.SetAsync(string.Empty, "v", cancellationToken: token));
    }

    [TestMethod]
    public async Task LookupSeeds_NullArguments_ThrowArgumentNullException()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(LookupOptions(RedisUrl), seams.Host, TestContext.CancellationToken);
        var token = TestContext.CancellationToken;

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Keys.SetAsync(null!, "v", cancellationToken: token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Keys.SetAsync("k", null!, cancellationToken: token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Directory.AddEntriesAsync(null!, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Zones.AddZoneAsync(null!, ExampleZone, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Zones.AddZoneAsync("example.com", null!, token));
    }

    // Extended servers on, exchanges recorded so a wait can see the store's events.
    private static SurlServerOptions LookupOptions(params string[] listenUrls)
    {
        var options = ApiTestSeams.Options(listenUrls);
        options.AllowAnonymous = true;
        options.AreExtensionsTurnedOn = true;
        options.RecordExchanges = true;
        return options;
    }

    private async Task SeedLookupsAsync(SurlServer server)
    {
        await server.Keys.SetAsync("seeded", "yes", cancellationToken: TestContext.CancellationToken);
        await server.Directory.AddEntriesAsync(DirectoryLdif, TestContext.CancellationToken);
        await server.Zones.AddZoneAsync("example.com", ExampleZone, TestContext.CancellationToken);
    }

    private async Task<StoreChangedEvent> WaitForStoreChangeAsync(SurlServer server, StoreKind store) =>
        (StoreChangedEvent)await server.WaitForEventAsync(
            new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = store }, ExchangeTimeLimit, TestContext.CancellationToken);
}
