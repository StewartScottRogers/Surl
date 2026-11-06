using System.Runtime.InteropServices;
using System.Text.Json;
using Surl.Api;
using Surl.Cli;
using Surl.McpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

[TestClass]
public sealed class ProgramTests
{
    private const string InitializeRequest =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""";

    private static readonly Func<string, string?> NoEnvironment = _ => null;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_Initialize_AnswersWithSurlMcpsServerInfoAndTheToolsCapability()
    {
        await using var host = new ProgramTestHost(TestContext.CancellationToken);

        var result = await host.CallAsync(InitializeRequest);

        var serverInfo = result.GetProperty("serverInfo");
        Assert.AreEqual(Program.ServerName, serverInfo.GetProperty("name").GetString());
        Assert.AreEqual("surl-mcp", serverInfo.GetProperty("name").GetString());
        Assert.AreEqual("Surl", serverInfo.GetProperty("title").GetString());
        Assert.AreEqual(SurlMcpHelpText.Version, serverInfo.GetProperty("version").GetString());
        Assert.AreEqual(JsonValueKind.Object, result.GetProperty("capabilities").GetProperty("tools").ValueKind);
        Assert.AreEqual(Program.Instructions, result.GetProperty("instructions").GetString());
    }

    [TestMethod]
    public async Task RunAsync_ToolsList_ListsTheLibrarysToolsInTheAdrsOrder()
    {
        await using var host = new ProgramTestHost(TestContext.CancellationToken);
        await host.CallAsync(InitializeRequest);

        var result = await host.CallAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");

        var names = result.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()).ToArray();
        CollectionAssert.AreEqual(new[] { StartServerTool.Name, ListServersTool.Name, StopServerTool.Name, GetExchangesTool.Name, GetEventsTool.Name, WaitForEventTool.Name }, names);
    }

    [TestMethod]
    public async Task RunAsync_ToolsList_IncludesGetExchangesWithAnObjectInputSchemaAndADescription()
    {
        await using var host = new ProgramTestHost(TestContext.CancellationToken);
        await host.CallAsync(InitializeRequest);

        var result = await host.CallAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");

        var tool = Assert.ContainsSingle(
            result.GetProperty("tools").EnumerateArray().Where(tool => tool.GetProperty("name").GetString() == "get_exchanges"));
        Assert.AreEqual(GetExchangesTool.Description, tool.GetProperty("description").GetString());
        var inputSchema = tool.GetProperty("inputSchema");
        Assert.AreEqual("object", inputSchema.GetProperty("type").GetString());
        CollectionAssert.AreEqual(
            new[] { "server_id", "after_exchange_id", "max_exchanges", "scheme", "listen_url" },
            inputSchema.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray());
    }

    [TestMethod]
    [DataRow("get_events", new[] { "server_id", "after_sequence", "kinds", "scheme", "listen_url", "account", "max_events" })]
    [DataRow("wait_for_event", new[] { "server_id", "after_sequence", "kinds", "scheme", "listen_url", "account", "timeout_ms" })]
    public async Task RunAsync_ToolsList_IncludesEachEventToolWithAnObjectInputSchemaAndADescription(string name, string[] properties)
    {
        await using var host = new ProgramTestHost(TestContext.CancellationToken);
        await host.CallAsync(InitializeRequest);

        var result = await host.CallAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");

        var tool = Assert.ContainsSingle(result.GetProperty("tools").EnumerateArray().Where(tool => tool.GetProperty("name").GetString() == name));
        Assert.AreEqual(name == GetEventsTool.Name ? GetEventsTool.Description : WaitForEventTool.Description, tool.GetProperty("description").GetString());
        var inputSchema = tool.GetProperty("inputSchema");
        Assert.AreEqual("object", inputSchema.GetProperty("type").GetString());
        Assert.IsFalse(inputSchema.GetProperty("additionalProperties").GetBoolean());
        CollectionAssert.AreEqual(new[] { "server_id" }, inputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray());
        CollectionAssert.AreEqual(properties, inputSchema.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_ResourcesList_ListsTheOverviewEveryAiHelpTopicAndAllInOrder()
    {
        await using var host = new ProgramTestHost(TestContext.CancellationToken);
        await host.CallAsync(InitializeRequest);

        var result = await host.CallAsync("""{"jsonrpc":"2.0","id":2,"method":"resources/list"}""");

        var resources = result.GetProperty("resources").EnumerateArray().ToArray();
        string[] expectedUris =
        [
            "surl://aihelp",
            .. AiHelpTopics.All.Select(topic => $"surl://aihelp/{topic.Name}"),
            "surl://aihelp/all",
        ];
        CollectionAssert.AreEqual(expectedUris, resources.Select(resource => resource.GetProperty("uri").GetString()).ToArray());
        Assert.IsFalse(result.TryGetProperty("nextCursor", out _));
        foreach (var resource in resources)
        {
            Assert.AreEqual("text/markdown", resource.GetProperty("mimeType").GetString());
            Assert.StartsWith("surl --aihelp", resource.GetProperty("name").GetString()!);
        }
    }

    [TestMethod]
    public async Task RunAsync_ResourcesReadOfEachListedResource_ReturnsExactlyWhatSurlAiHelpPrintsForIt()
    {
        await using var host = new ProgramTestHost(TestContext.CancellationToken);
        await host.CallAsync(InitializeRequest);
        string?[] topics = [null, .. AiHelpTopics.All.Select(topic => topic.Name), "all"];

        var id = 10;
        foreach (var topic in topics)
        {
            var uri = AiHelpResourceProvider.UriOf(topic);
            var result = await host.CallAsync($$$"""{"jsonrpc":"2.0","id":{{{id++}}},"method":"resources/read","params":{"uri":"{{{uri}}}"}}""");

            var contents = Assert.ContainsSingle(result.GetProperty("contents").EnumerateArray());
            Assert.AreEqual(uri, contents.GetProperty("uri").GetString());
            Assert.AreEqual("text/markdown", contents.GetProperty("mimeType").GetString());
            Assert.AreEqual(AiHelpText.Answer(topic).Output, contents.GetProperty("text").GetString(), uri);
        }
    }

    [TestMethod]
    public async Task RunAsync_ResourcesReadOfAnUnknownTopic_AnswersResourceNotFound()
    {
        await using var host = new ProgramTestHost(TestContext.CancellationToken);
        await host.CallAsync(InitializeRequest);

        var response = await host.CallForResponseAsync(
            """{"jsonrpc":"2.0","id":2,"method":"resources/read","params":{"uri":"surl://aihelp/no-such-topic"}}""");

        Assert.AreEqual(-32002, response.GetProperty("error").GetProperty("code").GetInt32());
    }

    [TestMethod]
    public async Task RunAsync_StartServerThenInputCloses_AnswersTheStartStopsTheServerAndReturnsOk()
    {
        await using var host = new ProgramTestHost(TestContext.CancellationToken);
        await host.CallAsync(InitializeRequest);

        var result = await host.CallAsync(StartServerCall("""["http://127.0.0.1:0"]""", "[]"));
        Assert.AreEqual(1, host.Seams.ListenerFactory.OpenListenerCount);
        var exitCode = await host.CloseInputAsync();

        Assert.IsFalse(result.GetProperty("isError").GetBoolean());
        var structured = result.GetProperty("structuredContent");
        Assert.AreEqual("s1", structured.GetProperty("server_id").GetString());
        Assert.AreEqual(
            $"http://127.0.0.1:{FakeListenerFactory.BoundPort}/", Assert.ContainsSingle(structured.GetProperty("bound_urls").EnumerateArray()).GetString());
        Assert.AreEqual((int)SurlExitCode.Ok, exitCode);
        Assert.AreEqual(0, host.Seams.ListenerFactory.OpenListenerCount);
    }

    [TestMethod]
    public async Task RunAsync_StartThatWarns_WritesOneJsonRpcMessagePerLineToOutputAndTheWarningToError()
    {
        await using var host = new ProgramTestHost(TestContext.CancellationToken);
        await host.CallAsync(InitializeRequest);

        var result = await host.CallAsync(StartServerCall("""["http://127.0.0.1:0"]""", """["--allow-anonymous"]"""));
        await host.CloseInputAsync();

        var warning = Assert.ContainsSingle(
            result.GetProperty("structuredContent").GetProperty("log_lines").EnumerateArray()
                .Select(line => line.GetString()!)
                .Where(line => line.StartsWith("surl: warning:", StringComparison.Ordinal)));
        StringAssert.Contains(host.ErrorText, warning);
        Assert.HasCount(2, host.Output.Writes);
        foreach (var write in host.Output.Writes)
        {
            Assert.EndsWith("\n", write);
            Assert.AreEqual(1, write.Count(character => character == '\n'));
            using var message = JsonDocument.Parse(write);
            Assert.AreEqual("2.0", message.RootElement.GetProperty("jsonrpc").GetString());
        }
    }

    [TestMethod]
    public async Task RunAsync_CancelledWhileServing_StopsTheServersAndReturnsOk()
    {
        await using var host = new ProgramTestHost(TestContext.CancellationToken);
        await host.CallAsync(InitializeRequest);
        await host.CallAsync(StartServerCall("""["http://127.0.0.1:0"]""", "[]"));

        var exitCode = await host.CancelAsync();

        Assert.AreEqual((int)SurlExitCode.Ok, exitCode);
        Assert.AreEqual(0, host.Seams.ListenerFactory.OpenListenerCount);
    }

    [TestMethod]
    public async Task RunAsync_Help_WritesTheHelpToOutputAndReturnsOk()
    {
        using var output = new MemoryStream();

        var exitCode = await RunWithoutSessionAsync(["--help"], output, TextWriter.Null);

        Assert.AreEqual((int)SurlExitCode.Ok, exitCode);
        Assert.AreEqual(SurlMcpHelpText.Help, System.Text.Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_Version_WritesTheVersionLineToOutputAndReturnsOk()
    {
        using var output = new MemoryStream();

        var exitCode = await RunWithoutSessionAsync(["--version"], output, TextWriter.Null);

        Assert.AreEqual((int)SurlExitCode.Ok, exitCode);
        Assert.AreEqual(SurlMcpHelpText.VersionLine, System.Text.Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_UnknownWord_WritesTheRefusalToErrorAndReturnsFailedInit()
    {
        using var output = new MemoryStream();
        using var error = new StringWriter();

        var exitCode = await RunWithoutSessionAsync(["--bogus"], output, error);

        Assert.AreEqual((int)SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual("surl-mcp: option --bogus: is unknown" + Environment.NewLine, error.ToString());
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task RunAsync_RootThatDoesNotExist_WritesWhyToErrorAndReturnsFailedInit()
    {
        using var output = new MemoryStream();
        using var error = new StringWriter();
        var seams = new SurlMcpHostSeams((_, _) => throw new AssertFailedException("Nothing is served."), _ => false, _ => throw new AssertFailedException("No directory is set."));

        var exitCode = await Program.RunAsync(Stream.Null, output, error, ["--root", "missing"], NoEnvironment, seams, TestContext.CancellationToken);

        Assert.AreEqual((int)SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual("surl-mcp: (2) the root missing is not a directory" + Environment.NewLine, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_RootFromTheEnvironment_MakesItsFullPathTheCurrentDirectoryAndTheServersRoot()
    {
        var directoriesSet = new List<string>();
        SurlMcpHostSettings? settingsServed = null;
        var seams = new SurlMcpHostSeams(
            (settings, error) =>
            {
                settingsServed = settings;
                return new RunningSurlServers(settings, error, TimeProvider.System);
            },
            _ => true,
            directoriesSet.Add);

        var exitCode = await Program.RunAsync(
            Stream.Null, Stream.Null, TextWriter.Null, [], name => name == "SURL_MCP_ROOT" ? "served" : null, seams, TestContext.CancellationToken);

        Assert.AreEqual((int)SurlExitCode.Ok, exitCode);
        var fullRoot = Path.GetFullPath("served");
        CollectionAssert.AreEqual(new[] { fullRoot }, directoriesSet);
        Assert.AreEqual(fullRoot, settingsServed!.Root);
    }

    [TestMethod]
    public async Task RunAsync_ProcessSeamsWithTheCurrentDirectoryAsRootAndNoInput_ServesNothingAndReturnsOk()
    {
        var currentDirectory = Environment.CurrentDirectory;

        var exitCode = await Program.RunAsync(
            Stream.Null, Stream.Null, TextWriter.Null, ["--root", currentDirectory], NoEnvironment, TestContext.CancellationToken);

        Assert.AreEqual((int)SurlExitCode.Ok, exitCode);
        Assert.AreEqual(currentDirectory, Environment.CurrentDirectory);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task Main_UnknownWord_WritesTheRefusalToStandardErrorAndReturnsFailedInit()
    {
        var originalError = System.Console.Error;
        using var error = new StringWriter();
        System.Console.SetError(error);

        int exitCode;
        try
        {
            exitCode = await Program.Main(["--bogus"]);
        }
        finally
        {
            System.Console.SetError(originalError);
        }

        Assert.AreEqual((int)SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual("surl-mcp: option --bogus: is unknown" + Environment.NewLine, error.ToString());
    }

    [TestMethod]
    public void Main_NullArguments_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = Program.Main(null!); });
    }

    [TestMethod]
    [DataRow(PosixSignal.SIGINT)]
    [DataRow(PosixSignal.SIGTERM)]
    public void CreateStopOnSignal_Signal_CancelsTheDefaultHandlingAndTheStopToken(PosixSignal signal)
    {
        using var stop = new CancellationTokenSource();
        var context = new PosixSignalContext(signal);

        Program.CreateStopOnSignal(stop)(context);

        Assert.IsTrue(context.Cancel);
        Assert.IsTrue(stop.IsCancellationRequested);
    }

    private static string StartServerCall(string listenUrlsJson, string argumentsJson) =>
        """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"start_server","arguments":{"listen_urls":"""
        + listenUrlsJson + ""","arguments":""" + argumentsJson + "}}}";

    private Task<int> RunWithoutSessionAsync(string[] arguments, Stream output, TextWriter error) =>
        Program.RunAsync(
            Stream.Null,
            output,
            error,
            arguments,
            NoEnvironment,
            new SurlMcpHostSeams((_, _) => throw new AssertFailedException("Nothing is served."), _ => true, _ => { }),
            TestContext.CancellationToken);

    /// <summary>
    /// <see cref="Program.RunAsync(Stream, Stream, TextWriter, IReadOnlyList{string}, Func{string, string?}, SurlMcpHostSeams, CancellationToken)"/>
    /// running over in-memory standard streams, its servers started through <see cref="ApiTestSeams"/>.
    /// </summary>
    private sealed class ProgramTestHost : IAsyncDisposable
    {
        private readonly StringWriter errorText = new();
        private readonly UnitTestChannelInputStream input = new();
        private readonly CancellationTokenSource stop;
        private readonly Task<int> running;

        public ProgramTestHost(CancellationToken testCancellation)
        {
            stop = CancellationTokenSource.CreateLinkedTokenSource(testCancellation);
            var seams = new SurlMcpHostSeams(
                (settings, error) => new RunningSurlServers(
                    settings, error, TimeProvider.System, (options, token) => SurlServer.StartAsync(options, Seams.Host, token)),
                _ => true,
                _ => { });
            running = Program.RunAsync(input, Output, TextWriter.Synchronized(errorText), [], NoEnvironment, seams, stop.Token);
        }

        public ApiTestSeams Seams { get; } = new();

        public UnitTestLineOutputStream Output { get; } = new();

        public string ErrorText => errorText.ToString();

        /// <summary>Writes one request and returns its response's <c>result</c>.</summary>
        public async Task<JsonElement> CallAsync(string request) => (await CallForResponseAsync(request)).GetProperty("result");

        /// <summary>Writes one request and returns its whole response, <c>result</c> or <c>error</c>.</summary>
        public async Task<JsonElement> CallForResponseAsync(string request)
        {
            input.WriteLine(request);
            using var response = JsonDocument.Parse(await Output.NextWriteAsync());
            return response.RootElement.Clone();
        }

        public Task<int> CloseInputAsync()
        {
            input.Complete();
            return running;
        }

        public Task<int> CancelAsync()
        {
            stop.Cancel();
            return running;
        }

        public async ValueTask DisposeAsync()
        {
            input.Complete();
            await running;
            stop.Dispose();
            await input.DisposeAsync();
            errorText.Dispose();
        }
    }
}
