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
    public void Answer_LifecycleInitializeExample_AnswersThePagesShapeWithTheCallersServerInfoAndCapabilities()
    {
        McpRequestDispatcher dispatcher = new(new McpSessionOptions(
            ExampleServer, new McpServerCapabilities(Tools: true, Resources: true), "Optional instructions for the client"));

        string response = Answer(dispatcher, LifecycleInitializeRequest);

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2024-11-05","capabilities":{"resources":{"subscribe":false,"listChanged":false},"tools":{"listChanged":false}},"serverInfo":{"name":"ExampleServer","version":"1.0.0"},"instructions":"Optional instructions for the client"}}""",
            response);
        Assert.AreEqual("2024-11-05", dispatcher.NegotiatedProtocolVersion);
    }

    [TestMethod]
    public void Answer_InitializeLatestWithNoCapabilitiesOrInstructions_WritesTheTitleAndEmptyCapabilities()
    {
        McpRequestDispatcher dispatcher = new(new McpSessionOptions(ExampleServer, new McpServerCapabilities(false, false)));

        string response = Answer(dispatcher, InitializeLatest);

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{},"serverInfo":{"name":"ExampleServer","title":"Example Server Display Name","version":"1.0.0"}}}""",
            response);
    }

    [TestMethod]
    public void Answer_InitializeLatestWithoutATitle_WritesNoTitle()
    {
        McpRequestDispatcher dispatcher = new(new McpSessionOptions(new McpServerInfo("s", null, "2"), new McpServerCapabilities(true, false)));

        string response = Answer(dispatcher, InitializeLatest);

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{"tools":{"listChanged":false}},"serverInfo":{"name":"s","version":"2"}}}""",
            response);
    }

    [TestMethod]
    public void Answer_InitializeWithAnUnsupportedProtocolVersion_AnswersTheLatestVersionNotAnError()
    {
        McpRequestDispatcher dispatcher = NewDispatcher();

        string response = Answer(dispatcher, """{"jsonrpc":"2.0","id":"a","method":"initialize","params":{"protocolVersion":"1999-01-01","capabilities":{}}}""");

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
    public void Answer_InitializeWithBadParams_AnswersInvalidParamsAndStaysUninitialized(string request, string what)
    {
        McpRequestDispatcher dispatcher = NewDispatcher();

        string response = Answer(dispatcher, request);

        Assert.AreEqual($$$"""{"jsonrpc":"2.0","id":2,"error":{"code":-32602,"message":"Invalid params: {{{what}}}"}}""", response);
        Assert.IsNull(dispatcher.NegotiatedProtocolVersion);
    }

    [TestMethod]
    public void Answer_SecondInitialize_IsRefusedAsAlreadyInitialized()
    {
        McpRequestDispatcher dispatcher = NewDispatcher();
        Answer(dispatcher, InitializeLatest);

        string response = Answer(dispatcher, """{"jsonrpc":"2.0","id":3,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{}}}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":3,"error":{"code":-32600,"message":"Invalid Request: the session is already initialized"}}""", response);
        Assert.AreEqual("2025-06-18", dispatcher.NegotiatedProtocolVersion);
    }

    [TestMethod]
    public void Answer_RequestBeforeInitialize_IsRefusedAsNotInitialized()
    {
        McpRequestDispatcher dispatcher = NewDispatcher();

        string response = Answer(dispatcher, """{"jsonrpc":"2.0","id":4,"method":"tools/list"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":4,"error":{"code":-32600,"message":"Invalid Request: the session is not initialized"}}""", response);
    }

    [TestMethod]
    public void Answer_PingBeforeAndAfterInitialize_IsAnsweredWithAnEmptyObject()
    {
        McpRequestDispatcher dispatcher = NewDispatcher();
        const string ping = """{"jsonrpc":"2.0","id":"123","method":"ping"}""";
        const string expected = """{"jsonrpc":"2.0","id":"123","result":{}}""";

        Assert.AreEqual(expected, Answer(dispatcher, ping));
        Answer(dispatcher, InitializeLatest);
        Assert.AreEqual(expected, Answer(dispatcher, ping));
    }

    [TestMethod]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/initialized"}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/unknown","params":{}}""")]
    [DataRow("""{"jsonrpc":"2.0","id":9,"result":{}}""")]
    [DataRow("""{"jsonrpc":"2.0","id":9,"error":{"code":-1,"message":"x"}}""")]
    public void Answer_NotificationOrClientResponse_GetsNoResponse(string message)
    {
        McpRequestDispatcher dispatcher = NewDispatcher();

        Assert.IsNull(dispatcher.Answer(Encoding.UTF8.GetBytes(message)));
        Assert.IsNull(dispatcher.NegotiatedProtocolVersion);
    }

    [TestMethod]
    public void Answer_RequestsAfterInitializeWithoutInitializedNotification_AreAccepted()
    {
        McpRequestDispatcher dispatcher = NewDispatcher();
        Answer(dispatcher, InitializeLatest);

        string response = Answer(dispatcher, """{"jsonrpc":"2.0","id":5,"method":"no/such/method"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":5,"error":{"code":-32601,"message":"Method not found"}}""", response);
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("not json")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"method":"ping",}""")]
    public void Answer_MalformedJson_AnswersParseErrorWithANullId(string message)
    {
        Assert.AreEqual("""{"jsonrpc":"2.0","id":null,"error":{"code":-32700,"message":"Parse error"}}""", Answer(NewDispatcher(), message));
    }

    [TestMethod]
    public void Answer_RequestWithANullId_AnswersInvalidRequestWithANullId()
    {
        string response = Answer(NewDispatcher(), """{"jsonrpc":"2.0","id":null,"method":"ping"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":null,"error":{"code":-32600,"message":"Invalid Request"}}""", response);
    }

    [TestMethod]
    public void Answer_InvalidRequestWithAReadableId_EchoesTheId()
    {
        string response = Answer(NewDispatcher(), """{"jsonrpc":"1.0","id":12345678901234567890123,"method":"ping"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":12345678901234567890123,"error":{"code":-32600,"message":"Invalid Request"}}""", response);
    }

    [TestMethod]
    public void Answer_MessageOverTheLimit_IsRefusedAndTheSessionContinues()
    {
        McpRequestDispatcher dispatcher = new(new McpSessionOptions(ExampleServer, new McpServerCapabilities(false, false), maximumMessageBytes: 50));
        string longPing = """{"jsonrpc":"2.0","id":1,"method":"ping","params":{"padding":"xxxxxxxxxxxxxxxx"}}""";

        string refused = Answer(dispatcher, longPing);

        Assert.AreEqual("""{"jsonrpc":"2.0","id":null,"error":{"code":-32600,"message":"Invalid Request: the message is longer than 50 bytes"}}""", refused);
        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"result":{}}""", Answer(dispatcher, """{"jsonrpc":"2.0","id":1,"method":"ping"}"""));
    }

    [TestMethod]
    public void Answer_MessageExactlyAtTheLimit_IsAnswered()
    {
        string ping = """{"jsonrpc":"2.0","id":1,"method":"ping"}""";
        McpRequestDispatcher dispatcher = new(new McpSessionOptions(ExampleServer, new McpServerCapabilities(false, false), maximumMessageBytes: ping.Length));

        Assert.AreEqual("""{"jsonrpc":"2.0","id":1,"result":{}}""", Answer(dispatcher, ping));
    }

    [TestMethod]
    public void AnswerMessageTooLong_DefaultLimit_NamesFourMebibytes()
    {
        string response = Encoding.UTF8.GetString(NewDispatcher().AnswerMessageTooLong());

        Assert.AreEqual("""{"jsonrpc":"2.0","id":null,"error":{"code":-32600,"message":"Invalid Request: the message is longer than 4194304 bytes"}}""", response);
    }

    [TestMethod]
    public void Answer_ConcurrentInitializes_ExactlyOneSucceeds()
    {
        McpRequestDispatcher dispatcher = NewDispatcher();

        string[] responses = new string[16];
        Parallel.For(0, responses.Length, i => responses[i] = Answer(dispatcher, InitializeLatest));

        Assert.AreEqual(1, responses.Count(response => response.Contains("\"result\"", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Constructor_NullOptions_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpRequestDispatcher(null!));
    }

    private static McpRequestDispatcher NewDispatcher() =>
        new(new McpSessionOptions(ExampleServer, new McpServerCapabilities(Tools: true, Resources: false)));

    private static string Answer(McpRequestDispatcher dispatcher, string message) =>
        Encoding.UTF8.GetString(dispatcher.Answer(Encoding.UTF8.GetBytes(message))!);
}
