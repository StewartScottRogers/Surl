using System.Text;
using System.Text.Json;
using Surl.Content;
using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// One initialized session of the MCP server's tools over an in-memory content store, answered through
/// <see cref="McpServerSession" /> as a transport would feed it.
/// </summary>
internal sealed class McpToolHarness
{
    // The session's own limit stays at the default so a small read limit never refuses the request itself.
    private readonly McpServerSession _session;

    private McpToolHarness(ContentStore store, ContentCaller caller, int readLimit, InMemoryContentFileSystem fileSystem)
    {
        FileSystem = fileSystem;
        Store = store;
        Registry = McpServerTools.CreateRegistry(store, caller, readLimit);
        _session = new McpServerSession(Registry, McpServerSession.CreateOptions("1.0", McpSessionOptions.DefaultMaximumMessageBytes));
    }

    public static string Root => InMemoryContentFileSystem.RootPath;

    public InMemoryContentFileSystem FileSystem { get; }

    public ContentStore Store { get; }

    public McpServerRegistry Registry { get; }

    public static async Task<McpToolHarness> StartAsync(
        ContentExposureOptions? exposure = null,
        int readLimit = McpSessionOptions.DefaultMaximumMessageBytes,
        ContentCaller? caller = null,
        Func<InMemoryContentFileSystem, IReadOnlyList<ContentShare>>? shares = null,
        string protocolVersion = "2025-06-18")
    {
        InMemoryContentFileSystem fileSystem = new(TimeProvider.System);
        ContentStore store = new(Root, fileSystem, exposure ?? new ContentExposureOptions(), shares?.Invoke(fileSystem) ?? [], []);
        McpToolHarness harness = new(store, caller ?? ContentCaller.Anonymous("mcp"), readLimit, fileSystem);
        await harness.AnswerAsync(
            """{"jsonrpc":"2.0","id":0,"method":"initialize","params":{"protocolVersion":"VERSION","capabilities":{}}}""".Replace("VERSION", protocolVersion, StringComparison.Ordinal));
        return harness;
    }

    public void CreateFolder(string relativePath) => FileSystem.CreateDirectory(Path.Join(Root, relativePath));

    public void WriteFile(string relativePath, string text) => WriteFile(relativePath, Encoding.UTF8.GetBytes(text));

    public void WriteFile(string relativePath, byte[] bytes)
    {
        using Stream stream = FileSystem.CreateFileForAsyncWrite(Path.Join(Root, relativePath));
        stream.Write(bytes);
    }

    public byte[] ReadFile(string relativePath)
    {
        using Stream stream = FileSystem.OpenFileForAsyncRead(Path.Join(Root, relativePath));
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    public bool FileExists(string relativePath) =>
        FileSystem.GetEntryKind(Path.Join(Root, relativePath)) == ContentEntryKind.File;

    public Task<string> CallAsync(string tool, string arguments) =>
        AnswerAsync($$$"""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"{{{tool}}}","arguments":{{{arguments}}}}}""");

    public async Task<JsonElement> CallResultAsync(string tool, string arguments)
    {
        string response = await CallAsync(tool, arguments);
        return JsonDocument.Parse(response).RootElement.GetProperty("result").Clone();
    }

    public async Task<string> AnswerAsync(string message)
    {
        byte[]? response = await _session.AnswerAsync(Encoding.UTF8.GetBytes(message), CancellationToken.None);
        Assert.IsNotNull(response);
        return Encoding.UTF8.GetString(response);
    }
}
