using System.Text.Json;
using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

// BL-1218: read_key and list_keys (ADR-0164 decision 9), each reading back what set_key put in.
[TestClass]
public sealed class ReadKeyToolsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SeededKeys_ReadBackByNameAndListedByGlob()
    {
        await using var host = await StartSeededHostAsync();

        var one = Ok(await CallAsync(host.ReadKey, """{"server_id": "s1", "key": "user:1"}"""));
        var binary = Ok(await CallAsync(host.ReadKey, """{"server_id": "s1", "key_base64": "/w==", "database": 2}"""));
        var listed = Ok(await CallAsync(host.ListKeys, """{"server_id": "s1", "pattern": "user:*"}"""));
        var paged = Ok(await CallAsync(host.ListKeys, """{"server_id": "s1", "after_key": "other", "limit": 1}"""));
        var everything = Ok(await CallAsync(host.ListKeys, """{"server_id": "s1"}"""));

        Assert.AreEqual("user:1", one.GetProperty("key").GetString());
        Assert.AreEqual("string", one.GetProperty("type").GetString());
        Assert.AreEqual(5, one.GetProperty("byte_count").GetInt64());
        Assert.AreEqual("alice", one.GetProperty("text").GetString());
        Assert.IsTrue(one.TryGetProperty("expires_at", out _));
        Assert.AreEqual("/w==", binary.GetProperty("key_base64").GetString());
        Assert.AreEqual("AP8=", binary.GetProperty("base64").GetString());
        Assert.IsFalse(binary.TryGetProperty("expires_at", out _));
        Assert.AreEqual(2, listed.GetProperty("keys").GetArrayLength());
        Assert.IsFalse(listed.GetProperty("has_more").GetBoolean());
        Assert.AreEqual("user:1", Assert.ContainsSingle(paged.GetProperty("keys").EnumerateArray()).GetProperty("key").GetString());
        Assert.IsTrue(paged.GetProperty("has_more").GetBoolean());
        Assert.AreEqual(3, everything.GetProperty("keys").GetArrayLength());
    }

    [TestMethod]
    [DataRow(ReadKeyTool.Name, """{"server_id": "s9", "key": "k"}""")]
    [DataRow(ListKeysTool.Name, """{"server_id": "s9"}""")]
    public async Task EveryKeyReadTool_AnUnknownServerId_IsStopServersError(string toolName, string arguments)
    {
        await using var host = await StartSeededHostAsync();

        var result = await CallAsync(FindTool(host, toolName), arguments);

        StoreToolsTests.AssertFailure(result, StopServerTool.NoRunningServer("s9"));
    }

    [TestMethod]
    [DataRow(ReadKeyTool.Name, """{"server_id": "s1", "key": "none"}""")]
    [DataRow(ReadKeyTool.Name, """{"server_id": "s1"}""")]
    [DataRow(ReadKeyTool.Name, """{"server_id": "s1", "key": "k", "key_base64": "aw=="}""")]
    [DataRow(ReadKeyTool.Name, """{"server_id": "s1", "key": "user:1", "database": 3}""")]
    [DataRow(ListKeysTool.Name, """{"server_id": "s1", "limit": 5000}""")]
    public async Task EveryKeyReadTool_AnAbsentOrRefusedKey_IsAnError(string toolName, string arguments)
    {
        await using var host = await StartSeededHostAsync();

        var result = await CallAsync(FindTool(host, toolName), arguments);

        Assert.IsTrue(result.IsError);
        Assert.IsFalse(string.IsNullOrWhiteSpace(Assert.ContainsSingle(result.TextContent)));
    }

    [TestMethod]
    public async Task ListKeys_WithADatabase_ListsThatDatabasesKeys()
    {
        await using var host = await StartSeededHostAsync();

        var second = Ok(await CallAsync(host.ListKeys, """{"server_id": "s1", "database": 2}"""));
        var first = Ok(await CallAsync(host.ListKeys, """{"server_id": "s1", "database": 0}"""));

        Assert.AreEqual("/w==", Assert.ContainsSingle(second.GetProperty("keys").EnumerateArray()).GetProperty("key_base64").GetString());
        Assert.AreEqual(3, first.GetProperty("keys").GetArrayLength());
    }

    [TestMethod]
    public async Task ReadKey_AKeyOfAnotherType_HasNoValueMember()
    {
        await using var host = await StartSeededHostAsync();
        var connection = new FakeConnection("RPUSH numbers 1\r\nQUIT\r\n"u8.ToArray());
        host.Seams.ListenerFactory.Accept("redis", connection);
        await connection.Disposed.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        var list = Ok(await CallAsync(host.ReadKey, """{"server_id": "s1", "key": "numbers"}"""));
        var text = Ok(await CallAsync(host.ReadKey, """{"server_id": "s1", "key": "other"}"""));

        Assert.AreEqual("list", list.GetProperty("type").GetString());
        Assert.IsFalse(list.TryGetProperty("text", out _));
        Assert.IsFalse(list.TryGetProperty("base64", out _));
        Assert.AreEqual("x", text.GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task SetKey_ANameThatPassesTheLimitOnAnEscape_IsCutToExactly256Characters()
    {
        await using var host = await StartSeededHostAsync();
        var key = Convert.ToBase64String([.. Enumerable.Repeat((byte)'a', 253), 0xFF]);

        var result = Ok(await CallAsync(host.SetKey, $$"""{"server_id": "s1", "key_base64": "{{key}}", "value": "v"}"""));
        var shortName = Ok(await CallAsync(host.SetKey, """{"server_id": "s1", "key": "k", "value": "v"}"""));

        Assert.AreEqual("0/" + new string('a', 253) + "\\", result.GetProperty("key").GetString());
        Assert.AreEqual(256, result.GetProperty("key").GetString()!.Length);
        Assert.AreEqual("0/k", shortName.GetProperty("key").GetString());
    }

    [TestMethod]
    public void Create_NullServers_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ReadKeyTool.Create(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ListKeysTool.Create(null!));
    }

    private static McpTool FindTool(McpTestHost host, string name) =>
        new[] { host.ReadKey, host.ListKeys }.Single(tool => tool.Name == name);

    private static JsonElement Ok(McpToolResult result)
    {
        Assert.IsFalse(result.IsError, result.TextContent.FirstOrDefault());
        Assert.AreEqual(result.StructuredContent!.Value.GetRawText(), Assert.ContainsSingle(result.TextContent));
        return result.StructuredContent!.Value;
    }

    private Task<McpToolResult> CallAsync(McpTool tool, string arguments) =>
        McpTestHost.CallAsync(tool, arguments, TestContext.CancellationToken);

    private async Task<McpTestHost> StartSeededHostAsync()
    {
        var host = new McpTestHost(areExtensionsTurnedOn: true);
        var started = await CallAsync(host.StartServer, """{"listen_urls": ["redis://127.0.0.1:0"], "arguments": ["--allow-anonymous"]}""");
        Assert.IsFalse(started.IsError, started.TextContent.FirstOrDefault());
        Assert.IsFalse((await CallAsync(host.SetKey, """{"server_id": "s1", "key": "user:1", "value": "alice", "expire_ms": 60000}""")).IsError);
        Assert.IsFalse((await CallAsync(host.SetKey, """{"server_id": "s1", "key": "user:2", "value": "bob"}""")).IsError);
        Assert.IsFalse((await CallAsync(host.SetKey, """{"server_id": "s1", "key": "other", "value": "x"}""")).IsError);
        Assert.IsFalse((await CallAsync(host.SetKey, """{"server_id": "s1", "key_base64": "/w==", "value_base64": "AP8=", "database": 2}""")).IsError);
        return host;
    }
}
