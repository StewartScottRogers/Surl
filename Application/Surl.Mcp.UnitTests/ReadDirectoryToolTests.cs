using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

// BL-1219: read_directory (ADR-0164 decision 9), reading back what add_ldap_entries put in.
[TestClass]
public sealed class ReadDirectoryToolTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SeededEntries_ReadBackAsLdifLessUserPassword()
    {
        await using var host = await StartHostAsync("ldap://127.0.0.1:0");
        var added = await CallAsync(host.AddLdapEntries, """{"server_id": "s1", "ldif": "dn: dc=example,dc=com\ndc: example\nuserPassword: secret\n"}""");
        Assert.IsFalse(added.IsError, added.TextContent.FirstOrDefault());

        var result = await CallAsync(host.ReadDirectory, """{"server_id": "s1"}""");

        Assert.IsFalse(result.IsError, result.TextContent.FirstOrDefault());
        Assert.AreEqual(result.StructuredContent!.Value.GetRawText(), Assert.ContainsSingle(result.TextContent));
        Assert.AreEqual("version: 1\n\ndn: dc=example,dc=com\ndc: example\n", result.StructuredContent!.Value.GetProperty("ldif").GetString());
    }

    [TestMethod]
    public async Task AnUnknownServerId_IsStopServersError()
    {
        await using var host = await StartHostAsync("ldap://127.0.0.1:0");

        var result = await CallAsync(host.ReadDirectory, """{"server_id": "s9"}""");

        StoreToolsTests.AssertFailure(result, StopServerTool.NoRunningServer("s9"));
    }

    [TestMethod]
    public async Task AServerWithoutAnLdapListener_ReadsAnEmptyDirectory()
    {
        await using var host = await StartHostAsync("redis://127.0.0.1:0");

        var result = await CallAsync(host.ReadDirectory, """{"server_id": "s1"}""");

        Assert.IsFalse(result.IsError, result.TextContent.FirstOrDefault());
        Assert.AreEqual("version: 1\n", result.StructuredContent!.Value.GetProperty("ldif").GetString());
    }

    [TestMethod]
    public void Create_NullServers_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ReadDirectoryTool.Create(null!));

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
