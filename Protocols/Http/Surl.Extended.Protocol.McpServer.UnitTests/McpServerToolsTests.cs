using System.Text.Json;
using Surl.Content;
using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpServerToolsTests
{
    private static readonly ContentExposureOptions Everything = new() { ListDirectories = true, AllowUploads = true };

    [TestMethod]
    public async Task ToolsList_EveryOptionOn_ListsTheAdrsFourToolsInOrderEachWithItsSchema()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Everything);

        string response = await harness.AnswerAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");

        JsonElement[] tools = [.. JsonDocument.Parse(response).RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()];
        CollectionAssert.AreEqual(
            new[] { "list_directory", "read_file", "write_file", "echo" },
            tools.Select(tool => tool.GetProperty("name").GetString()).ToArray());
        string[] schemas = [McpListDirectoryTool.InputSchema, McpReadFileTool.InputSchema, McpWriteFileTool.InputSchema, McpEchoTool.InputSchema];
        for (int index = 0; index < tools.Length; index++)
        {
            Assert.IsTrue(JsonElement.DeepEquals(JsonDocument.Parse(schemas[index]).RootElement, tools[index].GetProperty("inputSchema")), tools[index].ToString());
        }

        Assert.IsFalse(JsonDocument.Parse(response).RootElement.GetProperty("result").TryGetProperty("nextCursor", out _));
    }

    [TestMethod]
    public async Task ToolsList_EveryOptionOn_WritesEachToolsTitleAndDescription()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Everything);

        string response = await harness.AnswerAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        StringAssert.Contains(response, """{"name":"list_directory","title":"List a folder","description":"List the files and folders in a folder Surl serves.","inputSchema":""");
        StringAssert.Contains(response, """{"name":"read_file","title":"Read a file","description":"Read one file Surl serves.","inputSchema":""");
        StringAssert.Contains(response, """{"name":"write_file","title":"Write a file","description":"Create or replace one file Surl serves.","inputSchema":""");
        StringAssert.Contains(response, """{"name":"echo","title":"Echo","description":"Return the text it is given, unchanged.","inputSchema":{"type":"object","properties":{"text":{"type":"string","description":"The text to return."}},"required":["text"]}}""");
    }

    [TestMethod]
    public async Task ToolsList_DefaultServer_ListsOnlyReadFileAndEcho()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();

        CollectionAssert.AreEqual(new[] { "read_file", "echo" }, harness.Registry.Tools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public async Task Initialize_WithTools_AdvertisesTheToolsCapability()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();
        McpServerSession session = new(harness.Registry, McpServerSession.CreateOptions("1.0", McpSessionOptions.DefaultMaximumMessageBytes));

        byte[]? response = await session.AnswerAsync(
            System.Text.Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","id":0,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{}}}"""),
            CancellationToken.None);

        StringAssert.Contains(System.Text.Encoding.UTF8.GetString(response!), "\"capabilities\":{\"tools\":{\"listChanged\":false}}");
    }

    [TestMethod]
    public void CreateRegistry_NullStoreOrCaller_Throws()
    {
        ContentStore store = new(McpToolHarness.Root, new InMemoryContentFileSystem(TimeProvider.System), new ContentExposureOptions());

        Assert.ThrowsExactly<ArgumentNullException>(() => McpServerTools.CreateRegistry(null!, ContentCaller.Anonymous("mcp"), 10));
        Assert.ThrowsExactly<ArgumentNullException>(() => McpServerTools.CreateRegistry(store, null!, 10));
    }
}
