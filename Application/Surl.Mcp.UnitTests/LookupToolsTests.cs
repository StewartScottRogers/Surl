using System.Text;
using System.Text.Json;
using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

// BL-1167: set_key, add_ldap_entries and add_dns_zone (ADR-0158 decision 9), each proved by a fake client of the
// protocol that reads the store, and reset_server removing what each seeded.
[TestClass]
public sealed class LookupToolsTests
{
    private const string DirectoryLdif = "dn: dc=example,dc=com\nobjectClass: domain\ndc: example\n";
    private const string ExampleZone =
        "$TTL 3600\n@ IN SOA ns.example.com. host.example.com. 1 7200 3600 1209600 300\nwww IN A 192.0.2.1\n";

    private static readonly TimeSpan ExchangeTimeLimit = TimeSpan.FromSeconds(30);

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SetKey_ThenAFakeRedisGet_AnswersTheValueAndNeverEchoesIt()
    {
        await using var host = await StartHostAsync("redis://127.0.0.1:0");

        var result = await CallAsync(host.SetKey, """{"server_id": "s1", "key": "seeded", "value": "yes"}""");
        var answer = Encoding.ASCII.GetString(await ServeAsync(host, "redis", "GET seeded\r\nQUIT\r\n"u8.ToArray()));

        StoreToolsTests.AssertResult(result, """{"key":"seeded","database":0,"event_name":"0/seeded","byte_count":3}""");
        StringAssert.Contains(answer, "$3\r\nyes\r\n");
    }

    [TestMethod]
    public async Task SetKey_GivenBase64InADatabaseWithAnExpiry_IsReadBackThere()
    {
        await using var host = await StartHostAsync("redis://127.0.0.1:0");

        var result = await CallAsync(host.SetKey, """{"server_id": "s1", "key_base64": "cmF3", "value_base64": "Ynl0ZXM=", "database": 2, "expire_ms": 3600000}""");
        var answer = Encoding.ASCII.GetString(await ServeAsync(host, "redis", "SELECT 2\r\nGET raw\r\nQUIT\r\n"u8.ToArray()));

        StoreToolsTests.AssertResult(result, """{"key":"raw","database":2,"event_name":"2/raw","byte_count":5}""");
        StringAssert.Contains(answer, "$5\r\nbytes\r\n");
    }

    [TestMethod]
    public async Task SetKey_ANameWithBytesThatAreNotUtf8_IsWrittenAsEscapesAndCutAt256Characters()
    {
        await using var host = await StartHostAsync("redis://127.0.0.1:0");
        var longKey = Convert.ToBase64String([0xFF, .. new byte[300].Select(_ => (byte)'a')]);

        var escaped = await CallAsync(host.SetKey, """{"server_id": "s1", "key_base64": "/2E=", "value": ""}""");
        var cut = await CallAsync(host.SetKey, $$"""{"server_id": "s1", "key_base64": "{{longKey}}", "value": "v"}""");

        StoreToolsTests.AssertResult(escaped, """{"key_base64":"/2E=","database":0,"event_name":"0/\\xFFa","byte_count":0}""");
        Assert.AreEqual(longKey, cut.StructuredContent!.Value.GetProperty("key_base64").GetString());
        var name = cut.StructuredContent!.Value.GetProperty("event_name").GetString()!;
        Assert.AreEqual(256, name.Length);
        StringAssert.StartsWith(name, "0/\\xFFaaa");
    }

    [TestMethod]
    [DataRow("""{"server_id": "s1", "value": "v"}""", "give exactly one of key and key_base64")]
    [DataRow("""{"server_id": "s1", "key": "k"}""", "give exactly one of value and value_base64")]
    [DataRow("""{"server_id": "s1", "key": "k", "value_base64": "not base64!"}""", "value_base64: expected a base64 string")]
    [DataRow("""{"server_id": "s1", "key_base64": "", "value": "v"}""", "key_base64: a key needs at least one byte")]
    public async Task SetKey_ContentNotAcceptable_IsAnErrorSayingWhy(string arguments, string expected)
    {
        await using var host = await StartHostAsync("redis://127.0.0.1:0");

        var result = await CallAsync(host.SetKey, arguments);

        StoreToolsTests.AssertFailure(result, expected);
    }

    [TestMethod]
    [DataRow("set_key", """{"server_id": "s1", "key": "k", "value": "v"}""", "Keyspace: A key in database 0 was refused: This instance holds no Redis keyspace.")]
    [DataRow("add_dns_zone", """{"server_id": "s1", "apex": "example.com", "zone_text": "x"}""", "Zones: The zone example.com was refused: This instance holds no DNS zones.")]
    [DataRow("add_ldap_entries", """{"server_id": "s1", "ldif": "dn: dc=x\nobjectClass: domain\ndc: x\n"}""", "Directory: The LDIF was refused: This instance holds no LDAP directory.")]
    public async Task SeedingAStoreNoListenUrlServes_IsAnErrorNamingTheStore(string tool, string arguments, string expected)
    {
        await using var host = await StartHostAsync("http://127.0.0.1:0");
        var target = tool switch { "set_key" => host.SetKey, "add_dns_zone" => host.AddDnsZone, _ => host.AddLdapEntries };

        var result = await CallAsync(target, arguments);

        StoreToolsTests.AssertFailure(result, expected);
    }

    [TestMethod]
    public async Task SetKey_AValueTheKeyspaceRefusesForSize_IsAnErrorWithTheStoresRefusal()
    {
        await using var host = await StartHostAsync("redis://127.0.0.1:0");

        var value = new string('a', SeededContent.MaxBytes);
        var result = await CallAsync(host.SetKey, $$"""{"server_id": "s1", "key": "k", "value": "{{value}}"}""");

        Assert.IsTrue(result.IsError);
        StringAssert.StartsWith(Assert.ContainsSingle(result.TextContent), "Keyspace: ");
    }

    [TestMethod]
    public async Task AddLdapEntries_ThenAFakeLdapSearch_FindsTheEntryAndCountsEntries()
    {
        await using var host = await StartHostAsync("ldap://127.0.0.1:0");

        var result = await CallAsync(host.AddLdapEntries, $$"""{"server_id": "s1", "ldif": {{JsonSerializer.Serialize(DirectoryLdif)}}}""");
        var answer = await ServeAsync(host, "ldap", LdapSearchThenUnbind);

        StoreToolsTests.AssertResult(result, """{"entry_count":1}""");
        Assert.IsTrue(Contains(answer, ExampleDn), Convert.ToHexString(answer));
    }

    [TestMethod]
    public async Task AddLdapEntries_ARecordTheDirectoryRefuses_IsAnError()
    {
        await using var host = await StartHostAsync("ldap://127.0.0.1:0");

        var result = await CallAsync(host.AddLdapEntries, """{"server_id": "s1", "ldif": "dn: dc=x\nchangetype: delete\n"}""");

        Assert.IsTrue(result.IsError);
        StringAssert.StartsWith(Assert.ContainsSingle(result.TextContent), "Directory: ");
    }

    [TestMethod]
    public async Task AddLdapEntries_PastSixteenMebibytes_IsAnError()
    {
        await using var host = await StartHostAsync("ldap://127.0.0.1:0");
        var ldif = new string('a', SeededContent.MaxBytes + 1);

        var result = await CallAsync(host.AddLdapEntries, $$"""{"server_id": "s1", "ldif": "{{ldif}}"}""");

        StoreToolsTests.AssertFailure(result, "ldif: more than the 16777216 (16 MiB) one seed may carry");
    }

    [TestMethod]
    public async Task AddDnsZone_ThenAFakeDnsQuery_AnswersFromTheZone()
    {
        await using var host = await StartHostAsync("dns://127.0.0.1:0");

        var result = await CallAsync(host.AddDnsZone, $$"""{"server_id": "s1", "apex": "example.com", "zone_text": {{JsonSerializer.Serialize(ExampleZone)}}}""");
        var answer = await ServeAsync(host, "dns", WwwExampleComQuery);

        StoreToolsTests.AssertResult(result, $$"""{"apex":"example.com","byte_count":{{ExampleZone.Length}}}""");
        Assert.IsTrue(Contains(answer, ExampleAddress), Convert.ToHexString(answer));
    }

    [TestMethod]
    public async Task AddDnsZone_ASecondZoneForTheApexOrTextThatIsNoZone_IsAnError()
    {
        await using var host = await StartHostAsync("dns://127.0.0.1:0");
        var zone = JsonSerializer.Serialize(ExampleZone);
        await CallAsync(host.AddDnsZone, $$"""{"server_id": "s1", "apex": "example.com", "zone_text": {{zone}}}""");

        var twice = await CallAsync(host.AddDnsZone, $$"""{"server_id": "s1", "apex": "example.com.", "zone_text": {{zone}}}""");
        var bad = await CallAsync(host.AddDnsZone, """{"server_id": "s1", "apex": "other.com", "zone_text": "not a zone"}""");

        Assert.IsTrue(twice.IsError);
        Assert.IsTrue(bad.IsError);
        StringAssert.StartsWith(Assert.ContainsSingle(twice.TextContent), "Zones: ");
    }

    [TestMethod]
    public async Task AddDnsZone_PastSixteenMebibytes_IsAnError()
    {
        await using var host = await StartHostAsync("dns://127.0.0.1:0");
        var text = new string('a', SeededContent.MaxBytes + 1);

        var result = await CallAsync(host.AddDnsZone, $$"""{"server_id": "s1", "apex": "example.com", "zone_text": "{{text}}"}""");

        StoreToolsTests.AssertFailure(result, "zone_text: more than the 16777216 (16 MiB) one seed may carry");
    }

    [TestMethod]
    public async Task ResetServer_AfterSeedingAKeyEntryAndZone_RemovesAllThree()
    {
        await using var host = await StartHostAsync("redis://127.0.0.1:0", "ldap://127.0.0.1:0", "dns://127.0.0.1:0");
        await CallAsync(host.SetKey, """{"server_id": "s1", "key": "seeded", "value": "yes"}""");
        await CallAsync(host.AddLdapEntries, $$"""{"server_id": "s1", "ldif": {{JsonSerializer.Serialize(DirectoryLdif)}}}""");
        await CallAsync(host.AddDnsZone, $$"""{"server_id": "s1", "apex": "example.com", "zone_text": {{JsonSerializer.Serialize(ExampleZone)}}}""");

        var reset = await CallAsync(host.ResetServer, """{"server_id": "s1"}""");
        var redis = Encoding.ASCII.GetString(await ServeAsync(host, "redis", "GET seeded\r\nQUIT\r\n"u8.ToArray()));
        var ldap = await ServeAsync(host, "ldap", LdapSearchThenUnbind);
        var dns = await ServeAsync(host, "dns", WwwExampleComQuery);

        StoreToolsTests.AssertResult(reset, """{"server_id":"s1"}""");
        StringAssert.Contains(redis, "$-1\r\n");
        Assert.IsFalse(Contains(ldap, ExampleDn), Convert.ToHexString(ldap));
        Assert.IsFalse(Contains(dns, ExampleAddress), Convert.ToHexString(dns));
    }

    private static bool Contains(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;

    private async Task<McpTestHost> StartHostAsync(params string[] listenUrls)
    {
        var host = new McpTestHost(areExtensionsTurnedOn: true);
        var started = await CallAsync(
            host.StartServer, $$"""{"listen_urls": {{JsonSerializer.Serialize(listenUrls)}}, "arguments": ["--allow-anonymous"]}""");
        Assert.IsFalse(started.IsError, started.TextContent.FirstOrDefault());
        return host;
    }

    private Task<McpToolResult> CallAsync(McpTool tool, string arguments) =>
        McpTestHost.CallAsync(tool, arguments, TestContext.CancellationToken);

    private async Task<byte[]> ServeAsync(McpTestHost host, string scheme, byte[] clientBytes)
    {
        var connection = new FakeConnection(clientBytes);
        host.Seams.ListenerFactory.Accept(scheme, connection);
        await connection.Disposed.WaitAsync(ExchangeTimeLimit, TestContext.CancellationToken);
        return connection.WrittenToClient;
    }
}
