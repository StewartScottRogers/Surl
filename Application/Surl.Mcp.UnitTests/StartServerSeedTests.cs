using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

// BL-2032: start_server's redis_keys, directory_ldif and dns_zones (ADR-0158 decisions 3 and 9, amendment 5).
[TestClass]
public sealed class StartServerSeedTests
{
    private const string DirectoryLdif = "dn: dc=example,dc=com\nobjectClass: domain\ndc: example\n";
    private const string ExampleZone =
        "$TTL 3600\n@ IN SOA ns.example.com. host.example.com. 1 7200 3600 1209600 300\nwww IN A 192.0.2.1\n";

    private static readonly string AllThreeSeeds = JsonSerializer.Serialize(new
    {
        listen_urls = new[] { "redis://127.0.0.1:0", "ldap://127.0.0.1:0", "dns://127.0.0.1:0" },
        arguments = new[] { "--allow-anonymous" },
        redis_keys = new object[]
        {
            new { key = "greeting", value = "hello" },
            new { key_base64 = "cmF3", value_base64 = "Ynl0ZXM=", database = 2, expire_ms = 31536000000L },
        },
        directory_ldif = DirectoryLdif,
        dns_zones = new[] { new { apex = "example.com", zone_text = ExampleZone } },
    });

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void InputSchema_DeclaresTheThreeOptionalSeedPropertiesInTheSeedToolsShapes()
    {
        using var schema = JsonDocument.Parse(StartServerTool.InputSchema);
        var properties = schema.RootElement.GetProperty("properties");

        var key = properties.GetProperty("redis_keys").GetProperty("items");
        CollectionAssert.AreEqual(
            new[] { "key", "key_base64", "value", "value_base64", "database", "expire_ms" },
            PropertyNames(key));
        Assert.IsFalse(key.GetProperty("additionalProperties").GetBoolean());
        AssertBounds(key.GetProperty("properties").GetProperty("database"), 0, 15);
        AssertBounds(key.GetProperty("properties").GetProperty("expire_ms"), 1, 31536000000);
        Assert.AreEqual("string", properties.GetProperty("directory_ldif").GetProperty("type").GetString());
        var zone = properties.GetProperty("dns_zones").GetProperty("items");
        CollectionAssert.AreEqual(new[] { "apex", "zone_text" }, PropertyNames(zone));
        CollectionAssert.AreEqual(
            new[] { "apex", "zone_text" }, zone.GetProperty("required").EnumerateArray().Select(name => name.GetString()).ToArray());
        Assert.IsFalse(zone.GetProperty("additionalProperties").GetBoolean());
        CollectionAssert.AreEqual(
            new[] { "listen_urls" },
            schema.RootElement.GetProperty("required").EnumerateArray().Select(name => name.GetString()).ToArray());
    }

    [TestMethod]
    public void Descriptions_OfStartAndResetServer_NameTheSeedsAndWhatAResetReturnsTo()
    {
        foreach (var name in new[] { "redis_keys", "directory_ldif", "dns_zones", "`reset_server`" })
        {
            StringAssert.Contains(StartServerTool.Description, name);
        }

        foreach (var name in new[] { "Redis keys", "LDAP directory", "DNS zones", "`start_server`" })
        {
            StringAssert.Contains(ResetServerTool.Description, name);
        }
    }

    [TestMethod]
    public async Task StartServer_WithAllThreeSeeds_AnswersAServerThatReadsThemBackAndRecordsSeededEvents()
    {
        await using var host = NewHost();

        var started = await CallAsync(host.StartServer, AllThreeSeeds);
        var textKey = await CallAsync(host.ReadKey, """{"server_id": "s1", "key": "greeting"}""");
        var base64Key = await CallAsync(host.ReadKey, """{"server_id": "s1", "key": "raw", "database": 2}""");
        var directory = await CallAsync(host.ReadDirectory, """{"server_id": "s1"}""");
        var zone = await CallAsync(host.ReadZone, """{"server_id": "s1", "apex": "example.com"}""");
        var events = await CallAsync(host.GetEvents, """{"server_id": "s1", "kinds": ["store_changed"]}""");

        Assert.IsFalse(started.IsError, started.TextContent.FirstOrDefault());
        Assert.IsFalse(Assert.ContainsSingle(started.TextContent).Contains("hello", StringComparison.Ordinal));
        Assert.AreEqual("hello", Text(textKey, "text"));
        Assert.IsFalse(textKey.StructuredContent!.Value.TryGetProperty("expires_at", out _));
        Assert.AreEqual("bytes", Text(base64Key, "text"));
        Assert.IsTrue(base64Key.StructuredContent!.Value.TryGetProperty("expires_at", out _));
        StringAssert.Contains(Text(directory, "ldif"), "dn: dc=example,dc=com");
        StringAssert.Contains(Text(zone, "zone"), "www.example.com. 3600 IN A 192.0.2.1");
        var seededStores = events.StructuredContent!.Value.GetProperty("events").EnumerateArray()
            .Where(stored => stored.GetProperty("change").GetString() == "seeded")
            .Select(stored => stored.GetProperty("store").GetString())
            .ToArray();
        CollectionAssert.AreEqual(new[] { "keyspace", "keyspace", "directory", "zones" }, seededStores);
    }

    [TestMethod]
    public async Task ResetServer_AfterFurtherSeeds_LeavesExactlyTheStartTimeOnes()
    {
        await using var host = NewHost();
        await CallAsync(host.StartServer, AllThreeSeeds);
        var directoryAtStart = Text(await CallAsync(host.ReadDirectory, """{"server_id": "s1"}"""), "ldif");
        var added = new[]
        {
            await CallAsync(host.SetKey, """{"server_id": "s1", "key": "greeting", "value": "changed"}"""),
            await CallAsync(host.SetKey, """{"server_id": "s1", "key": "further", "value": "v"}"""),
            await CallAsync(host.AddLdapEntries, """{"server_id": "s1", "ldif": "dn: ou=more,dc=example,dc=com\nobjectClass: organizationalUnit\nou: more\n"}"""),
            await CallAsync(host.AddDnsZone, JsonSerializer.Serialize(new { server_id = "s1", apex = "other.test", zone_text = ExampleZone.Replace("example.com", "other.test", StringComparison.Ordinal) })),
        };
        foreach (var result in added)
        {
            Assert.IsFalse(result.IsError, result.TextContent.FirstOrDefault());
        }

        var reset = await CallAsync(host.ResetServer, """{"server_id": "s1"}""");

        StoreToolsTests.AssertResult(reset, """{"server_id":"s1"}""");
        var keys = await CallAsync(host.ListKeys, """{"server_id": "s1"}""");
        var key = Assert.ContainsSingle(keys.StructuredContent!.Value.GetProperty("keys").EnumerateArray());
        Assert.AreEqual("greeting", key.GetProperty("key").GetString());
        Assert.AreEqual("hello", Text(await CallAsync(host.ReadKey, """{"server_id": "s1", "key": "greeting"}"""), "text"));
        Assert.AreEqual("bytes", Text(await CallAsync(host.ReadKey, """{"server_id": "s1", "key": "raw", "database": 2}"""), "text"));
        Assert.AreEqual(directoryAtStart, Text(await CallAsync(host.ReadDirectory, """{"server_id": "s1"}"""), "ldif"));
        StoreToolsTests.AssertResult(await CallAsync(host.ReadZone, """{"server_id": "s1"}"""), """{"zones":["example.com."]}""");
    }

    [TestMethod]
    public async Task StartServerAndSetKey_WholeNumbersWrittenWithAFractionOrExponent_AreRead()
    {
        await using var host = NewHost();

        var started = await CallAsync(
            host.StartServer,
            """{"listen_urls": ["redis://127.0.0.1:0"], "arguments": ["--allow-anonymous"], "redis_keys": [{"key": "a", "value": "v", "database": 1.0, "expire_ms": 6e4}]}""");
        var set = await CallAsync(host.SetKey, """{"server_id": "s1", "key": "b", "value": "w", "database": 2.0, "expire_ms": 6e4}""");

        Assert.IsFalse(started.IsError, started.TextContent.FirstOrDefault());
        Assert.IsFalse(set.IsError, set.TextContent.FirstOrDefault());
        Assert.AreEqual("v", Text(await CallAsync(host.ReadKey, """{"server_id": "s1", "key": "a", "database": 1}"""), "text"));
        Assert.AreEqual("w", Text(await CallAsync(host.ReadKey, """{"server_id": "s1", "key": "b", "database": 2}"""), "text"));
    }

    [TestMethod]
    [DataRow("redis://127.0.0.1:0", """ "directory_ldif": "dn: dc=x\nobjectClass: domain\ndc: x\n" """, "Directory: The LDIF was refused: This instance holds no LDAP directory.")]
    [DataRow("http://127.0.0.1:0", """ "redis_keys": [{"key": "k", "value": "v"}] """, "Keyspace: A key in database 0 was refused: This instance holds no Redis keyspace.")]
    [DataRow("http://127.0.0.1:0", """ "dns_zones": [{"apex": "example.com", "zone_text": "x"}] """, "Zones: The zone example.com was refused: This instance holds no DNS zones.")]
    public async Task StartServer_ASeedForAStoreNoListenUrlServes_IsAnErrorWithTheStoresWordsAndNoServer(string listenUrl, string seed, string expected)
    {
        await using var host = NewHost();

        var result = await CallAsync(host.StartServer, $$"""{"listen_urls": ["{{listenUrl}}"], "arguments": ["--allow-anonymous"], {{seed}}}""");

        StoreToolsTests.AssertFailure(result, expected);
        await AssertNoServerAsync(host);
    }

    [TestMethod]
    [DataRow("ldap://127.0.0.1:0", """ "directory_ldif": "dn: dc=x\nchangetype: delete\n" """, "Directory: ")]
    [DataRow("dns://127.0.0.1:0", """ "dns_zones": [{"apex": "example.com", "zone_text": "not a zone"}] """, "Zones: ")]
    public async Task StartServer_ARefusedSeed_IsAnErrorThatLeavesNoServerAndUsesNoSlot(string listenUrl, string seed, string expectedStart)
    {
        await using var host = NewHost(new SurlMcpHostSettings { MaxServers = 1 });

        var result = await CallAsync(host.StartServer, $$"""{"listen_urls": ["{{listenUrl}}"], "arguments": ["--allow-anonymous"], {{seed}}}""");

        Assert.IsTrue(result.IsError);
        StringAssert.StartsWith(Assert.ContainsSingle(result.TextContent), expectedStart);
        await AssertNoServerAsync(host);
        var next = await CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""");
        Assert.IsFalse(next.IsError, next.TextContent.FirstOrDefault());
    }

    [TestMethod]
    [DataRow("""{"key": "k", "key_base64": "aw==", "value": "s3cret"}""", "redis_keys[1]: give exactly one of key and key_base64")]
    [DataRow("""{"value": "s3cret"}""", "redis_keys[1]: give exactly one of key and key_base64")]
    [DataRow("""{"key": "k", "value": "s3cret", "value_base64": "czNjcmV0"}""", "redis_keys[1]: give exactly one of value and value_base64")]
    [DataRow("""{"key": "k"}""", "redis_keys[1]: give exactly one of value and value_base64")]
    [DataRow("""{"key": "", "value": "s3cret"}""", "redis_keys[1]: key: a key needs at least one byte")]
    [DataRow("""{"key_base64": "", "value": "s3cret"}""", "redis_keys[1]: key_base64: a key needs at least one byte")]
    [DataRow("""{"key_base64": "s3cret!", "value": "s3cret"}""", "redis_keys[1]: key_base64: expected a base64 string")]
    [DataRow("""{"key": "k", "value_base64": "s3cret!"}""", "redis_keys[1]: value_base64: expected a base64 string")]
    public async Task StartServer_ABadRedisKeyItem_IsAnErrorNamingItWithoutItsValueAndNoServer(string item, string expected)
    {
        await using var host = NewHost();

        var result = await CallAsync(
            host.StartServer,
            $$"""{"listen_urls": ["redis://127.0.0.1:0"], "redis_keys": [{"key": "good", "value": "v"}, {{item}}]}""");

        StoreToolsTests.AssertFailure(result, expected);
        Assert.IsNull(result.StructuredContent);
        await AssertNoServerAsync(host);
    }

    private static McpTestHost NewHost(SurlMcpHostSettings? settings = null) => new(settings, areExtensionsTurnedOn: true);

    private static string[] PropertyNames(JsonElement schema) =>
        [.. schema.GetProperty("properties").EnumerateObject().Select(property => property.Name)];

    private static void AssertBounds(JsonElement schema, long minimum, long maximum)
    {
        Assert.AreEqual(minimum, schema.GetProperty("minimum").GetInt64());
        Assert.AreEqual(maximum, schema.GetProperty("maximum").GetInt64());
    }

    private static string Text(McpToolResult result, string member)
    {
        Assert.IsFalse(result.IsError, result.TextContent.FirstOrDefault());
        return result.StructuredContent!.Value.GetProperty(member).GetString()!;
    }

    private async Task AssertNoServerAsync(McpTestHost host) =>
        StoreToolsTests.AssertResult(await CallAsync(host.ListServers, "{}"), """{"servers":[]}""");

    private Task<McpToolResult> CallAsync(McpTool tool, string arguments) =>
        McpTestHost.CallAsync(tool, arguments, TestContext.CancellationToken);
}
