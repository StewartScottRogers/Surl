using System.Text.Json;
using Surl.Api;
using Surl.McpServing;
using Surl.Output;

namespace Surl.Mcp;

[TestClass]
public sealed class StartServerToolTests
{
    private static readonly string BoundHttpUrl = $"http://127.0.0.1:{FakeListenerFactory.BoundPort}/";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Handler_HttpOnLoopbackPortZero_ReturnsTheServersIdAndBoundUrl()
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(
            host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);

        Assert.IsFalse(result.IsError);
        var structured = result.StructuredContent!.Value;
        Assert.AreEqual("s1", structured.GetProperty("server_id").GetString());
        CollectionAssert.AreEqual(new[] { "http://127.0.0.1:0" }, Strings(structured, "listen_urls"));
        CollectionAssert.AreEqual(new[] { BoundHttpUrl }, Strings(structured, "bound_urls"));
        Assert.AreEqual(structured.GetRawText(), Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    public async Task Handler_AStartThatLogs_ReturnsTheLinesItLoggedAndCopiesThemToStandardError()
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(
            host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"], "arguments": ["--allow-uploads"]}""", TestContext.CancellationToken);

        var logLines = Strings(result.StructuredContent!.Value, "log_lines");
        Assert.IsNotEmpty(logLines);
        foreach (var line in logLines)
        {
            StringAssert.Contains(host.ErrorOutput.ToString(), line);
        }
    }

    [TestMethod]
    public async Task Handler_TwoStarts_NumbersTheServersInOrder()
    {
        await using var host = new McpTestHost();

        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);
        var second = await McpTestHost.CallAsync(
            host.StartServer, """{"listen_urls": ["ftp://[::1]:0"]}""", TestContext.CancellationToken);

        Assert.AreEqual("s2", second.StructuredContent!.Value.GetProperty("server_id").GetString());
    }

    [TestMethod]
    public async Task Handler_HttpsWithNoCertificate_IsAnErrorNamingExitCode58AndSurlsText()
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(
            host.StartServer, """{"listen_urls": ["https://127.0.0.1:0"]}""", TestContext.CancellationToken);

        AssertCertificateProblem(result);
        Assert.IsEmpty(host.Servers.List());
    }

    [TestMethod]
    public async Task Handler_ProductionStartAndHttpsWithNoCertificate_IsAnErrorNamingExitCode58BeforeAnythingBinds()
    {
        await using var servers = new RunningSurlServers(new SurlMcpHostSettings(), TextWriter.Null, TimeProvider.System);
        var tool = StartServerTool.Create(servers);

        var result = await McpTestHost.CallAsync(tool, """{"listen_urls": ["https://127.0.0.1:0"]}""", TestContext.CancellationToken);

        AssertCertificateProblem(result);
    }

    [TestMethod]
    public async Task Handler_AnUnknownScheme_IsAnErrorNamingSurlsExitCode()
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(
            host.StartServer, """{"listen_urls": ["nosuch://127.0.0.1:0"]}""", TestContext.CancellationToken);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual(1, result.StructuredContent!.Value.GetProperty("exit_code").GetInt32());
        Assert.AreEqual("UnsupportedProtocol", result.StructuredContent!.Value.GetProperty("exit_code_name").GetString());
    }

    [TestMethod]
    public async Task Handler_AnOptionAHostOwns_IsAnErrorWithSurlApisMessageAndNoExitCode()
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(
            host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"], "arguments": ["--version"]}""", TestContext.CancellationToken);

        Assert.IsTrue(result.IsError);
        Assert.IsNull(result.StructuredContent);
        StringAssert.Contains(Assert.ContainsSingle(result.TextContent), "--version");
    }

    [TestMethod]
    [DataRow("0.0.0.0")]
    [DataRow("192.168.1.10")]
    [DataRow("example.com")]
    public async Task Handler_ANonLoopbackHost_IsRefused(string listenHost)
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(
            host.StartServer, $$"""{"listen_urls": ["http://{{listenHost}}:0"]}""", TestContext.CancellationToken);

        AssertRefused(result, $"the listen host {listenHost} is not loopback");
        Assert.IsEmpty(host.Servers.List());
    }

    [TestMethod]
    public async Task Handler_ANonLoopbackHostAsAPositionalArgument_IsRefused()
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(
            host.StartServer,
            """{"listen_urls": ["http://127.0.0.1:0"], "arguments": ["ftp://0.0.0.0:0"]}""",
            TestContext.CancellationToken);

        AssertRefused(result, "the listen host 0.0.0.0 is not loopback");
    }

    [TestMethod]
    public async Task Handler_ANonLoopbackHostOnAHostThatAllowsIt_Starts()
    {
        await using var host = new McpTestHost(new SurlMcpHostSettings { AllowNonLoopback = true });

        var result = await McpTestHost.CallAsync(
            host.StartServer, """{"listen_urls": ["http://0.0.0.0:0"]}""", TestContext.CancellationToken);

        Assert.IsFalse(result.IsError);
    }

    [TestMethod]
    public async Task Handler_OneServerPastTheLimit_IsRefusedNamingTheLimit()
    {
        await using var host = new McpTestHost(new SurlMcpHostSettings { MaxServers = 2 });
        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);
        await McpTestHost.CallAsync(host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);

        var result = await McpTestHost.CallAsync(
            host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);

        AssertRefused(result, "at most 2 servers run at once");
        Assert.HasCount(2, host.Servers.List());
    }

    [TestMethod]
    public async Task Handler_APathOptionOnAHostWithNoRoot_IsRefused()
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(
            host.StartServer,
            """{"listen_urls": ["http://127.0.0.1:0"], "arguments": ["--directory", "served"]}""",
            TestContext.CancellationToken);

        AssertRefused(result, "--directory names a local path, and this host was started with no root");
    }

    [TestMethod]
    public async Task Handler_APathOutsideTheRoot_IsRefusedNamingTheOption()
    {
        var root = Path.Combine(Path.GetTempPath(), "surl-mcp-root");
        var outside = Path.Combine(Path.GetTempPath(), "elsewhere", "user.txt");
        await using var host = new McpTestHost(
            new SurlMcpHostSettings { Root = root },
            new StartServerSafetyCheck(new SurlMcpHostSettings { Root = root }, new PathConfinement(root, () => root, _ => null)));

        var result = await McpTestHost.CallAsync(
            host.StartServer,
            $$"""{"listen_urls": ["http://127.0.0.1:0"], "arguments": ["--user-file", {{JsonSerializer.Serialize(outside)}}]}""",
            TestContext.CancellationToken);

        AssertRefused(result, $"--user-file names {outside}, which is not inside the root");
    }

    [TestMethod]
    public async Task Handler_ADataDirectoryInsideTheRoot_Starts()
    {
        var root = Path.Combine(Path.GetTempPath(), "surl-mcp-root");
        var settings = new SurlMcpHostSettings { Root = root };
        await using var host = new McpTestHost(settings, new StartServerSafetyCheck(settings, new PathConfinement(root, () => root, _ => null)));

        var result = await McpTestHost.CallAsync(
            host.StartServer,
            """{"listen_urls": ["http://127.0.0.1:0"], "arguments": ["--directory", "served"]}""",
            TestContext.CancellationToken);

        Assert.IsFalse(result.IsError, string.Join("\n", result.TextContent));
    }

    [TestMethod]
    [DataRow("-K")]
    [DataRow("--config")]
    public async Task Handler_AConfigFile_IsRefused(string option)
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(
            host.StartServer,
            $$"""{"listen_urls": ["http://127.0.0.1:0"], "arguments": ["{{option}}", "surl.conf"]}""",
            TestContext.CancellationToken);

        AssertRefused(result, StartServerSafetyCheck.ConfigFileRule);
    }

    [TestMethod]
    public async Task Handler_FilesAndBase64Files_ServesBoth()
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(
            host.StartServer,
            """{"listen_urls": ["http://127.0.0.1:0"], "files": {"index.html": "<p>hi</p>"}, "files_base64": {"data.bin": "AAEC"}}""",
            TestContext.CancellationToken);

        Assert.IsFalse(result.IsError, string.Join("\n", result.TextContent));
    }

    [TestMethod]
    [DataRow("""{"listen_urls": ["http://127.0.0.1:0"], "files": {"a.txt": 1}}""", "files.a.txt: expected a string")]
    [DataRow("""{"listen_urls": ["http://127.0.0.1:0"], "files_base64": {"a.bin": "not base64!"}}""", "files_base64.a.bin: expected a base64 string")]
    [DataRow("""{"listen_urls": ["http://127.0.0.1:0"], "files": {"a.txt": "x"}, "files_base64": {"a.txt": "AA=="}}""",
        "files_base64.a.txt: the path is given more than once across files and files_base64")]
    public async Task Handler_FilesThatCannotBeServed_IsAnErrorNamingThePath(string argumentsJson, string expected)
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(host.StartServer, argumentsJson, TestContext.CancellationToken);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual(expected, Assert.ContainsSingle(result.TextContent));
    }

    [TestMethod]
    public async Task Handler_ABadFilePath_IsAnErrorWithSurlApisMessage()
    {
        await using var host = new McpTestHost();

        var result = await McpTestHost.CallAsync(
            host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"], "files": {"../a.txt": "x"}}""", TestContext.CancellationToken);

        Assert.IsTrue(result.IsError);
        Assert.IsNull(result.StructuredContent);
    }

    [TestMethod]
    public async Task Create_HostSettings_StartsServersAtTheHostsLogLevel()
    {
        await using var host = new McpTestHost(new SurlMcpHostSettings { LogLevel = LogLevel.None });

        var result = await McpTestHost.CallAsync(
            host.StartServer, """{"listen_urls": ["http://127.0.0.1:0"]}""", TestContext.CancellationToken);

        Assert.IsEmpty(Strings(result.StructuredContent!.Value, "log_lines"));
    }

    [TestMethod]
    public void Create_NullServers_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => StartServerTool.Create(null!));

    private static string[] Strings(JsonElement structured, string name) =>
        [.. structured.GetProperty(name).EnumerateArray().Select(item => item.GetString()!)];

    private static void AssertCertificateProblem(McpToolResult result)
    {
        Assert.IsTrue(result.IsError);
        var structured = result.StructuredContent!.Value;
        Assert.AreEqual(58, structured.GetProperty("exit_code").GetInt32());
        Assert.AreEqual("CertificateProblem", structured.GetProperty("exit_code_name").GetString());
        const string Line = "(58) https://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one";
        CollectionAssert.AreEqual(new[] { Line }, Strings(structured, "lines"));
        Assert.AreEqual("surl: " + Line, Assert.ContainsSingle(result.TextContent));
    }

    private static void AssertRefused(McpToolResult result, string ruleStart)
    {
        Assert.IsTrue(result.IsError);
        var rule = result.StructuredContent!.Value.GetProperty("refused").GetString()!;
        Assert.StartsWith(ruleStart, rule);
        Assert.AreEqual("refused: " + rule, Assert.ContainsSingle(result.TextContent));
    }
}
