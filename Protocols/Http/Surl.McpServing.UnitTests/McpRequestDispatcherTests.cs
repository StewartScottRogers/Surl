using System.Text;

namespace Surl.McpServing;

[TestClass]
public sealed class McpRequestDispatcherTests
{
    // The "Lifecycle" page's initialize request (MCP 2025-06-18, basic/lifecycle), on one line.
    private const string LifecycleInitializeRequest =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{"roots":{"listChanged":true},"sampling":{},"elicitation":{}},"clientInfo":{"name":"ExampleClient","title":"Example Client Display Name","version":"1.0.0"}}}""";

    private const string InitializeLatest =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{}}}""";

    private static readonly McpServerInfo ExampleServer = new("ExampleServer", "Example Server Display Name", "1.0.0");

    [TestMethod]
    public async Task AnswerAsync_LifecycleInitializeExample_AnswersThePagesShapeWithTheCallersServerInfoAndCapabilities()
    {
        McpRequestDispatcher dispatcher = new(RegistryWithOneTool(), new McpSessionOptions(
            ExampleServer, "Optional instructions for the client"));

        string response = await AnswerAsync(dispatcher, LifecycleInitializeRequest);

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2024-11-05","capabilities":{"tools":{"listChanged":false}},"serverInfo":{"name":"ExampleServer","version":"1.0.0"},"instructions":"Optional instructions for the client"}}""",
            response);
        Assert.AreEqual("2024-11-05", dispatcher.NegotiatedProtocolVersion);
    }

    [TestMethod]
    public async Task AnswerAsync_InitializeLatestWithNoCapabilitiesOrInstructions_WritesTheTitleAndEmptyCapabilities()
    {
        McpRequestDispatcher dispatcher = new(new McpServerRegistry(), new McpSessionOptions(ExampleServer));

        string response = await AnswerAsync(dispatcher, InitializeLatest);

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{},"serverInfo":{"name":"ExampleServer","title":"Example Server Display Name","version":"1.0.0"}}}""",
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_InitializeLatestWithoutATitle_WritesNoTitle()
    {
        McpRequestDispatcher dispatcher = new(RegistryWithOneTool(), new McpSessionOptions(new McpServerInfo("s", null, "2")));

        string response = await AnswerAsync(dispatcher, InitializeLatest);

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{"tools":{"listChanged":false}},"serverInfo":{"name":"s","version":"2"}}}""",
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_InitializeWithAnUnsupportedProtocolVersion_AnswersTheLatestVersionNotAnError()
    {
        McpRequestDispatcher dispatcher = NewDispatcher();

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":"a","method":"initialize","params":{"protocolVersion":"1999-01-01","capabilities":{}}}""");

        StringAssert.StartsWith(response, """{"jsonrpc":"2.0","id":"a","result":{"protocolVersion":"2025-06-18",""");
        Assert.AreEqual("2025-06-18", dispatcher.NegotiatedProtocolVersion);
    }

    [TestMethod]
    [DataRow("""{"jsonrpc":"2.0","id":2,"method":"initialize"}""", "protocolVersion")]
    [DataRow("""{"jsonrpc":"2.0","id":2,"method":"initialize","params":[]}""", "protocolVersion")]
    [DataRow("""{"jsonrpc":"2.0","id":2,"method":"initialize","params":{"capabilities":{}}}""", "protocolVersion")]
    [DataRow("""{"jsonrpc":"2.0","id":2,"method":"initialize","params":{"protocolVersion":7,"capabilities":{}}}""", "protocolVersion")]
    [DataRow("""{"jsonrpc":"2.0","id":2,"method":"initialize","params":{"protocolVersion":"2025-06-18"}}""", "capabilities")]
    [DataRow("""{"jsonrpc":"2.0","id":2,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":[]}}""", "capabilities")]
    public async Task AnswerAsync_InitializeWithBadParams_AnswersInvalidParamsAndStaysUninitialized(string request, string what)
    {
        McpRequestDispatcher dispatcher = NewDispatcher();

        string response = await AnswerAsync(dispatcher, request);

        Assert.AreEqual($$$"""{"jsonrpc":"2.0","id":2,"error":{"code":-32602,"message":"Invalid params: {{{what}}}"}}""", response);
        Assert.IsNull(dispatcher.NegotiatedProtocolVersion);
    }

    [TestMethod]
    public async Task AnswerAsync_SecondInitialize_IsRefusedAsAlreadyInitialized()
    {
        McpRequestDispatcher dispatcher = NewDispatcher();
        await AnswerAsync(dispatcher, InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":3,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{}}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":3,"error":{"code":-32600,"message":"Invalid Request: the session is already initialized"}}""", response);
        Assert.AreEqual("2025-06-18", dispatcher.NegotiatedProtocolVersion);
    }

    [TestMethod]
    public async Task AnswerAsync_RequestBeforeInitialize_IsRefusedAsNotInitialized()
    {
        McpRequestDispatcher dispatcher = NewDispatcher();

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":4,"method":"tools/list"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":4,"error":{"code":-32600,"message":"Invalid Request: the session is not initialized"}}""", response);
    }

    [TestMethod]
    public async Task AnswerAsync_PingBeforeAndAfterInitialize_IsAnsweredWithAnEmptyObject()
    {
        McpRequestDispatcher dispatcher = NewDispatcher();
        const string ping = """{"jsonrpc":"2.0","id":"123","method":"ping"}""";
        const string expected = """{"jsonrpc":"2.0","id":"123","result":{}}""";

        Assert.AreEqual(expected, await AnswerAsync(dispatcher, ping));
        await AnswerAsync(dispatcher, InitializeLatest);
        Assert.AreEqual(expected, await AnswerAsync(dispatcher, ping));
    }

    [TestMethod]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/initialized"}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/unknown","params":{}}""")]
    [DataRow("""{"jsonrpc":"2.0","id":9,"result":{}}""")]
    [DataRow("""{"jsonrpc":"2.0","id":9,"error":{"code":-1,"message":"x"}}""")]
    public async Task AnswerAsync_NotificationOrClientResponse_GetsNoResponse(string message)
    {
        McpRequestDispatcher dispatcher = NewDispatcher();

        Assert.IsNull(await dispatcher.AnswerAsync(Encoding.UTF8.GetBytes(message), CancellationToken.None));
        Assert.IsNull(dispatcher.NegotiatedProtocolVersion);
    }

    [TestMethod]
    public async Task AnswerAsync_RequestsAfterInitializeWithoutInitializedNotification_AreAccepted()
    {
        McpRequestDispatcher dispatcher = NewDispatcher();
        await AnswerAsync(dispatcher, InitializeLatest);

        string response = await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":5,"method":"no/such/method"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":5,"error":{"code":-32601,"message":"Method not found"}}""", response);
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("not json")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"method":"ping",}""")]
    public async Task AnswerAsync_MalformedJson_AnswersParseErrorWithANullId(string message)
    {
        Assert.AreEqual("""{"jsonrpc":"2.0","id":null,"error":{"code":-32700,"message":"Parse error"}}""", await AnswerAsync(NewDispatcher(), message));
    }

    [TestMethod]
    public async Task AnswerAsync_RequestWithANullId_AnswersInvalidRequestWithANullId()
    {
        string response = await AnswerAsync(NewDispatcher(), """{"jsonrpc":"2.0","id":null,"method":"ping"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":null,"error":{"code":-32600,"message":"Invalid Request"}}""", response);
    }

    [TestMethod]
    public async Task AnswerAsync_InvalidRequestWithAReadableId_EchoesTheId()
    {
        string response = await AnswerAsync(NewDispatcher(), """{"jsonrpc":"1.0","id":12345678901234567890123,"method":"ping"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":12345678901234567890123,"error":{"code":-32600,"message":"Invalid Request"}}""", response);
    }

    [TestMethod]
    public async Task AnswerAsync_MessageOverTheLimit_IsRefusedAndTheSessionContinues()
    {
        McpRequestDispatcher dispatcher = new(new McpServerRegistry(), new McpSessionOptions(ExampleServer, maximumMessageBytes: 50));
        string longPing = """{"jsonrpc":"2.0","id":1,"method":"ping","params":{"padding":"xxxxxxxxxxxxxxxx"}}""";

        string refused = await AnswerAsync(dispatcher, longPing);

        Assert.AreEqual("""{"jsonrpc":"2.0","id":null,"error":{"code":-32600,"message":"Invalid Request: the message is longer than 50 bytes"}}""", refused);
        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"result":{}}""", await AnswerAsync(dispatcher, """{"jsonrpc":"2.0","id":1,"method":"ping"}"""));
    }

    [TestMethod]
    public async Task AnswerAsync_MessageExactlyAtTheLimit_IsAnswered()
    {
        string ping = """{"jsonrpc":"2.0","id":1,"method":"ping"}""";
        McpRequestDispatcher dispatcher = new(new McpServerRegistry(), new McpSessionOptions(ExampleServer, maximumMessageBytes: ping.Length));

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"result":{}}""", await AnswerAsync(dispatcher, ping));
    }

    [TestMethod]
    public void AnswerMessageTooLong_DefaultLimit_NamesFourMebibytes()
    {
        string response = Encoding.UTF8.GetString(NewDispatcher().AnswerMessageTooLong());

        Assert.AreEqual("""{"jsonrpc":"2.0","id":null,"error":{"code":-32600,"message":"Invalid Request: the message is longer than 4194304 bytes"}}""", response);
    }

    [TestMethod]
    public async Task AnswerAsync_ConcurrentInitializes_ExactlyOneSucceeds()
    {
        McpRequestDispatcher dispatcher = NewDispatcher();

        string[] responses = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => AnswerAsync(dispatcher, InitializeLatest))));

        Assert.AreEqual(1, responses.Count(response => response.Contains("\"result\"", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Constructor_NullRegistryOrOptions_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpRequestDispatcher(null!, new McpSessionOptions(ExampleServer)));
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpRequestDispatcher(new McpServerRegistry(), null!));
    }

    private static McpRequestDispatcher NewDispatcher() =>
        new(RegistryWithOneTool(), new McpSessionOptions(ExampleServer));

    private static McpServerRegistry RegistryWithOneTool()
    {
        McpServerRegistry registry = new();
        registry.AddTool(new McpTool("echo", null, "Echoes.", """{"type":"object"}""", (_, _) => Task.FromResult(McpToolResult.FromText("echo"))));
        return registry;
    }

    private static async Task<string> AnswerAsync(McpRequestDispatcher dispatcher, string message) =>
        Encoding.UTF8.GetString((await dispatcher.AnswerAsync(Encoding.UTF8.GetBytes(message), CancellationToken.None))!);
}
