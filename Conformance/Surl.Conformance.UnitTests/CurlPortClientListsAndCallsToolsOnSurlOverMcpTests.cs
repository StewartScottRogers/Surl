using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>mcp</c> client against a live <c>surl mcp://</c> and <c>mcps://</c>, and against
/// <c>surl mcp+stdio:</c> started as the client's server program (BL-469, ADR-0083 decisions 1 to 3). Each lists
/// tools, calls <c>echo</c> and has an unauthenticated HTTP request refused. Each expected value comes from the
/// Model Context Protocol specification, revision 2025-06-18, as <c>Documentation/Conformance/Extended/McpServer.md</c>
/// lists it - <c>echo</c> returns its <c>text</c> unchanged (MCP:Surl-Tools-4), <c>tools/list</c> names it
/// (MCP:Tools-2), a missing token is <c>401</c> with a Bearer challenge (MCP:Auth-3, MCP:Auth-5) - never from what
/// the port printed beyond the bytes the server was required to send. Inconclusive when the pinned client is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientListsAndCallsToolsOnSurlOverMcpTests
{
    private const string EchoCall = """{"name":"echo","arguments":{"text":"hello from the port"}}""";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("MCP:Lifecycle-1, MCP:Tools-2, MCP:Http-1: the client initializes, lists tools and prints echo, the tool every server offers")]
    [DataRow("mcp", false)]
    [DataRow("mcps", true)]
    public async Task List_OverStreamableHttp_PrintsTheEchoTool(string scheme, bool tls)
    {
        await using var surl = await StartAsync(scheme, tls, ["--allow-anonymous"]);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "-sS", "-k", "-m", "30", Url(surl, scheme));

        AssertListsEcho(result, surl.Log);
    }

    [TestMethod]
    [Description("MCP:Surl-Tools-4, MCP:Http-9, MCP:Tools-6: tools/call echo is answered as an SSE stream whose one response returns the text unchanged")]
    [DataRow("mcp", false)]
    [DataRow("mcps", true)]
    public async Task Call_OverStreamableHttp_PrintsTheEchoedText(string scheme, bool tls)
    {
        await using var surl = await StartAsync(scheme, tls, ["--allow-anonymous"]);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-sS", "-k", "-m", "30", "-d", EchoCall, Url(surl, scheme));

        AssertEchoed(result, surl.Log);
    }

    [TestMethod]
    [Description("MCP:Auth-5, MCP:Auth-3: with an account configured an unauthenticated request is 401 with a Bearer challenge naming the resource metadata, and the client reaches no tool")]
    public async Task Request_WithoutCredentials_IsRefusedWith401AndABearerChallenge()
    {
        await using var surl = await StartAsync("mcp", false, ["--user", "alice:secret"]);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "-sS", "-m", "30", Url(surl, "mcp"));

        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, Url(surl, "http"))
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"ping"}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        using var response = await http.SendAsync(request, TestContext.CancellationToken);
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, response.StatusCode, diagnostics);
        var challenge = string.Join(", ", response.Headers.WwwAuthenticate.Select(value => value.ToString()));
        StringAssert.Contains(challenge, "Bearer", diagnostics);
        StringAssert.Contains(challenge, "resource_metadata=", diagnostics);
        // The client's exit code for a 401 is its own choice, not the server's requirement; only "not 0" is asserted.
        Assert.AreNotEqual(0, result.ExitCode, diagnostics);
        StringAssert.Contains(result.StandardError, "401", diagnostics);
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }

    [TestMethod]
    [Description("MCP:Stdio-1, MCP:Stdio-3, MCP:Tools-2: the client starts surl mcp+stdio: as its server program and lists tools; standard output holds only what the client printed")]
    public async Task List_OverStdio_PrintsTheEchoTool()
    {
        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "-sS", "-m", "30", StdioUrl());

        AssertListsEcho(result, string.Empty);
    }

    [TestMethod]
    [Description("MCP:Stdio-1, MCP:Surl-Tools-4: over stdio tools/call echo returns the text unchanged")]
    public async Task Call_OverStdio_PrintsTheEchoedText()
    {
        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "-sS", "-m", "30", "-d", EchoCall, StdioUrl());

        AssertEchoed(result, string.Empty);
    }

    [TestMethod]
    [Description("MCP:Auth-9, MCP:Surl-Stdio-3: a stdio session needs no credential, so it lists tools with none given and no 401 is reported")]
    public async Task List_OverStdioWithoutCredentials_IsNotRefused()
    {
        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "-sS", "-m", "30", StdioUrl());

        var diagnostics = $"{result}; stderr: {result.StandardError}";
        Assert.AreEqual(0, result.ExitCode, diagnostics);
        Assert.DoesNotContain("401", result.StandardError, diagnostics);
    }

    private Task<SurlOnLoopback> StartAsync(string scheme, bool tls, string[] options) =>
        SurlOnLoopback.StartInMemoryAsync(scheme, tls ? [.. options, "--self-signed"] : options, TestContext.CancellationToken);

    private static string Url(SurlOnLoopback surl, string scheme) => $"{scheme}://127.0.0.1:{surl.BaseUrl.Port}/mcp";

    /// <summary>The URL that makes the client start the built surl as <c>surl mcp+stdio:</c>; inconclusive when it is not beside the tests.</summary>
    private static string StdioUrl()
    {
        var executable = PublishedSurlProcess.ConfiguredExecutablePath
            ?? Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "surl.exe" : "surl");
        if (!File.Exists(executable))
        {
            Assert.Inconclusive($"No surl executable at {executable}.");
        }

        var path = executable.Replace('\\', '/').Replace(" ", "%20", StringComparison.Ordinal);
        return $"mcp+stdio://localhost/{path.TrimStart('/')}?mcp%2Bstdio%3A".Replace("localhost/", OperatingSystem.IsWindows() ? "localhost/" : "localhost//", StringComparison.Ordinal);
    }

    private static void AssertListsEcho(CurlPortClientRunResult result, string surlLog)
    {
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surlLog}";
        Assert.AreEqual(0, result.ExitCode, diagnostics);
        var lines = Encoding.UTF8.GetString(result.StandardOutput).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.IsTrue(lines.Any(line => line.StartsWith("echo\t", StringComparison.Ordinal)), diagnostics);
    }

    private static void AssertEchoed(CurlPortClientRunResult result, string surlLog)
    {
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surlLog}";
        Assert.AreEqual(0, result.ExitCode, diagnostics);
        Assert.AreEqual("hello from the port", Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
    }
}
