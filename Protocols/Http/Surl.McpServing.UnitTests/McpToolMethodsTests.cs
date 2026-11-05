using System.Text;
using System.Text.Json;

namespace Surl.McpServing;

[TestClass]
public sealed class McpToolMethodsTests
{
    private const string InitializeLatest =
        """{"jsonrpc":"2.0","id":0,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{}}}""";

    private const string InitializeOldest =
        """{"jsonrpc":"2.0","id":0,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{}}}""";

    // The "Tools" page's get_weather tool (MCP 2025-06-18, server/tools), its schema as the page writes it.
    private const string WeatherSchema =
        """
        {
          "type": "object",
          "properties": {
            "location": {
              "type": "string",
              "description": "City name or zip code"
            }
          },
          "required": ["location"]
        }
        """;

    private const string WeatherText =
        "Current weather in New York:\nTemperature: 72°F\nConditions: Partly cloudy";

    [TestMethod]
    public async Task AnswerList_ToolsPageExample_ListsTheToolInThePagesShape()
    {
        McpRequestDispatcher dispatcher = await InitializedAsync(WeatherRegistry(), InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":1,"result":{"tools":[{"name":"get_weather","title":"Weather Information Provider","description":"Get current weather information for a location","inputSchema":{"type":"object","properties":{"location":{"type":"string","description":"City name or zip code"}},"required":["location"]}}]}}""",
            response);
    }

    [TestMethod]
    public async Task AnswerList_OlderRevision_WritesNoTitle()
    {
        McpRequestDispatcher dispatcher = await InitializedAsync(WeatherRegistry(), InitializeOldest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        StringAssert.Contains(response, """{"name":"get_weather","description":""");
    }

    [TestMethod]
    public async Task AnswerList_PageSizeSmallerThanTheToolCount_ReturnsNextCursorAndTheNextPageReturnsTheRest()
    {
        McpServerRegistry registry = new();
        foreach (string name in new[] { "a", "b", "c" })
        {
            registry.AddTool(Tool(name, """{"type":"object"}"""));
        }

        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeLatest, listPageSize: 2);

        string first = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");
        string cursor = JsonDocument.Parse(first).RootElement.GetProperty("result").GetProperty("nextCursor").GetString()!;
        string second = await AnswerAsync(dispatcher, $$$"""{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{"cursor":"{{{cursor}}}"}}""");

        Assert.AreEqual("Mg", cursor);
        CollectionAssert.AreEqual(new[] { "a", "b" }, ToolNames(first));
        CollectionAssert.AreEqual(new[] { "c" }, ToolNames(second));
        Assert.IsFalse(JsonDocument.Parse(second).RootElement.GetProperty("result").TryGetProperty("nextCursor", out _));
    }

    [TestMethod]
    public async Task AnswerList_NoToolsRegistered_ListsNone()
    {
        McpRequestDispatcher dispatcher = await InitializedAsync(new McpServerRegistry(), InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"result":{"tools":[]}}""", response);
    }

    [TestMethod]
    [DataRow("""{"cursor":"not a cursor!"}""")]
    [DataRow("""{"cursor":"MA"}""")]
    [DataRow("""{"cursor":"Mw"}""")]
    [DataRow("""{"cursor":7}""")]
    [DataRow("""[]""")]
    public async Task AnswerList_InvalidCursor_AnswersInvalidParams(string parameters)
    {
        McpRequestDispatcher dispatcher = await InitializedAsync(WeatherRegistry(), InitializeLatest);

        string response = await AnswerAsync(dispatcher, $$"""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{{parameters}}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"error":{"code":-32602,"message":"Invalid params: cursor"}}""", response);
    }

    [TestMethod]
    public async Task AnswerCall_ToolsPageExampleWithGoodArguments_ReturnsTheHandlersContent()
    {
        McpRequestDispatcher dispatcher = await InitializedAsync(WeatherRegistry(), InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_weather","arguments":{"location":"New York"}}}""");

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":2,"result":{"content":[{"type":"text","text":"Current weather in New York:\nTemperature: 72\u00B0F\nConditions: Partly cloudy"}],"isError":false}}""",
            response);
    }

    [TestMethod]
    [DataRow("""{"name":"get_weather"}""", "Invalid params: arguments.location: required")]
    [DataRow("""{"name":"get_weather","arguments":null}""", "Invalid params: arguments.location: required")]
    [DataRow("""{"name":"get_weather","arguments":{"location":7}}""", "Invalid params: arguments.location: expected a string")]
    [DataRow("""{"name":"get_weather","arguments":[]}""", "Invalid params: arguments: expected an object")]
    [DataRow("""{"name":"no_such_tool","arguments":{}}""", "Invalid params: unknown tool no_such_tool")]
    [DataRow("""{"arguments":{}}""", "Invalid params: name")]
    [DataRow("""{"name":7}""", "Invalid params: name")]
    [DataRow("""[]""", "Invalid params: name")]
    public async Task AnswerCall_BadParamsOrArguments_AnswersInvalidParamsWithoutRunningTheHandler(string parameters, string message)
    {
        int handlerRuns = 0;
        McpServerRegistry registry = new();
        registry.AddTool(new McpTool("get_weather", null, "d", WeatherSchema, (_, _) =>
        {
            handlerRuns++;
            return Task.FromResult(McpToolResult.FromText("x"));
        }));
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeLatest);

        string response = await AnswerAsync(dispatcher, $$"""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{{parameters}}}""");

        Assert.AreEqual($$$"""{"jsonrpc":"2.0","id":3,"error":{"code":-32602,"message":"{{{message}}}"}}""", response);
        Assert.AreEqual(0, handlerRuns);
    }

    [TestMethod]
    public async Task AnswerCall_NoParams_AnswersInvalidParamsName()
    {
        McpRequestDispatcher dispatcher = await InitializedAsync(WeatherRegistry(), InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":3,"method":"tools/call"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":3,"error":{"code":-32602,"message":"Invalid params: name"}}""", response);
    }

    [TestMethod]
    public async Task AnswerCall_MissingArguments_GivesTheHandlerTheEmptyObject()
    {
        string? given = null;
        McpServerRegistry registry = new();
        registry.AddTool(new McpTool("t", null, "d", """{"type":"object"}""", (arguments, _) =>
        {
            given = arguments.GetRawText();
            return Task.FromResult(McpToolResult.FromText("ok"));
        }));
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeLatest);

        await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"t"}}""");

        Assert.AreEqual("{}", given);
    }

    [TestMethod]
    public async Task AnswerCall_HandlerThrowsToolFailure_AnswersIsErrorWithItsMessage()
    {
        McpServerRegistry registry = new();
        registry.AddTool(new McpTool("t", null, "d", """{"type":"object"}""", (_, _) => throw new McpToolFailureException("the port is in use")));
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"t"}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":4,"result":{"content":[{"type":"text","text":"the port is in use"}],"isError":true}}""", response);
    }

    [TestMethod]
    public async Task AnswerCall_HandlerReturnsAFailure_AnswersIsError()
    {
        McpServerRegistry registry = new();
        registry.AddTool(new McpTool("t", null, "d", """{"type":"object"}""", (_, _) => Task.FromResult(McpToolResult.FromFailure("no"))));
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"t"}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":4,"result":{"content":[{"type":"text","text":"no"}],"isError":true}}""", response);
    }

    [TestMethod]
    public async Task AnswerCall_HandlerThrowsAnythingElse_AnswersInternalErrorWithoutItsText()
    {
        McpServerRegistry registry = new();
        registry.AddTool(new McpTool("t", null, "d", """{"type":"object"}""", async (_, _) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("C:\\secret\\path");
        }));
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"t"}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":5,"error":{"code":-32603,"message":"Internal error"}}""", response);
    }

    [TestMethod]
    public async Task AnswerCall_HandlerCancelledThroughItsToken_GetsNoResponse()
    {
        McpServerRegistry registry = new();
        registry.AddTool(new McpTool("t", null, "d", """{"type":"object"}""", (_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(McpToolResult.FromText("never"));
        }));
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeLatest);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        byte[]? response = await dispatcher.AnswerAsync(
            Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"t"}}"""), cancellation.Token);

        Assert.IsNull(response);
    }

    [TestMethod]
    public async Task AnswerCall_HandlerCancelsWhileTheRequestIsNotCancelled_AnswersInternalError()
    {
        McpServerRegistry registry = new();
        registry.AddTool(new McpTool("t", null, "d", """{"type":"object"}""", (_, _) => throw new OperationCanceledException()));
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"t"}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":7,"error":{"code":-32603,"message":"Internal error"}}""", response);
    }

    [TestMethod]
    [DataRow(InitializeLatest, """{"jsonrpc":"2.0","id":8,"result":{"content":[{"type":"text","text":"a"},{"type":"text","text":"b"}],"structuredContent":{"n":1},"isError":false}}""")]
    [DataRow(InitializeOldest, """{"jsonrpc":"2.0","id":8,"result":{"content":[{"type":"text","text":"a"},{"type":"text","text":"b"}],"isError":false}}""")]
    public async Task AnswerCall_StructuredContent_IsWrittenOnlyForTheLatestRevision(string initialize, string expected)
    {
        JsonElement structured = JsonDocument.Parse("""{"n":1}""").RootElement;
        McpServerRegistry registry = new();
        registry.AddTool(new McpTool("t", null, "d", """{"type":"object"}""", (_, _) => Task.FromResult(new McpToolResult(["a", "b"], structured))));
        McpRequestDispatcher dispatcher = await InitializedAsync(registry, initialize);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":8,"method":"tools/call","params":{"name":"t"}}""");

        Assert.AreEqual(expected, response);
    }

    [TestMethod]
    public async Task AnswerCall_BeforeInitialize_IsRefusedAsNotInitialized()
    {
        McpRequestDispatcher dispatcher = new(WeatherRegistry(), new McpSessionOptions(new McpServerInfo("s", null, "1")));

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"get_weather"}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":9,"error":{"code":-32600,"message":"Invalid Request: the session is not initialized"}}""", response);
    }

    private static McpServerRegistry WeatherRegistry()
    {
        McpServerRegistry registry = new();
        registry.AddTool(new McpTool(
            "get_weather",
            "Weather Information Provider",
            "Get current weather information for a location",
            WeatherSchema,
            (_, _) => Task.FromResult(McpToolResult.FromText(WeatherText))));
        return registry;
    }

    private static McpTool Tool(string name, string schema) =>
        new(name, null, "d", schema, (_, _) => Task.FromResult(McpToolResult.FromText(name)));

    private static string[] ToolNames(string response) =>
        [.. JsonDocument.Parse(response).RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!)];

    private static async Task<McpRequestDispatcher> InitializedAsync(McpServerRegistry registry, string initialize, int listPageSize = McpSessionOptions.DefaultListPageSize)
    {
        McpRequestDispatcher dispatcher = new(registry, new McpSessionOptions(new McpServerInfo("s", null, "1"), listPageSize: listPageSize));
        await AnswerAsync(dispatcher, initialize);
        return dispatcher;
    }

    private static async Task<string> AnswerAsync(McpRequestDispatcher dispatcher, string message) =>
        Encoding.UTF8.GetString((await dispatcher.AnswerAsync(Encoding.UTF8.GetBytes(message), CancellationToken.None))!);
}
