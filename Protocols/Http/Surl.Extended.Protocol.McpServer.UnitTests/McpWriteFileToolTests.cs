using System.Text;
using System.Text.Json;
using Surl.Content;

namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpWriteFileToolTests
{
    private static readonly ContentExposureOptions Uploads = new() { AllowUploads = true };

    [TestMethod]
    public async Task Call_Text_WritesItAsUtf8AndSaysSo()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Uploads);

        string response = await harness.CallAsync("write_file", """{"path":"/notes.txt","text":"café"}""");

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"text","text":"wrote 5 bytes to notes.txt"}],"structuredContent":{"path":"notes.txt","size":5},"isError":false}}""",
            response);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("café"), harness.ReadFile("notes.txt"));
    }

    [TestMethod]
    public async Task Call_Base64_WritesTheDecodedBytesReplacingTheFile()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Uploads);
        harness.WriteFile("a.bin", "old contents");

        JsonElement result = await harness.CallResultAsync("write_file", """{"path":"a.bin","base64":"/wAB"}""");

        Assert.IsFalse(result.GetProperty("isError").GetBoolean());
        CollectionAssert.AreEqual(new byte[] { 0xFF, 0x00, 0x01 }, harness.ReadFile("a.bin"));
    }

    [TestMethod]
    [DataRow("""{"path":"a","text":"x","base64":"eA=="}""")]
    [DataRow("""{"path":"a"}""")]
    public async Task Call_BothOrNeitherOfTextAndBase64_IsErrorAndWritesNothing(string arguments)
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Uploads);

        await AssertFailureAsync(harness, arguments, McpToolFailureText.ExactlyOneOfTextAndBase64);
        Assert.IsFalse(harness.FileExists("a"));
    }

    [TestMethod]
    public async Task Call_Base64ThatDoesNotDecode_IsError()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Uploads);

        await AssertFailureAsync(harness, """{"path":"a","base64":"not base64!"}""", McpToolFailureText.Base64NotValid);
    }

    [TestMethod]
    public async Task Call_PathOutsideTheContentStore_IsErrorThePathIsRefused()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Uploads);

        await AssertFailureAsync(harness, """{"path":"../escape.txt","text":"x"}""", McpToolFailureText.PathRefused);
    }

    [TestMethod]
    public async Task Call_PastMaxFilesize_IsErrorAndKeepsTheFile()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 3 });
        harness.WriteFile("a.txt", "old");

        await AssertFailureAsync(harness, """{"path":"a.txt","text":"four"}""", McpToolFailureText.LargerThanMaxFilesize);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("old"), harness.ReadFile("a.txt"));
    }

    [TestMethod]
    public async Task Call_IntoAReadOnlyShare_IsErrorWritingIsNotPermitted()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(
            Uploads,
            shares: _ => [new ContentShare("archive", McpToolHarness.Root, new InMemoryContentFileSystem(TimeProvider.System)) { IsReadOnly = true }]);

        await AssertFailureAsync(harness, """{"path":"archive/a.txt","text":"x"}""", McpToolFailureText.WritingNotPermitted);
    }

    [TestMethod]
    public async Task Call_DotFileWithoutServeDotFiles_IsErrorWritingIsNotPermitted()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Uploads);

        await AssertFailureAsync(harness, """{"path":".hidden","text":"x"}""", McpToolFailureText.WritingNotPermitted);
        Assert.IsFalse(harness.FileExists(".hidden"));
    }

    [TestMethod]
    public async Task Call_WithoutAllowUploads_IsTheCoresUnknownTool()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();

        string response = await harness.CallAsync("write_file", """{"path":"a.txt","text":"x"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"error":{"code":-32602,"message":"Invalid params: unknown tool write_file"}}""", response);
        Assert.IsFalse(harness.FileExists("a.txt"));
    }

    [TestMethod]
    public async Task Call_AnUnknownArgument_IsTheCoresInvalidParams()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(Uploads);

        string response = await harness.CallAsync("write_file", """{"path":"a","text":"x","mode":"append"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"error":{"code":-32602,"message":"Invalid params: arguments.mode: not allowed"}}""", response);
    }

    private static async Task AssertFailureAsync(McpToolHarness harness, string arguments, string text)
    {
        JsonElement result = await harness.CallResultAsync("write_file", arguments);

        Assert.IsTrue(result.GetProperty("isError").GetBoolean());
        Assert.AreEqual(text, result.GetProperty("content")[0].GetProperty("text").GetString());
    }
}
