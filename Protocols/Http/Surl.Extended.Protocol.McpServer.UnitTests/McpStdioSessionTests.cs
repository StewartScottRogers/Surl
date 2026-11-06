using System.Text;
using Surl.Content;
using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpStdioSessionTests
{
    private const string Initialize =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"ExampleClient","version":"1.0.0"}}}""";

    private const string Initialized = """{"jsonrpc":"2.0","method":"notifications/initialized"}""";

    [TestMethod]
    public async Task RunAsync_InitializeToolsListAndToolsCall_AnswersEachOnOneLineAndEndsWhenTheInputCloses()
    {
        InMemoryContentFileSystem fileSystem = new(TimeProvider.System);
        WriteFile(fileSystem, "hello.txt", "Hello, stdio");
        ContentStore store = new(InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions(), [], []);

        string[] lines = await RunAsync(
            store,
            Initialize,
            Initialized,
            """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""",
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"read_file","arguments":{"path":"hello.txt"}}}""",
            """{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"echo","arguments":{"text":"ping"}}}""");

        Assert.HasCount(4, lines);
        StringAssert.StartsWith(lines[0], """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18",""");
        StringAssert.Contains(lines[0], "\"serverInfo\":{\"name\":\"surl\",\"title\":\"Surl MCP server\",\"version\":\"1.2.3\"}");
        StringAssert.StartsWith(lines[1], """{"jsonrpc":"2.0","id":2,"result":{"tools":[{"name":"read_file",""");
        StringAssert.Contains(lines[1], "\"name\":\"echo\"");
        Assert.DoesNotContain("\"name\":\"write_file\"", lines[1]);
        Assert.DoesNotContain("\"name\":\"list_directory\"", lines[1]);
        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":3,"result":{"content":[{"type":"text","text":"Hello, stdio"}],"isError":false}}""",
            lines[2]);
        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":4,"result":{"content":[{"type":"text","text":"ping"}],"isError":false}}""",
            lines[3]);
    }

    [TestMethod]
    public async Task RunAsync_UploadsAllowed_WritesAFileAsTheAnonymousCaller()
    {
        InMemoryContentFileSystem fileSystem = new(TimeProvider.System);
        ContentStore store = new(
            InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions { AllowUploads = true }, [], []);

        string[] lines = await RunAsync(
            store,
            Initialize,
            """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"write_file","arguments":{"path":"up.txt","text":"sent"}}}""");

        Assert.HasCount(2, lines);
        StringAssert.Contains(lines[1], "\"isError\":false");
        using Stream written = fileSystem.OpenFileForAsyncRead(Path.Join(InMemoryContentFileSystem.RootPath, "up.txt"));
        using StreamReader reader = new(written);
        Assert.AreEqual("sent", await reader.ReadToEndAsync());
    }

    [TestMethod]
    public async Task RunAsync_InputClosesWithNoMessage_EndsCleanlyAndWritesNothing()
    {
        ContentStore store = new(
            InMemoryContentFileSystem.RootPath, new InMemoryContentFileSystem(TimeProvider.System), new ContentExposureOptions(), [], []);

        string[] lines = await RunAsync(store);

        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public async Task RunAsync_NoStore_ThrowsArgumentNullException()
    {
        using MemoryStream input = new();
        using MemoryStream output = new();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => McpStdioSession.RunAsync(input, output, null!, "1.2.3", McpSessionOptions.DefaultMaximumMessageBytes, CancellationToken.None));
    }

    // The input is a finished stream, so the session reads every line, then sees the input closed and ends.
    private static async Task<string[]> RunAsync(ContentStore store, params string[] messages)
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(string.Concat(messages.Select(message => message + "\n"))));
        using MemoryStream output = new();

        await McpStdioSession.RunAsync(input, output, store, "1.2.3", McpSessionOptions.DefaultMaximumMessageBytes, CancellationToken.None);

        string written = Encoding.UTF8.GetString(output.ToArray());
        Assert.IsTrue(written.Length == 0 || written.EndsWith('\n'), "Every response ends its line.");
        return written.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private static void WriteFile(InMemoryContentFileSystem fileSystem, string relativePath, string text)
    {
        using Stream stream = fileSystem.CreateFileForAsyncWrite(Path.Join(InMemoryContentFileSystem.RootPath, relativePath));
        stream.Write(Encoding.UTF8.GetBytes(text));
    }
}
