using System.Text.Json;
using Surl.Content;

namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpListDirectoryToolTests
{
    private static readonly ContentExposureOptions Listing = new() { ListDirectories = true };

    [TestMethod]
    public async Task Call_Root_ListsEntriesInOrdinalOrderAsTextAndStructuredContent()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Listing);
        harness.CreateFolder("docs");
        harness.WriteFile("b.txt", "12");
        harness.WriteFile("A.txt", "1");

        string response = await harness.CallAsync("list_directory", "{}");

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"text","text":"A.txt\nb.txt\ndocs/"}],"structuredContent":{"entries":[{"name":"A.txt","kind":"file","size":1},{"name":"b.txt","kind":"file","size":2},{"name":"docs","kind":"directory"}]},"isError":false}}""",
            response);
    }

    [TestMethod]
    public async Task Call_OlderRevision_WritesNoStructuredContent()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Listing, protocolVersion: "2025-03-26");
        harness.WriteFile("a", "1");

        string response = await harness.CallAsync("list_directory", """{"path":""}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"text","text":"a"}],"isError":false}}""", response);
    }

    [TestMethod]
    public async Task Call_Subfolder_ListsIt()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Listing);
        harness.CreateFolder("docs");
        harness.WriteFile("docs/x", "1");

        JsonElement result = await harness.CallResultAsync("list_directory", """{"path":"/docs"}""");

        Assert.AreEqual("x", result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task Call_EmptyFolder_IsOneEmptyTextBlock()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Listing);

        JsonElement result = await harness.CallResultAsync("list_directory", "{}");

        Assert.AreEqual(string.Empty, result.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.AreEqual(0, result.GetProperty("structuredContent").GetProperty("entries").GetArrayLength());
    }

    [TestMethod]
    public async Task Call_MoreThanTheMaximumEntries_IsTruncatedAndFlagged()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Listing);
        for (int index = 0; index <= McpListDirectoryTool.MaximumEntries; index++)
        {
            harness.WriteFile($"f{index:D4}", []);
        }

        JsonElement result = await harness.CallResultAsync("list_directory", "{}");

        string[] lines = result.GetProperty("content")[0].GetProperty("text").GetString()!.Split('\n');
        Assert.HasCount(McpListDirectoryTool.MaximumEntries + 1, lines);
        Assert.AreEqual("...", lines[^1]);
        Assert.AreEqual(McpListDirectoryTool.MaximumEntries, result.GetProperty("structuredContent").GetProperty("entries").GetArrayLength());
        Assert.IsTrue(result.GetProperty("structuredContent").GetProperty("is_truncated").GetBoolean());
    }

    [TestMethod]
    public async Task Call_MissingFolder_IsErrorNoSuchFileOrFolder()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Listing);

        await AssertFailureAsync(harness, """{"path":"nowhere"}""", McpToolFailureText.NoSuchFileOrFolder);
    }

    [TestMethod]
    public async Task Call_File_IsErrorThePathIsAFile()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Listing);
        harness.WriteFile("a.txt", "1");

        await AssertFailureAsync(harness, """{"path":"a.txt"}""", McpToolFailureText.IsAFile);
    }

    [TestMethod]
    public async Task Call_PathOutsideTheContentStore_IsErrorThePathIsRefused()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Listing);

        await AssertFailureAsync(harness, """{"path":".."}""", McpToolFailureText.PathRefused);
    }

    [TestMethod]
    public async Task Call_WithoutListDirectories_IsTheCoresUnknownTool()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();

        string response = await harness.CallAsync("list_directory", "{}");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"error":{"code":-32602,"message":"Invalid params: unknown tool list_directory"}}""", response);
    }

    private static async Task AssertFailureAsync(McpToolHarness harness, string arguments, string text)
    {
        JsonElement result = await harness.CallResultAsync("list_directory", arguments);

        Assert.IsTrue(result.GetProperty("isError").GetBoolean());
        Assert.AreEqual(text, result.GetProperty("content")[0].GetProperty("text").GetString());
    }
}
