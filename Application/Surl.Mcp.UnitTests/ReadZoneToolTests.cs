using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

// BL-1220: read_zone (ADR-0164 decision 9), reading back what add_dns_zone put in.
[TestClass]
public sealed class ReadZoneToolTests
{
    private const string ZoneText = "$TTL 60\n@ SOA ns hostmaster 1 2 3 4 30\nwww A 192.0.2.7\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnApex_ReadsTheSeededZoneBack()
    {
        await using var host = await StartHostAsync("dns://127.0.0.1:0");
        var added = await CallAsync(host.AddDnsZone, JsonSerializer.Serialize(new { server_id = "s1", apex = "example.test", zone_text = ZoneText }));
        Assert.IsFalse(added.IsError, added.TextContent.FirstOrDefault());

        var result = await CallAsync(host.ReadZone, """{"server_id": "s1", "apex": "Example.Test"}""");

        Assert.IsFalse(result.IsError, result.TextContent.FirstOrDefault());
        Assert.AreEqual(result.StructuredContent!.Value.GetRawText(), Assert.ContainsSingle(result.TextContent));
        Assert.AreEqual("Example.Test", result.StructuredContent!.Value.GetProperty("apex").GetString());
        Assert.AreEqual(
            "example.test. 60 IN SOA ns.example.test. hostmaster.example.test. 1 2 3 4 30\nwww.example.test. 60 IN A 192.0.2.7\n",
            result.StructuredContent!.Value.GetProperty("zone").GetString());
    }

    [TestMethod]
    public async Task NoApex_ListsEveryApex()
    {
        await using var host = await StartHostAsync("dns://127.0.0.1:0");
        await CallAsync(host.AddDnsZone, JsonSerializer.Serialize(new { server_id = "s1", apex = "example.test", zone_text = ZoneText }));

        var result = await CallAsync(host.ReadZone, """{"server_id": "s1"}""");

        Assert.IsFalse(result.IsError, result.TextContent.FirstOrDefault());
        Assert.AreEqual("""{"zones":["example.test."]}""", result.StructuredContent!.Value.GetRawText());
    }

    [TestMethod]
    public async Task AnApexNoZoneHas_IsAnError()
    {
        await using var host = await StartHostAsync("dns://127.0.0.1:0");

        var result = await CallAsync(host.ReadZone, """{"server_id": "s1", "apex": "other.test"}""");

        Assert.IsTrue(result.IsError);
    }

    [TestMethod]
    public async Task AnUnknownServerId_IsStopServersError()
    {
        await using var host = await StartHostAsync("dns://127.0.0.1:0");

        var result = await CallAsync(host.ReadZone, """{"server_id": "s9"}""");

        StoreToolsTests.AssertFailure(result, StopServerTool.NoRunningServer("s9"));
    }

    [TestMethod]
    public void Create_NullServers_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ReadZoneTool.Create(null!));

    private async Task<McpTestHost> StartHostAsync(string listenUrl)
    {
        var host = new McpTestHost(areExtensionsTurnedOn: true);
        var started = await CallAsync(
            host.StartServer, $$"""{"listen_urls": [{{JsonSerializer.Serialize(listenUrl)}}], "arguments": ["--allow-anonymous"]}""");
        Assert.IsFalse(started.IsError, started.TextContent.FirstOrDefault());
        return host;
    }

    private Task<McpToolResult> CallAsync(McpTool tool, string arguments) =>
        McpTestHost.CallAsync(tool, arguments, TestContext.CancellationToken);
}
