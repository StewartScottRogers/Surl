using System.Text;
using System.Text.Json;

namespace Surl.McpServing;

[TestClass]
public sealed class McpResourceMethodsTests
{
    private const string InitializeLatest =
        """{"jsonrpc":"2.0","id":0,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{}}}""";

    private const string InitializeOldest =
        """{"jsonrpc":"2.0","id":0,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{}}}""";

    private const string MainRsUri = "file:///project/src/main.rs";

    private const string MainRsText = "fn main() {\n    println!(\"Hello world!\");\n}";

    [TestMethod]
    public async Task Initialize_ResourceProviderAdded_AdvertisesResources()
    {
        McpServerRegistry registry = MainRsRegistry();
        McpRequestDispatcher dispatcher = new(registry, new McpSessionOptions(new McpServerInfo("s", null, "1")));

        string response = await AnswerAsync(dispatcher, InitializeLatest);

        StringAssert.Contains(response, """{"protocolVersion":"2025-06-18","capabilities":{"resources":{"subscribe":false,"listChanged":false}}""");
    }

    [TestMethod]
    public async Task Initialize_NoResourceAdded_DoesNotAdvertiseResources()
    {
        McpServerRegistry registry = new();
        registry.AddResources(new UnitTestResourceProvider());
        McpRequestDispatcher dispatcher = new(registry, new McpSessionOptions(new McpServerInfo("s", null, "1")));

        string response = await AnswerAsync(dispatcher, InitializeLatest);

        StringAssert.Contains(response, """{"protocolVersion":"2025-06-18","capabilities":{}""");
    }

    // The "Listing Resources" example (MCP 2025-06-18, server/resources).
    [TestMethod]
    public async Task AnswerList_ResourcesPageExample_ListsTheResourceInThePagesShape()
    {
        McpRequestDispatcher dispatcher = await InitializedAsync(MainRsRegistry(), InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":1,"method":"resources/list","params":{}}""");

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":1,"result":{"resources":[{"uri":"file:///project/src/main.rs","name":"main.rs","title":"Rust Software Application Main File","description":"Primary application entry point","mimeType":"text/x-rust"}]}}""",
            response);
    }

    [TestMethod]
    public async Task AnswerList_OlderRevisionAndNoOptionalFields_WritesUriAndNameOnly()
    {
        McpServerRegistry registry = new();
        registry.AddResources(new UnitTestResourceProvider(new McpResource("surl://a", "a", "A", null, null)));
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeOldest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":1,"method":"resources/list"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"result":{"resources":[{"uri":"surl://a","name":"a"}]}}""", response);
    }

    [TestMethod]
    public async Task AnswerList_PageSizeSmallerThanTheResourceCount_ReturnsNextCursorAndTheNextPageReturnsTheRest()
    {
        McpServerRegistry registry = new();
        registry.AddResources(new UnitTestResourceProvider(Resource("surl://a"), Resource("surl://b")));
        registry.AddResources(new UnitTestResourceProvider(Resource("surl://c")));
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeLatest, listPageSize: 2);

        string first = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":1,"method":"resources/list"}""");
        string cursor = JsonDocument.Parse(first).RootElement.GetProperty("result").GetProperty("nextCursor").GetString()!;
        string second = await AnswerAsync(dispatcher, $$$"""{"jsonrpc":"2.0","id":2,"method":"resources/list","params":{"cursor":"{{{cursor}}}"}}""");

        Assert.AreEqual("Mg", cursor);
        CollectionAssert.AreEqual(new[] { "surl://a", "surl://b" }, ResourceUris(first));
        CollectionAssert.AreEqual(new[] { "surl://c" }, ResourceUris(second));
        Assert.IsFalse(JsonDocument.Parse(second).RootElement.GetProperty("result").TryGetProperty("nextCursor", out _));
    }

    [TestMethod]
    [DataRow("""{"cursor":"MA"}""")]
    [DataRow("""{"cursor":7}""")]
    [DataRow("""[]""")]
    public async Task AnswerList_InvalidCursor_AnswersInvalidParams(string parameters)
    {
        McpRequestDispatcher dispatcher = await InitializedAsync(MainRsRegistry(), InitializeLatest);

        string response = await AnswerAsync(dispatcher, $$"""{"jsonrpc":"2.0","id":1,"method":"resources/list","params":{{parameters}}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"error":{"code":-32602,"message":"Invalid params: cursor"}}""", response);
    }

    // The "Reading Resources" example (MCP 2025-06-18, server/resources).
    [TestMethod]
    public async Task AnswerRead_ResourcesPageExample_ReturnsItsContentsInThePagesShape()
    {
        McpRequestDispatcher dispatcher = await InitializedAsync(MainRsRegistry(), InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":2,"method":"resources/read","params":{"uri":"file:///project/src/main.rs"}}""");

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":2,"result":{"contents":[{"uri":"file:///project/src/main.rs","name":"main.rs","title":"Rust Software Application Main File","mimeType":"text/x-rust","text":"fn main() {\n    println!(\u0022Hello world!\u0022);\n}"}]}}""",
            response);
    }

    [TestMethod]
    public async Task AnswerRead_OlderRevisionAndNoMimeType_WritesNoTitleOrMimeType()
    {
        McpServerRegistry registry = new();
        registry.AddResources(new UnitTestResourceProvider(new McpResource("surl://a", "a", "A", "d", null)));
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeOldest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":2,"method":"resources/read","params":{"uri":"surl://a"}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":2,"result":{"contents":[{"uri":"surl://a","name":"a","text":"text of surl://a"}]}}""", response);
    }

    // The "Error Handling" example (MCP 2025-06-18, server/resources).
    [TestMethod]
    public async Task AnswerRead_UnknownUri_AnswersResourceNotFoundWithTheUri()
    {
        McpRequestDispatcher dispatcher = await InitializedAsync(MainRsRegistry(), InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":5,"method":"resources/read","params":{"uri":"file:///nonexistent.txt"}}""");

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":5,"error":{"code":-32002,"message":"Resource not found","data":{"uri":"file:///nonexistent.txt"}}}""",
            response);
    }

    [TestMethod]
    public async Task AnswerRead_ProviderReturnsNull_AnswersResourceNotFound()
    {
        McpServerRegistry registry = new();
        registry.AddResources(new UnitTestResourceProvider(Resource("surl://gone")) { ReadText = (_, _) => Task.FromResult<string?>(null) });
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":5,"method":"resources/read","params":{"uri":"surl://gone"}}""");

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":5,"error":{"code":-32002,"message":"Resource not found","data":{"uri":"surl://gone"}}}""",
            response);
    }

    [TestMethod]
    [DataRow("""{}""")]
    [DataRow("""{"uri":7}""")]
    [DataRow("""[]""")]
    [DataRow(null)]
    public async Task AnswerRead_MissingUri_AnswersInvalidParams(string? parameters)
    {
        McpRequestDispatcher dispatcher = await InitializedAsync(MainRsRegistry(), InitializeLatest);
        string paramsMember = parameters is null ? string.Empty : $",\"params\":{parameters}";

        string response = await AnswerAsync(dispatcher, $$"""{"jsonrpc":"2.0","id":6,"method":"resources/read"{{paramsMember}}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":6,"error":{"code":-32602,"message":"Invalid params: uri"}}""", response);
    }

    [TestMethod]
    public async Task AnswerRead_ProviderThrows_AnswersInternalErrorWithoutTheExceptionText()
    {
        McpServerRegistry registry = new();
        registry.AddResources(new UnitTestResourceProvider(Resource("surl://a")) { ReadText = (_, _) => throw new InvalidOperationException("C:\\secret") });
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":7,"method":"resources/read","params":{"uri":"surl://a"}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":7,"error":{"code":-32603,"message":"Internal error"}}""", response);
    }

    [TestMethod]
    public async Task AnswerRead_CancelledThroughItsToken_GetsNoResponse()
    {
        McpServerRegistry registry = new();
        registry.AddResources(new UnitTestResourceProvider(Resource("surl://a"))
        {
            ReadText = (_, token) => Task.FromCanceled<string?>(token),
        });
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeLatest);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        byte[]? response = await dispatcher.AnswerAsync(
            Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","id":8,"method":"resources/read","params":{"uri":"surl://a"}}"""), cancellation.Token);

        Assert.IsNull(response);
    }

    [TestMethod]
    public async Task AnswerRead_ProviderThrowsCancellationWithoutItsTokenCancelled_AnswersInternalError()
    {
        McpServerRegistry registry = new();
        registry.AddResources(new UnitTestResourceProvider(Resource("surl://a")) { ReadText = (_, _) => throw new OperationCanceledException() });
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":9,"method":"resources/read","params":{"uri":"surl://a"}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":9,"error":{"code":-32603,"message":"Internal error"}}""", response);
    }

    [TestMethod]
    public async Task AnswerRead_BeforeInitialize_IsRefusedAsNotInitialized()
    {
        McpRequestDispatcher dispatcher = new(MainRsRegistry(), new McpSessionOptions(new McpServerInfo("s", null, "1")));

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":9,"method":"resources/read","params":{"uri":"file:///project/src/main.rs"}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":9,"error":{"code":-32600,"message":"Invalid Request: the session is not initialized"}}""", response);
    }

    private static McpServerRegistry MainRsRegistry()
    {
        McpServerRegistry registry = new();
        registry.AddResources(new UnitTestResourceProvider(new McpResource(
            MainRsUri, "main.rs", "Rust Software Application Main File", "Primary application entry point", "text/x-rust"))
        {
            ReadText = (uri, _) => Task.FromResult<string?>(uri == MainRsUri ? MainRsText : null),
        });
        return registry;
    }

    private static McpResource Resource(string uri) => new(uri, uri, null, null, "text/plain");

    private static string[] ResourceUris(string response) =>
        [.. JsonDocument.Parse(response).RootElement.GetProperty("result").GetProperty("resources").EnumerateArray()
            .Select(resource => resource.GetProperty("uri").GetString()!)];

    private static async Task<McpRequestDispatcher> InitializedAsync(McpServerRegistry registry, string initialize, int listPageSize = McpSessionOptions.DefaultListPageSize)
    {
        McpRequestDispatcher dispatcher = new(registry, new McpSessionOptions(new McpServerInfo("s", null, "1"), listPageSize: listPageSize));
        await AnswerAsync(dispatcher, initialize);
        return dispatcher;
    }

    private static async Task<string> AnswerAsync(McpRequestDispatcher dispatcher, string message) =>
        Encoding.UTF8.GetString((await dispatcher.AnswerAsync(Encoding.UTF8.GetBytes(message), CancellationToken.None))!);
}
