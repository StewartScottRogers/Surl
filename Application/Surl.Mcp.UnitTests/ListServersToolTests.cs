using System.Text.Json;

namespace Surl.Mcp;

[TestClass]
public sealed class ListServersToolTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Handler_TwoRunningServers_NamesEachWithItsListenAndBoundUrlsInStartOrder()
    {
        await using var host = new McpTestHost();
        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);
        await McpTestHost.CallAsync(
            host.StartServer, """{"listen_urls": ["ftp://localhost:0", "http://[::1]:0"]}""", TestContext.CancellationToken);

        var result = await McpTestHost.CallAsync(host.ListServers, "{}", TestContext.CancellationToken);

        Assert.IsFalse(result.IsError);
        var servers = result.StructuredContent!.Value.GetProperty("servers").EnumerateArray().ToArray();
        Assert.HasCount(2, servers);
        Assert.AreEqual("s1", servers[0].GetProperty("server_id").GetString());
        CollectionAssert.AreEqual(new[] { "http://127.0.0.1:0" }, Strings(servers[0], "listen_urls"));
        CollectionAssert.AreEqual(new[] { "http://127.0.0.1:49731/" }, Strings(servers[0], "bound_urls"));
        Assert.AreEqual("s2", servers[1].GetProperty("server_id").GetString());
        CollectionAssert.AreEqual(new[] { "ftp://localhost:0", "http://[::1]:0" }, Strings(servers[1], "listen_urls"));
        CollectionAssert.AreEqual(new[] { "ftp://localhost:49731/", "http://[::1]:49731/" }, Strings(servers[1], "bound_urls"));
        Assert.AreEqual("2026-11-05T10:00:00.0000000+00:00", servers[0].GetProperty("started_at").GetString());
        Assert.AreEqual(0, servers[0].GetProperty("exchange_count").GetInt32());
        Assert.AreEqual(0, servers[0].GetProperty("dropped_exchange_count").GetInt64());
        Assert.AreEqual(result.StructuredContent!.Value.GetRawText(), Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    public async Task Handler_NoServers_ListsNone()
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(host.ListServers, "{}", TestContext.CancellationToken);

        Assert.AreEqual(0, result.StructuredContent!.Value.GetProperty("servers").GetArrayLength());
    }

    [TestMethod]
    public void Create_NullServers_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ListServersTool.Create(null!));

    private static string[] Strings(JsonElement server, string name) =>
        [.. server.GetProperty(name).EnumerateArray().Select(item => item.GetString()!)];
}
