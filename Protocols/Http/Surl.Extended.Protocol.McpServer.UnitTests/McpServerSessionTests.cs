using System.Text;
using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpServerSessionTests
{
    // The "Lifecycle" page's initialize request (MCP 2025-06-18, basic/lifecycle), on one line.
    private const string LifecycleInitializeRequest =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{"roots":{"listChanged":true},"sampling":{},"elicitation":{}},"clientInfo":{"name":"ExampleClient","title":"Example Client Display Name","version":"1.0.0"}}}""";

    private const string Instructions =
        "This server lists, reads and, when uploads are allowed, writes the files Surl serves. Paths are '/'-separated and relative to the served directory.";

    // The core's JSON writer escapes each apostrophe as ', which a JSON reader reads back as the same text.
    private const string InstructionsAsWritten =
        "This server lists, reads and, when uploads are allowed, writes the files Surl serves. Paths are \\u0027/\\u0027-separated and relative to the served directory.";

    [TestMethod]
    public async Task AnswerAsync_LifecycleInitializeExample_AnswersThePagesShapeWithThisServersServerInfo()
    {
        McpServerSession session = NewSession();

        string response = await AnswerAsync(session, LifecycleInitializeRequest);

        Assert.AreEqual(
            $$$"""{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2024-11-05","capabilities":{},"serverInfo":{"name":"surl","version":"1.2.3"},"instructions":"{{{InstructionsAsWritten}}}"}}""",
            response);
        Assert.AreEqual("2024-11-05", session.NegotiatedProtocolVersion);
    }

    [TestMethod]
    public async Task AnswerAsync_InitializeLatest_WritesThisServersTitle()
    {
        McpServerSession session = NewSession();

        string response = await AnswerAsync(session, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{}}}""");

        StringAssert.Contains(response, "\"serverInfo\":{\"name\":\"surl\",\"title\":\"Surl MCP server\",\"version\":\"1.2.3\"}");
    }

    [TestMethod]
    public async Task AnswerAsync_InitializeWithAnUnsupportedProtocolVersion_AnswersTheLatestVersionNotAnError()
    {
        McpServerSession session = NewSession();

        string response = await AnswerAsync(session, """{"jsonrpc":"2.0","id":"a","method":"initialize","params":{"protocolVersion":"1999-01-01","capabilities":{}}}""");

        StringAssert.StartsWith(response, """{"jsonrpc":"2.0","id":"a","result":{"protocolVersion":"2025-06-18",""");
        Assert.AreEqual("2025-06-18", session.NegotiatedProtocolVersion);
    }

    [TestMethod]
    public async Task AnswerAsync_RequestBeforeInitialize_IsRefusedAsNotInitialized()
    {
        McpServerSession session = NewSession();

        string response = await AnswerAsync(session, """{"jsonrpc":"2.0","id":3,"method":"tools/list"}""");

        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":3,"error":{"code":-32600,"message":"Invalid Request: the session is not initialized"}}""",
            response);
        Assert.IsNull(session.NegotiatedProtocolVersion);
    }

    [TestMethod]
    public async Task AnswerAsync_Ping_AnswersAnEmptyObject()
    {
        McpServerSession session = NewSession();

        string response = await AnswerAsync(session, """{"jsonrpc":"2.0","id":"123","method":"ping"}""");

        Assert.AreEqual("""{"jsonrpc":"2.0","id":"123","result":{}}""", response);
    }

    [TestMethod]
    public async Task AnswerAsync_InitializedNotification_GetsNoResponse()
    {
        McpServerSession session = NewSession();
        await AnswerAsync(session, LifecycleInitializeRequest);

        byte[]? response = await session.AnswerAsync(
            Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""), CancellationToken.None);

        Assert.IsNull(response);
    }

    [TestMethod]
    public void CreateOptions_CarriesThisServersIdentityAndTheMessageLimit()
    {
        McpSessionOptions options = McpServerSession.CreateOptions("9.9", 1024);

        Assert.AreEqual(new McpServerInfo("surl", "Surl MCP server", "9.9"), options.ServerInfo);
        Assert.AreEqual(Instructions, options.Instructions);
        Assert.AreEqual(1024, options.MaximumMessageBytes);
    }

    private static McpServerSession NewSession() =>
        new(new McpServerRegistry(), McpServerSession.CreateOptions("1.2.3", McpSessionOptions.DefaultMaximumMessageBytes));

    private static async Task<string> AnswerAsync(McpServerSession session, string message)
    {
        byte[]? response = await session.AnswerAsync(Encoding.UTF8.GetBytes(message), CancellationToken.None);
        Assert.IsNotNull(response);
        return Encoding.UTF8.GetString(response);
    }
}
