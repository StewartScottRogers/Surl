using System.Text;
using System.Text.Json;

namespace Surl.McpServing;

[TestClass]
public sealed class McpStreamSessionTests
{
    private const string Initialize =
        """{"jsonrpc":"2.0","id":0,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{}}}""";

    private const string Initialized = """{"jsonrpc":"2.0","method":"notifications/initialized"}""";

    private const string WeatherText =
        "Current weather in New York:\nTemperature: 72°F\nConditions: Partly cloudy";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [TestMethod]
    public async Task RunAsync_InitializeToolsListAndToolsCall_WritesEachResponseOnExactlyOneLine()
    {
        MemoryStream input = Input(
            Initialize,
            Initialized,
            """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""",
            """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_weather","arguments":{"location":"New York"}}}""");
        UnitTestLineOutputStream output = new();

        await RunAsync(input, output, WeatherRegistry());

        Assert.HasCount(3, output.Writes);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, output.Writes.Select(write => ReadLine(write).GetProperty("id").GetInt32()).ToArray());
        Assert.AreEqual("2025-06-18", ReadLine(output.Writes[0]).GetProperty("result").GetProperty("protocolVersion").GetString());
        Assert.AreEqual("get_weather", ReadLine(output.Writes[1]).GetProperty("result").GetProperty("tools")[0].GetProperty("name").GetString());
        Assert.AreEqual(WeatherText, ReadLine(output.Writes[2]).GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task RunAsync_ToolTextContainsANewline_WritesItEscapedInsideOneLine()
    {
        MemoryStream input = Input(
            Initialize,
            """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_weather","arguments":{"location":"New York"}}}""");
        UnitTestLineOutputStream output = new();

        await RunAsync(input, output, WeatherRegistry());

        StringAssert.Contains(output.Writes[1], """Current weather in New York:\nTemperature""");
        Assert.AreEqual(1, output.Writes[1].Count(character => character == '\n'));
    }

    [TestMethod]
    public async Task RunAsync_NotificationsAndClientResponses_WritesNothing()
    {
        MemoryStream input = Input(
            Initialized,
            """{"jsonrpc":"2.0","method":"notifications/unknown"}""",
            """{"jsonrpc":"2.0","id":9,"result":{}}""");
        UnitTestLineOutputStream output = new();

        await RunAsync(input, output, WeatherRegistry());

        Assert.IsEmpty(output.Writes);
    }

    [TestMethod]
    public async Task RunAsync_InputEnds_ReturnsAfterAnsweringWhatWasRead()
    {
        MemoryStream input = Input("""{"jsonrpc":"2.0","id":1,"method":"ping"}""");
        UnitTestLineOutputStream output = new();

        await RunAsync(input, output, WeatherRegistry());

        CollectionAssert.AreEqual(new[] { """{"jsonrpc":"2.0","id":1,"result":{}}""" + "\n" }, output.Writes.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_CarriageReturnsEmptyLinesAndAnUnterminatedLastLine_AnswersEachMessage()
    {
        MemoryStream input = new(Encoding.UTF8.GetBytes(
            "\n\r\n{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}\r\n\n{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"ping\"}"));
        UnitTestLineOutputStream output = new();

        await RunAsync(input, output, WeatherRegistry());

        CollectionAssert.AreEqual(
            new[] { """{"jsonrpc":"2.0","id":1,"result":{}}""" + "\n", """{"jsonrpc":"2.0","id":2,"result":{}}""" + "\n" },
            output.Writes.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_LineOverTheLimit_AnswersInvalidRequestWithANullIdAndContinues()
    {
        MemoryStream input = Input(
            """{"jsonrpc":"2.0","id":1,"method":"ping","params":{"padding":"0123456789012345678901234567890123456789"}}""",
            """{"jsonrpc":"2.0","id":2,"method":"ping"}""");
        UnitTestLineOutputStream output = new();

        await RunAsync(input, output, WeatherRegistry(), maximumMessageBytes: 64);

        CollectionAssert.AreEqual(
            new[]
            {
                """{"jsonrpc":"2.0","id":null,"error":{"code":-32600,"message":"Invalid Request: the message is longer than 64 bytes"}}""" + "\n",
                """{"jsonrpc":"2.0","id":2,"result":{}}""" + "\n",
            },
            output.Writes.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_LineThatIsNotJson_AnswersParseErrorAndContinues()
    {
        MemoryStream input = Input("not json", """{"jsonrpc":"2.0","id":2,"method":"ping"}""");
        UnitTestLineOutputStream output = new();

        await RunAsync(input, output, WeatherRegistry());

        Assert.AreEqual(-32700, ReadLine(output.Writes[0]).GetProperty("error").GetProperty("code").GetInt32());
        Assert.AreEqual(2, ReadLine(output.Writes[1]).GetProperty("id").GetInt32());
    }

    [TestMethod]
    [DataRow("5", "5")]
    [DataRow("\"call-5\"", "\"call-5\"")]
    public async Task RunAsync_CancelledNotificationForARunningCall_CancelsThatHandlersTokenAndWritesNoResponse(string id, string requestId)
    {
        BlockingTool tool = new();
        UnitTestChannelInputStream input = new();
        UnitTestLineOutputStream output = new();
        Task session = RunAsync(input, output, tool.Registry());
        input.WriteLine(Initialize);
        await output.NextWriteAsync();
        input.WriteLine($$$"""{"jsonrpc":"2.0","id":{{{id}}},"method":"tools/call","params":{"name":"block"}}""");
        await tool.Started.Task.WaitAsync(Patience);

        input.WriteLine($$$"""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":{{{requestId}}},"reason":"User requested cancellation"}}""");
        await tool.Cancelled.Task.WaitAsync(Patience);
        tool.Release.SetResult();
        input.WriteLine("""{"jsonrpc":"2.0","id":6,"method":"ping"}""");
        string next = await output.NextWriteAsync();
        input.Complete();
        await session.WaitAsync(Patience);

        Assert.AreEqual("""{"jsonrpc":"2.0","id":6,"result":{}}""" + "\n", next);
        Assert.HasCount(2, output.Writes);
    }

    [TestMethod]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/cancelled"}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{}}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":true}}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":"5"}}""")]
    [DataRow("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":99}}""")]
    public async Task RunAsync_CancelledNotificationNamingNoRunningRequest_LeavesTheRunningCallToAnswer(string notification)
    {
        BlockingTool tool = new();
        UnitTestChannelInputStream input = new();
        UnitTestLineOutputStream output = new();
        Task session = RunAsync(input, output, tool.Registry());
        input.WriteLine(Initialize);
        await output.NextWriteAsync();
        input.WriteLine("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"block"}}""");
        await tool.Started.Task.WaitAsync(Patience);

        input.WriteLine(notification);
        input.WriteLine("""{"jsonrpc":"2.0","id":6,"method":"ping"}""");
        string ping = await output.NextWriteAsync();
        tool.Release.SetResult();
        string call = await output.NextWriteAsync();
        input.Complete();
        await session.WaitAsync(Patience);

        Assert.AreEqual("""{"jsonrpc":"2.0","id":6,"result":{}}""" + "\n", ping);
        Assert.AreEqual(5, ReadLine(call).GetProperty("id").GetInt32());
        Assert.IsFalse(tool.Cancelled.Task.IsCompleted);
    }

    [TestMethod]
    public async Task RunAsync_InputEndsWhileACallRuns_CancelsItsHandlerAndWritesNoResponse()
    {
        BlockingTool tool = new();
        UnitTestChannelInputStream input = new();
        UnitTestLineOutputStream output = new();
        Task session = RunAsync(input, output, tool.Registry());
        input.WriteLine(Initialize);
        input.WriteLine("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"block"}}""");
        await tool.Started.Task.WaitAsync(Patience);

        input.Complete();
        await tool.Cancelled.Task.WaitAsync(Patience);
        tool.Release.SetResult();
        await session.WaitAsync(Patience);

        Assert.HasCount(1, output.Writes);
    }

    [TestMethod]
    public async Task RunAsync_CallerCancelsWhileWaitingForInput_ReturnsWithoutThrowing()
    {
        UnitTestChannelInputStream input = new();
        UnitTestLineOutputStream output = new();
        using CancellationTokenSource cancellation = new();
        Task session = RunAsync(input, output, WeatherRegistry(), cancellationToken: cancellation.Token);
        input.WriteLine("""{"jsonrpc":"2.0","id":1,"method":"ping"}""");
        await output.NextWriteAsync();

        await cancellation.CancelAsync();
        await session.WaitAsync(Patience);

        Assert.HasCount(1, output.Writes);
    }

    [TestMethod]
    public async Task RunAsync_SixteenCallsRun_ReadsTheNextMessageOnlyOnceOneFinishes()
    {
        BlockingTool tool = new();
        UnitTestChannelInputStream input = new();
        UnitTestLineOutputStream output = new();
        Task session = RunAsync(input, output, tool.Registry());
        input.WriteLine(Initialize);
        await output.NextWriteAsync();
        for (int id = 1; id <= McpStreamSession.MaximumConcurrentRequests; id++)
        {
            input.WriteLine($$$"""{"jsonrpc":"2.0","id":{{{id}}},"method":"tools/call","params":{"name":"block"}}""");
        }

        await tool.WaitForStartsAsync(McpStreamSession.MaximumConcurrentRequests);
        input.WriteLine("""{"jsonrpc":"2.0","id":100,"method":"ping"}""");

        tool.Release.SetResult();
        string first = await output.NextWriteAsync();
        for (int write = 1; write <= McpStreamSession.MaximumConcurrentRequests; write++)
        {
            await output.NextWriteAsync();
        }

        input.Complete();
        await session.WaitAsync(Patience);

        StringAssert.Contains(first, "\"result\":{\"content\"");
        Assert.HasCount(McpStreamSession.MaximumConcurrentRequests + 2, output.Writes);
        Assert.Contains("""{"jsonrpc":"2.0","id":100,"result":{}}""" + "\n", output.Writes);
    }

    [TestMethod]
    public async Task RunAsync_CallerCancelsWhileSixteenCallsRun_CancelsEachHandlerAndReturns()
    {
        BlockingTool tool = new();
        UnitTestChannelInputStream input = new();
        UnitTestLineOutputStream output = new();
        using CancellationTokenSource cancellation = new();
        Task session = RunAsync(input, output, tool.Registry(), cancellationToken: cancellation.Token);
        input.WriteLine(Initialize);
        for (int id = 1; id <= McpStreamSession.MaximumConcurrentRequests; id++)
        {
            input.WriteLine($$$"""{"jsonrpc":"2.0","id":{{{id}}},"method":"tools/call","params":{"name":"block"}}""");
        }

        await tool.WaitForStartsAsync(McpStreamSession.MaximumConcurrentRequests);

        await cancellation.CancelAsync();
        await tool.Cancelled.Task.WaitAsync(Patience);
        tool.Release.SetResult();
        await session.WaitAsync(Patience);

        Assert.HasCount(1, output.Writes);
    }

    [TestMethod]
    public async Task RunAsync_OutputFailsForARunningCall_ThrowsTheFailureOnceTheSessionEnds()
    {
        BlockingTool tool = new();
        UnitTestChannelInputStream input = new();
        UnitTestLineOutputStream output = new(writesBeforeFailure: 1);
        Task session = RunAsync(input, output, tool.Registry());
        input.WriteLine(Initialize);
        input.WriteLine("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"block"}}""");
        await tool.Started.Task.WaitAsync(Patience);

        tool.Release.SetResult();
        await output.WriteFailed.Task.WaitAsync(Patience);
        input.Complete();

        await Assert.ThrowsExactlyAsync<IOException>(() => session.WaitAsync(Patience));
    }

    [TestMethod]
    public async Task RunAsync_InputThrowsCancellationOfItsOwn_ThrowsIt()
    {
        UnitTestLineOutputStream output = new();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => RunAsync(new UnitTestCancellingInputStream(), output, WeatherRegistry()));
    }

    [TestMethod]
    public async Task RunAsync_NullStreams_Throws()
    {
        McpSessionOptions options = new(new McpServerInfo("s", null, "1"));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => McpStreamSession.RunAsync(null!, new MemoryStream(), new McpServerRegistry(), options, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => McpStreamSession.RunAsync(new MemoryStream(), null!, new McpServerRegistry(), options, CancellationToken.None));
    }

    private static Task RunAsync(
        Stream input, Stream output, McpServerRegistry registry, int maximumMessageBytes = McpSessionOptions.DefaultMaximumMessageBytes, CancellationToken cancellationToken = default) =>
        McpStreamSession.RunAsync(
            input, output, registry, new McpSessionOptions(new McpServerInfo("s", null, "1"), maximumMessageBytes: maximumMessageBytes), cancellationToken);

    private static MemoryStream Input(params string[] lines) =>
        new(Encoding.UTF8.GetBytes(string.Concat(lines.Select(line => line + "\n"))));

    // Asserts the write is exactly one line - one newline, at its end - and returns it as JSON-RPC 2.0.
    private static JsonElement ReadLine(string write)
    {
        Assert.EndsWith("\n", write);
        Assert.AreEqual(1, write.Count(character => character == '\n'));
        JsonElement message = JsonDocument.Parse(write).RootElement.Clone();
        Assert.AreEqual("2.0", message.GetProperty("jsonrpc").GetString());
        return message;
    }

    // The "Tools" page's get_weather tool (MCP 2025-06-18, server/tools).
    private static McpServerRegistry WeatherRegistry()
    {
        McpServerRegistry registry = new();
        registry.AddTool(new McpTool(
            "get_weather",
            "Weather Information Provider",
            "Get current weather information for a location",
            """{"type":"object","properties":{"location":{"type":"string"}},"required":["location"]}""",
            (_, _) => Task.FromResult(McpToolResult.FromText(WeatherText))));
        return registry;
    }

    // A tool whose handler reports it started, reports its token cancelled, and returns only once released,
    // whatever its token says - so a test sees that a cancelled call's response is not written.
    private sealed class BlockingTool
    {
        private readonly SemaphoreSlim _starts = new(0);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public McpServerRegistry Registry()
        {
            McpServerRegistry registry = new();
            registry.AddTool(new McpTool("block", null, "Blocks until released.", """{"type":"object"}""", HandleAsync));
            return registry;
        }

        public async Task WaitForStartsAsync(int count)
        {
            for (int start = 0; start < count; start++)
            {
                await _starts.WaitAsync(Patience);
            }
        }

        private async Task<McpToolResult> HandleAsync(JsonElement arguments, CancellationToken cancellationToken)
        {
            using CancellationTokenRegistration registration = cancellationToken.Register(() => Cancelled.TrySetResult());
            Started.TrySetResult();
            _starts.Release();
            await Release.Task;
            return McpToolResult.FromText("released");
        }
    }

    private sealed class UnitTestCancellingInputStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException();
    }
}
