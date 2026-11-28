using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-2031: the Redis keys, LDAP entries and DNS zones a test gives in SurlServerOptions are held before any
// listener binds and are what a reset returns to (ADR-0158, decisions 3, 4 and 9, and Amendment 1, A2).
public sealed partial class SurlServerTests
{
    private const string RuntimeLdif = "dn: dc=runtime,dc=test\nobjectClass: domain\ndc: runtime\n";
    private const string RuntimeZone =
        "$TTL 3600\n@ IN SOA ns.example.org. host.example.org. 1 7200 3600 1209600 300\nwww IN A 192.0.2.2\n";

    private const string ApiDataDirectory = "data";

    [TestMethod]
    public async Task StartAsync_NoStartTimeSeed_HoldsNothingAndPublishesNoSeed()
    {
        var seams = new ApiTestSeams();

        await using var server = await SurlServer.StartAsync(
            LookupOptions(RedisUrl, LdapUrl, DnsUrl), seams.Host, TestContext.CancellationToken);

        Assert.IsEmpty((await server.Keys.ListAsync("*", cancellationToken: TestContext.CancellationToken)).Keys);
        Assert.DoesNotContain("dn:", await server.Directory.ExportLdifAsync(TestContext.CancellationToken));
        Assert.IsEmpty(await server.Zones.ListAsync(TestContext.CancellationToken));
        Assert.IsEmpty(SeedsOf(server));
    }

    [TestMethod]
    public async Task StartAsync_RedisKeys_AreReadThroughKeysAndPublishKeyspaceSeeded()
    {
        var seams = new ApiTestSeams();
        var options = LookupOptions(RedisUrl);
        options.TimeProvider = new ManualTimeProvider();
        options.RedisKeys =
        [
            new SurlRedisKey("start", "yes"),
            new SurlRedisKey("raw"u8.ToArray(), "bytes"u8.ToArray(), TimeSpan.FromHours(1), 15),
        ];

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var start = await server.Keys.GetAsync("start", cancellationToken: TestContext.CancellationToken);
        var raw = await server.Keys.GetAsync("raw", 15, TestContext.CancellationToken);

        Assert.AreEqual("yes", start.Text);
        Assert.IsNull(start.Info.ExpiresAt);
        Assert.AreEqual("bytes", raw.Text);
        Assert.AreEqual(options.TimeProvider.GetUtcNow() + TimeSpan.FromHours(1), raw.Info.ExpiresAt);
        CollectionAssert.AreEqual(
            new[] { (StoreKind.Keyspace, "0/start"), (StoreKind.Keyspace, "15/raw") }, SeedsOf(server));
    }

    [TestMethod]
    public async Task StartAsync_DirectoryLdif_IsReadThroughDirectoryAndPublishesDirectorySeeded()
    {
        var seams = new ApiTestSeams();
        var options = LookupOptions(LdapUrl);
        options.DirectoryLdif = DirectoryLdif;

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var ldif = await server.Directory.ExportLdifAsync(TestContext.CancellationToken);

        StringAssert.Contains(ldif, "dn: dc=example,dc=com");
        CollectionAssert.AreEqual(new[] { (StoreKind.Directory, "dc=example,dc=com") }, SeedsOf(server));
    }

    [TestMethod]
    public async Task StartAsync_DnsZones_AreReadThroughZonesAndPublishZonesSeeded()
    {
        var seams = new ApiTestSeams();
        var options = LookupOptions(DnsUrl);
        options.DnsZones = [new SurlDnsZone("example.com", ExampleZone)];

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var apexes = await server.Zones.ListAsync(TestContext.CancellationToken);
        var zone = await server.Zones.ReadAsync("example.com", TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "example.com." }, apexes.ToArray());
        StringAssert.Contains(zone, "192.0.2.1");
        CollectionAssert.AreEqual(new[] { (StoreKind.Zones, "example.com.") }, SeedsOf(server));
    }

    [TestMethod]
    public async Task StartAsync_AllThreeSeeds_AreAppliedKeysThenDirectoryThenZones()
    {
        var seams = new ApiTestSeams();

        await using var server = await SurlServer.StartAsync(SeededLookupOptions(), seams.Host, TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { StoreKind.Keyspace, StoreKind.Directory, StoreKind.Zones },
            SeedsOf(server).Select(seed => seed.Store).ToArray());
    }

    [TestMethod]
    public async Task ResetAsync_AfterRuntimeSeeds_LeavesExactlyTheStartTimeSeeds()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(SeededLookupOptions(), seams.Host, TestContext.CancellationToken);
        await server.Keys.SetAsync("start", "changed", cancellationToken: TestContext.CancellationToken);
        await server.Keys.SetAsync("runtime", "yes", cancellationToken: TestContext.CancellationToken);
        await server.Directory.AddEntriesAsync(RuntimeLdif, TestContext.CancellationToken);
        await server.Zones.AddZoneAsync("example.org", RuntimeZone, TestContext.CancellationToken);

        await server.ResetAsync(TestContext.CancellationToken);
        var keys = await server.Keys.ListAsync("*", cancellationToken: TestContext.CancellationToken);
        var start = await server.Keys.GetAsync("start", cancellationToken: TestContext.CancellationToken);
        var ldif = await server.Directory.ExportLdifAsync(TestContext.CancellationToken);
        var apexes = await server.Zones.ListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, keys.Keys);
        Assert.AreEqual("yes", start.Text);
        StringAssert.Contains(ldif, "dn: dc=example,dc=com");
        Assert.DoesNotContain("dc=runtime", ldif);
        CollectionAssert.AreEqual(new[] { "example.com." }, apexes.ToArray());
    }

    [TestMethod]
    [DataRow("key-too-large", "Keyspace", "A key in database 0", DisplayName = "a key past the size bound")]
    [DataRow("entry-change", "Directory", "The LDIF", DisplayName = "an LDIF change record")]
    [DataRow("entry-twice", "Directory", "The LDIF", DisplayName = "a second entry for one DN")]
    [DataRow("zone-bad", "Zones", "The zone other.com", DisplayName = "text that is not a zone")]
    [DataRow("zone-twice", "Zones", "The zone example.com.", DisplayName = "a second zone for one apex")]
    public async Task StartAsync_ARefusedStartTimeSeed_ThrowsSurlSeedRefusedExceptionAndBindsAndLocksNothing(
        string seed, string store, string what)
    {
        var seams = new ApiTestSeams();
        var options = LookupOptions(RedisUrl, LdapUrl, DnsUrl);
        options.DataDirectory = ApiDataDirectory;
        AddRefusedSeed(options, seed);

        var exception = await Assert.ThrowsExactlyAsync<SurlSeedRefusedException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(Enum.Parse<StoreKind>(store), exception.Store);
        StringAssert.StartsWith(exception.Message, $"{store}: {what} was refused: ");
        Assert.AreEqual(0, seams.ListenerFactory.StartedListenerCount);
        Assert.IsFalse(seams.IsHeld(ApiDataDirectory));
    }

    [TestMethod]
    [DataRow("key", "Keyspace", true, DisplayName = "Redis keys with no redis listen URL")]
    [DataRow("key", "Keyspace", false, DisplayName = "Redis keys with the extended servers off")]
    [DataRow("entry", "Directory", true, DisplayName = "LDIF with no ldap listen URL")]
    [DataRow("zone", "Zones", true, DisplayName = "a DNS zone with no dns listen URL")]
    [DataRow("zone", "Zones", false, DisplayName = "a DNS zone with the extended servers off")]
    public async Task StartAsync_ASeedOfAStoreNoListenUrlServes_ThrowsSurlSeedRefusedExceptionAndBindsAndLocksNothing(
        string seed, string store, bool areExtensionsTurnedOn)
    {
        var seams = new ApiTestSeams();
        var options = LookupOptions("http://127.0.0.1:0");
        options.AreExtensionsTurnedOn = areExtensionsTurnedOn;
        options.DataDirectory = ApiDataDirectory;
        AddServableSeed(options, seed);

        var exception = await Assert.ThrowsExactlyAsync<SurlSeedRefusedException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(Enum.Parse<StoreKind>(store), exception.Store);
        StringAssert.Contains(exception.Message, "This instance holds no ");
        Assert.AreEqual(0, seams.ListenerFactory.StartedListenerCount);
        Assert.IsFalse(seams.IsHeld(ApiDataDirectory));
    }

    [TestMethod]
    [DataRow("null-key-item", DisplayName = "a null Redis key")]
    [DataRow("empty-key", DisplayName = "an empty key")]
    [DataRow("database-below", DisplayName = "database -1")]
    [DataRow("database-above", DisplayName = "database 16")]
    [DataRow("expiry-below", DisplayName = "an expiry under 1 millisecond")]
    [DataRow("expiry-above", DisplayName = "an expiry over 365 days")]
    [DataRow("null-zone-item", DisplayName = "a null DNS zone")]
    [DataRow("null-apex", DisplayName = "a null apex")]
    [DataRow("null-zone-text", DisplayName = "a null zone text")]
    public async Task StartAsync_AMisusedStartTimeSeed_ThrowsArgumentExceptionBeforeAnyListenerFactoryIsMade(string misuse)
    {
        var factoriesMade = 0;
        var seams = new ApiTestSeams();
        var host = seams.Host with
        {
            CreateListenerFactory = (_, _) =>
            {
                factoriesMade++;
                return seams.ListenerFactory;
            },
        };
        var options = LookupOptions(RedisUrl, DnsUrl);
        AddMisusedSeed(options, misuse);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => SurlServer.StartAsync(options, host, TestContext.CancellationToken));

        Assert.AreEqual(0, factoriesMade);
    }

    [TestMethod]
    public async Task StartAsync_CancelledBeforeTheSeedsAreApplied_ThrowsOperationCanceledAndBindsNothing()
    {
        var seams = new ApiTestSeams();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => SurlServer.StartAsync(SeededLookupOptions(), seams.Host, cancelled.Token));

        Assert.AreEqual(0, seams.ListenerFactory.StartedListenerCount);
    }

    [TestMethod]
    public async Task StartAsync_TwoSeedsOfOneKey_TheLaterIsHeldAndReturnedToByAReset()
    {
        var seams = new ApiTestSeams();
        var options = LookupOptions(RedisUrl);
        options.RedisKeys = [new SurlRedisKey("k", "first"), new SurlRedisKey("k", "second")];

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);
        var atStart = await server.Keys.GetAsync("k", cancellationToken: TestContext.CancellationToken);
        await server.ResetAsync(TestContext.CancellationToken);
        var afterReset = await server.Keys.GetAsync("k", cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual("second", atStart.Text);
        Assert.AreEqual("second", afterReset.Text);
    }

    [TestMethod]
    [DataRow(null, "v", "key", DisplayName = "a null key")]
    [DataRow("k", null, "value", DisplayName = "a null value")]
    public void SurlRedisKey_ANullString_ThrowsArgumentNullExceptionNamingIt(string? key, string? value, string parameterName)
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new SurlRedisKey(key!, value!));

        Assert.AreEqual(parameterName, exception.ParamName);
    }

    [TestMethod]
    public async Task StartAsync_SeedsAtTheEdgesOfWhatIsAllowed_AreHeld()
    {
        var seams = new ApiTestSeams();
        var options = LookupOptions(RedisUrl);
        options.TimeProvider = new ManualTimeProvider();
        options.RedisKeys =
        [
            new SurlRedisKey("shortest", string.Empty, TimeSpan.FromMilliseconds(1)),
            new SurlRedisKey("longest", "v", TimeSpan.FromDays(365), 15),
        ];

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        Assert.HasCount(2, SeedsOf(server));
    }

    private static SurlServerOptions SeededLookupOptions()
    {
        var options = LookupOptions(RedisUrl, LdapUrl, DnsUrl);
        options.RedisKeys = [new SurlRedisKey("start", "yes")];
        options.DirectoryLdif = DirectoryLdif;
        options.DnsZones = [new SurlDnsZone("example.com", ExampleZone)];
        return options;
    }

    // The store, and the name, of each seed published so far, in the order published.
    private static (StoreKind Store, string Name)[] SeedsOf(SurlServer server) =>
    [
        .. server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.StoreChanged })
            .Cast<StoreChangedEvent>()
            .Where(changed => changed.Change == StoreChange.Seeded)
            .Select(changed => (changed.Store, changed.Name)),
    ];

    private static void AddServableSeed(SurlServerOptions options, string seed)
    {
        switch (seed)
        {
            case "key":
                options.RedisKeys = [new SurlRedisKey("k", "v")];
                break;
            case "entry":
                options.DirectoryLdif = DirectoryLdif;
                break;
            default:
                options.DnsZones = [new SurlDnsZone("example.com", ExampleZone)];
                break;
        }
    }

    private static void AddRefusedSeed(SurlServerOptions options, string seed)
    {
        switch (seed)
        {
            case "key-too-large":
                options.RedisKeys = [new SurlRedisKey("k"u8.ToArray(), new byte[16 * 1024 * 1024])];
                break;
            case "entry-change":
                options.DirectoryLdif = "dn: dc=x\nchangetype: delete\n";
                break;
            case "entry-twice":
                options.DirectoryLdif = DirectoryLdif + "\n" + DirectoryLdif;
                break;
            case "zone-bad":
                options.DnsZones = [new SurlDnsZone("other.com", "not a zone")];
                break;
            default:
                options.DnsZones = [new SurlDnsZone("example.com", ExampleZone), new SurlDnsZone("example.com.", ExampleZone)];
                break;
        }
    }

    private static void AddMisusedSeed(SurlServerOptions options, string misuse)
    {
        if (misuse is "null-zone-item" or "null-apex" or "null-zone-text")
        {
            options.DnsZones = [MisusedZone(misuse)!];
        }
        else
        {
            options.RedisKeys = [MisusedKey(misuse)!];
        }
    }

    private static SurlDnsZone? MisusedZone(string misuse) => misuse switch
    {
        "null-apex" => new SurlDnsZone(null!, ExampleZone),
        "null-zone-text" => new SurlDnsZone("example.com", null!),
        _ => null,
    };

    private static SurlRedisKey? MisusedKey(string misuse) => misuse switch
    {
        "empty-key" => new SurlRedisKey(string.Empty, "v"),
        "database-below" => new SurlRedisKey("k", "v", database: -1),
        "database-above" => new SurlRedisKey("k", "v", database: 16),
        "expiry-below" => new SurlRedisKey("k", "v", TimeSpan.FromMilliseconds(1) - TimeSpan.FromTicks(1)),
        "expiry-above" => new SurlRedisKey("k", "v", TimeSpan.FromDays(365) + TimeSpan.FromTicks(1)),
        _ => null,
    };
}
