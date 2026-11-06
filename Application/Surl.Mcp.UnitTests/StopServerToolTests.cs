namespace Surl.Mcp;

[TestClass]
public sealed class StopServerToolTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Handler_ARunningServer_StopsItAndRemovesItFromTheList()
    {
        await using var host = new McpTestHost();
        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);
        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);

        var result = await McpTestHost.CallAsync(host.StopServer, """{"server_id": "s1"}""", TestContext.CancellationToken);

        Assert.IsFalse(result.IsError);
        var structured = result.StructuredContent!.Value;
        Assert.AreEqual("s1", structured.GetProperty("server_id").GetString());
        Assert.AreEqual(0, structured.GetProperty("exit_code").GetInt32());
        Assert.AreEqual("Ok", structured.GetProperty("exit_code_name").GetString());
        Assert.AreEqual(structured.GetRawText(), Assert.ContainsSingle(result.TextContent));
        Assert.AreEqual("s2", Assert.ContainsSingle(host.Servers.List()).ServerId);
        Assert.AreEqual(1, host.Seams.ListenerFactory.OpenListenerCount);
    }

    [TestMethod]
    public async Task Handler_AnUnknownId_IsAnError()
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(host.StopServer, """{"server_id": "s9"}""", TestContext.CancellationToken);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("no running server has ID s9", Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    public async Task Handler_AServerStoppedAlready_IsAnErrorAndItsIdIsNotReused()
    {
        await using var host = new McpTestHost();
        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);
        await McpTestHost.CallAsync(host.StopServer, """{"server_id": "s1"}""", TestContext.CancellationToken);

        var again = await McpTestHost.CallAsync(host.StopServer, """{"server_id": "s1"}""", TestContext.CancellationToken);
        var next = await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);

        Assert.IsTrue(again.IsError);
        Assert.AreEqual("s2", next.StructuredContent!.Value.GetProperty("server_id").GetString());
    }

    [TestMethod]
    public void Create_NullServers_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => StopServerTool.Create(null!));
}
