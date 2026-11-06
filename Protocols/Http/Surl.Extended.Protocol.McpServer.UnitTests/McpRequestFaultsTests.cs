using System.Text.Json;
using Surl.McpServing;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.McpServer.McpHttpHarness;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// The chosen-error fault (ADR-0124, decisions 4 and 9; ADR-0144; BL-835): a request in a session that an
/// <see cref="FaultKind.Error" /> rule matches is answered with the rule's JSON-RPC error, in the media type the
/// request is answered in, in place of the core, and one <see cref="FaultAppliedEvent" /> is published. Messages
/// written by hand from the MCP specification and JSON-RPC 2.0 section 5.1.
/// </summary>
[TestClass]
public sealed class McpRequestFaultsTests
{
    private const string EchoCall = """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"echo","arguments":{"text":"hi"}}}""";

    private static FaultRule ErrorRule(string? command = "echo", string? reply = "-32000 try later") =>
        new("f1", FaultKind.Error) { Scheme = "mcp", Command = command, Reply = reply };

    private static async Task<(McpHttpHarness Harness, ScriptedFaultPlan Faults, string Response)> CallAsync(FaultRule rule, string message, string login = BearerField)
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync(login);
        ScriptedFaultPlan faults = new(rule);
        harness.Faults = faults;

        string response = await harness.SendAsync("POST", "/mcp", message, login, AcceptField, JsonField, $"Mcp-Session-Id: {sessionId}");
        return (harness, faults, response);
    }

    [TestMethod]
    public async Task ServeAsync_ToolCallUnderARule_GetsTheChosenErrorOnTheEventStream()
    {
        (McpHttpHarness harness, ScriptedFaultPlan faults, string response) = await CallAsync(ErrorRule(), EchoCall, $"Authorization: Bearer {UnitTestBearerAuthenticationPolicy.AliceToken}");

        Assert.AreEqual(
            BodyAnswer("200 OK", "text/event-stream", "data: {\"jsonrpc\":\"2.0\",\"id\":2,\"error\":{\"code\":-32000,\"message\":\"try later\"}}\n\n"),
            response);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/mcp"), harness.Events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "mcp://127.0.0.1:8080/", Scheme = "mcp", Account = "alice", Path = "/mcp", Command = "echo" },
            faults.Points.Single());
        Assert.Contains("The request was answered with JSON-RPC error -32000 by fault rule f1.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_MethodUnderARule_GetsTheChosenErrorAsJson()
    {
        (McpHttpHarness harness, _, string response) = await CallAsync(ErrorRule(command: "ping", reply: "7 nope"), """{"jsonrpc":"2.0","id":"a","method":"ping"}""");

        Assert.AreEqual(BodyAnswer("200 OK", "application/json", "{\"jsonrpc\":\"2.0\",\"id\":\"a\",\"error\":{\"code\":7,\"message\":\"nope\"}}"), response);
        Assert.HasCount(1, harness.Events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_RequestTheRuleDoesNotName_IsServed()
    {
        (McpHttpHarness harness, ScriptedFaultPlan faults, string response) = await CallAsync(ErrorRule(command: "read_file"), EchoCall);

        Assert.AreEqual(
            BodyAnswer("200 OK", "text/event-stream", "data: {\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"hi\"}],\"isError\":false}}\n\n"),
            response);
        Assert.IsEmpty(harness.Events.Events);
        Assert.IsNull(faults.Points.Single().Account);
    }

    [TestMethod]
    public async Task ServeAsync_ReplyThatDoesNotFit_IsNotAppliedAndIsNoted()
    {
        (McpHttpHarness harness, _, string response) = await CallAsync(ErrorRule(reply: "busy"), EchoCall);

        StringAssert.Contains(response, "\"result\"");
        Assert.IsEmpty(harness.Events.Events);
        Assert.Contains("Fault rule f1 not applied to the request: its reply does not fit the mcp error shape.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_CommandStageDelayRule_IsNotAppliedAndIsNoted()
    {
        FaultRule delay = new("f1", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 5 };

        (McpHttpHarness harness, _, string response) = await CallAsync(delay, EchoCall);

        StringAssert.Contains(response, "\"result\"");
        Assert.IsEmpty(harness.Events.Events);
        Assert.Contains("Fault rule f1 not applied to the request: a Delay rule is not applied at a request.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_Notification_AsksNoFaultPoint()
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync();
        ScriptedFaultPlan faults = new(ErrorRule(command: null));
        harness.Faults = faults;

        string response = await harness.PostAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""", $"Mcp-Session-Id: {sessionId}");

        Assert.AreEqual(EmptyAnswer("202 Accepted"), response);
        Assert.IsEmpty(faults.Points);
    }

    [TestMethod]
    [DataRow("""{"name":"echo"}""", "echo")]
    [DataRow("""{"name":7}""", "tools/call")]
    [DataRow("""{"arguments":{}}""", "tools/call")]
    [DataRow("""[]""", "tools/call")]
    [DataRow(null, "tools/call")]
    public void CommandOf_ToolsCall_IsTheToolsNameWhenItHasOne(string? parameters, string expected)
    {
        string message = $$"""{"jsonrpc":"2.0","id":1,"method":"tools/call"{{(parameters is null ? string.Empty : ",\"params\":" + parameters)}}}""";
        JsonRpcRequest request = (JsonRpcRequest)JsonRpcMessageParser.Parse(System.Text.Encoding.UTF8.GetBytes(message));

        Assert.AreEqual(expected, McpRequestFaults.CommandOf(request));
    }

    [TestMethod]
    [DataRow("-32603 internal", -32603, "internal")]
    [DataRow("+1 a b", 1, "a b")]
    [DataRow("0 x", 0, "x")]
    public void TryParse_ReplyInTheShape_GivesTheError(string reply, int code, string message)
    {
        Assert.IsTrue(McpRequestFaults.TryParse(reply, out JsonRpcError? error));
        Assert.AreEqual(new JsonRpcError(code, message), error);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no reply")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("-32000", DisplayName = "no message")]
    [DataRow("-32000 ", DisplayName = "empty message")]
    [DataRow(" x", DisplayName = "no code")]
    [DataRow("1.5 x", DisplayName = "not whole")]
    [DataRow("99999999999 x", DisplayName = "past int")]
    public void TryParse_ReplyOutsideTheShape_DoesNotFit(string? reply) =>
        Assert.IsFalse(McpRequestFaults.TryParse(reply, out _));
}
