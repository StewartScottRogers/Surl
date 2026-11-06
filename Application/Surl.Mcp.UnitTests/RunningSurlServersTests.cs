using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

[TestClass]
public sealed class RunningSurlServersTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DisposeAsync_RunningServers_StopsEveryServer()
    {
        var host = new McpTestHost();
        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);
        await McpTestHost.CallAsync(
            host.StartServer, """{"listen_urls": ["http://127.0.0.1:0", "ftp://127.0.0.1:0"]}""", TestContext.CancellationToken);
        Assert.AreEqual(3, host.Seams.ListenerFactory.OpenListenerCount);

        await host.Servers.DisposeAsync();

        Assert.AreEqual(0, host.Seams.ListenerFactory.OpenListenerCount);
        Assert.IsEmpty(host.Servers.List());
    }

    [TestMethod]
    public async Task DisposeAsync_NoServers_Completes()
    {
        var servers = new RunningSurlServers(new SurlMcpHostSettings(), TextWriter.Null, TimeProvider.System);

        await servers.DisposeAsync();

        Assert.IsEmpty(servers.List());
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new RunningSurlServers(null!, TextWriter.Null, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RunningSurlServers(new SurlMcpHostSettings(), null!, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RunningSurlServers(new SurlMcpHostSettings(), TextWriter.Null, null!));
    }

    [TestMethod]
    public void Settings_Default_AreSecureByDefault()
    {
        var settings = new SurlMcpHostSettings();

        Assert.IsNull(settings.Root);
        Assert.IsFalse(settings.AllowNonLoopback);
        Assert.AreEqual(8, settings.MaxServers);
        Assert.AreEqual(Output.LogLevel.Info, settings.LogLevel);
    }

    [TestMethod]
    public async Task Tools_EachOne_HasAJsonObjectInputSchemaAndADescriptionAndRegisters()
    {
        await using var host = new McpTestHost();
        var registry = new McpServerRegistry();

        foreach (var tool in new[] { host.StartServer, host.ListServers, host.StopServer })
        {
            registry.AddTool(tool);
            using var schema = JsonDocument.Parse(tool.InputSchema);
            Assert.AreEqual(JsonValueKind.Object, schema.RootElement.ValueKind, tool.Name);
            Assert.AreEqual("object", schema.RootElement.GetProperty("type").GetString(), tool.Name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(tool.Description), tool.Name);
        }

        CollectionAssert.AreEqual(
            new[] { StartServerTool.Name, ListServersTool.Name, StopServerTool.Name },
            registry.Tools.Select(tool => tool.Name).ToArray());
    }
}
