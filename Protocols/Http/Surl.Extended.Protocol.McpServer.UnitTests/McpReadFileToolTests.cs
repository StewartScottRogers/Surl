using System.Text.Json;
using Surl.Content;

namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpReadFileToolTests
{
    [TestMethod]
    public async Task Call_Utf8File_ReturnsOneTextBlockInThePagesShape()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();
        harness.CreateFolder("docs");
        harness.WriteFile("docs/a.txt", "café\n");

        string response = await harness.CallAsync("read_file", """{"path":"docs/a.txt"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"text","text":"caf\u00E9\n"}],"isError":false}}""", response);
    }

    [TestMethod]
    public async Task Call_LeadingSlash_IsIgnored()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();
        harness.WriteFile("a.txt", "x");

        JsonElement result = await harness.CallResultAsync("read_file", """{"path":"/a.txt"}""");

        Assert.AreEqual("x", result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task Call_NameWithASpaceAndAPercent_ReadsThatFile()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();
        harness.WriteFile("a b%41.txt", "spaced");

        JsonElement result = await harness.CallResultAsync("read_file", """{"path":"a b%41.txt"}""");

        Assert.AreEqual("spaced", result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task Call_BinaryFile_ReturnsOneEmbeddedResourceWithItsBytes()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();
        harness.WriteFile("x y.bin", [0xFF, 0x00, 0x01]);

        string response = await harness.CallAsync("read_file", """{"path":"x y.bin"}""");

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"resource","resource":{"uri":"file:///x%20y.bin","mimeType":"application/octet-stream","blob":"/wAB"}}],"isError":false}}""",
            response);
    }

    [TestMethod]
    public async Task Call_EmptyFile_IsAnEmptyTextBlock()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();
        harness.WriteFile("empty", []);

        JsonElement result = await harness.CallResultAsync("read_file", """{"path":"empty"}""");

        Assert.AreEqual(string.Empty, result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task Call_MissingFile_IsErrorNoSuchFileOrFolder()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();

        string response = await harness.CallAsync("read_file", """{"path":"missing.txt"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"text","text":"no such file or folder"}],"isError":true}}""", response);
    }

    [TestMethod]
    public async Task Call_DotFileWithoutServeDotFiles_IsAnsweredAsTheContentStoreAnswersIt_Missing()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();
        harness.WriteFile(".secret", "x");

        await AssertFailureAsync(harness, """{"path":".secret"}""", McpToolFailureText.NoSuchFileOrFolder);
    }

    [TestMethod]
    public async Task Call_ShareTheCallerIsNotGranted_IsAnsweredAsMissing()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(shares: _ =>
        {
            InMemoryContentFileSystem shareFileSystem = new(TimeProvider.System);
            using (Stream stream = shareFileSystem.CreateFileForAsyncWrite(Path.Join(McpToolHarness.Root, "a.txt")))
            {
                stream.WriteByte((byte)'x');
            }

            return [new ContentShare("team", McpToolHarness.Root, shareFileSystem) { Grants = new Dictionary<string, ContentShareAccess> { ["alice"] = ContentShareAccess.Read } }];
        });

        await AssertFailureAsync(harness, """{"path":"team/a.txt"}""", McpToolFailureText.NoSuchFileOrFolder);
    }

    [TestMethod]
    [DataRow("""{"path":"../etc/passwd"}""")]
    [DataRow("""{"path":"a/../../b"}""")]
    [DataRow("""{"path":"C:/Windows/win.ini"}""")]
    [DataRow("""{"path":"//server/share"}""")]
    [DataRow("""{"path":"a\\b"}""")]
    [DataRow("""{"path":"NUL"}""")]
    public async Task Call_PathOutsideTheContentStore_IsErrorThePathIsRefused(string arguments)
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();

        await AssertFailureAsync(harness, arguments, McpToolFailureText.PathRefused);
    }

    [TestMethod]
    public async Task Call_Folder_IsErrorThePathIsAFolder()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();
        harness.CreateFolder("docs");

        await AssertFailureAsync(harness, """{"path":"docs"}""", McpToolFailureText.IsAFolder);
    }

    [TestMethod]
    public async Task Call_FileLongerThanTheMessageLimit_IsErrorWithoutReadingIt()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(readLimit: 4);
        harness.WriteFile("a.txt", "12345");

        await AssertFailureAsync(harness, """{"path":"a.txt"}""", McpToolFailureText.LargerThanOneMessage);
    }

    [TestMethod]
    public async Task Call_TextWhoseEscapedAnswerPassesTheMessageLimit_IsError()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(readLimit: 6);
        harness.WriteFile("q.txt", "\"\"\"\"");

        await AssertFailureAsync(harness, """{"path":"q.txt"}""", McpToolFailureText.LargerThanOneMessage);
    }

    [TestMethod]
    public async Task Call_BinaryWhoseBase64PassesTheMessageLimit_IsError()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(readLimit: 4);
        harness.WriteFile("b.bin", [0xFF, 0xFE, 0xFD, 0xFC]);

        await AssertFailureAsync(harness, """{"path":"b.bin"}""", McpToolFailureText.LargerThanOneMessage);
    }

    [TestMethod]
    public async Task Call_TextExactlyAtTheMessageLimit_IsRead()
    {
        McpToolHarness harness = await McpToolHarness.StartAsync(readLimit: 4);
        harness.WriteFile("a.txt", "abcd");

        JsonElement result = await harness.CallResultAsync("read_file", """{"path":"a.txt"}""");

        Assert.IsFalse(result.GetProperty("isError").GetBoolean());
    }

    [TestMethod]
    [DataRow("{}", "Invalid params: arguments.path: required")]
    [DataRow("""{"path":""}""", "Invalid params: arguments.path: expected at least 1 characters")]
    [DataRow("""{"path":7}""", "Invalid params: arguments.path: expected a string")]
    public async Task Call_BadArguments_IsTheCoresInvalidParams(string arguments, string message)
    {
        McpToolHarness harness = await McpToolHarness.StartAsync();

        string response = await harness.CallAsync("read_file", arguments);

        Assert.AreEqual($$$"""{"jsonrpc":"2.0","id":1,"error":{"code":-32602,"message":"{{{message}}}"}}""", response);
    }

    private static async Task AssertFailureAsync(McpToolHarness harness, string arguments, string text)
    {
        JsonElement result = await harness.CallResultAsync("read_file", arguments);

        Assert.IsTrue(result.GetProperty("isError").GetBoolean());
        Assert.AreEqual(text, result.GetProperty("content")[0].GetProperty("text").GetString());
    }
}
