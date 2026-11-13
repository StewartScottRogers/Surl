using System.Text;
using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class McpStdioCompositionTests
{
    private const string InitializeLine =
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}}\n";

    private static SurlCommandLine Parse(params string[] arguments) =>
        CommandLineParser.Parse(arguments, ["mcp+stdio", "mcp", "mcps"]).CommandLine!;

    [TestMethod]
    public void IsAsked_McpStdioListenUrl_IsTrue()
    {
        Assert.IsTrue(McpStdioComposition.IsAsked(Parse("mcp+stdio:")));
    }

    [TestMethod]
    public void IsAsked_AnotherListenUrl_IsFalse()
    {
        Assert.IsFalse(McpStdioComposition.IsAsked(Parse("mcp://127.0.0.1:0/")));
    }

    [TestMethod]
    public void IsAsked_NullCommandLine_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => McpStdioComposition.IsAsked(null!));
    }

    [TestMethod]
    public void FindRefusal_AloneWithDefaults_IsNull()
    {
        Assert.IsNull(McpStdioComposition.FindRefusal(Parse("mcp+stdio:")));
    }

    [TestMethod]
    public void FindRefusal_NullCommandLine_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => McpStdioComposition.FindRefusal(null!));
    }

    [TestMethod]
    public void FindRefusal_BesideAnotherListenUrl_IsFailedInitNamingWhy()
    {
        var refusal = McpStdioComposition.FindRefusal(Parse("mcp+stdio:", "mcp://127.0.0.1:0/"));

        Assert.AreEqual(SurlExitCode.FailedInit, refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "mcp+stdio: serves standard input and output and cannot be combined with another listen URL" }, refusal.Lines.ToArray());
    }

    [TestMethod]
    public void FindRefusal_BesideAProxy_IsFailedInit()
    {
        var refusal = McpStdioComposition.FindRefusal(Parse("mcp+stdio:", "--proxy", "http://127.0.0.1:0/"));

        Assert.AreEqual(SurlExitCode.FailedInit, refusal!.ExitCode);
    }

    [TestMethod]
    [DataRow("--trace")]
    [DataRow("--trace-ascii")]
    [DataRow("--log-file")]
    public void FindRefusal_AnOutputToStandardOutput_IsFailedInit(string option)
    {
        var refusal = McpStdioComposition.FindRefusal(Parse("mcp+stdio:", option, "-"));

        Assert.AreEqual(SurlExitCode.FailedInit, refusal!.ExitCode);
        Assert.StartsWith("mcp+stdio: standard output carries only MCP messages", refusal.Lines.Single());
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-s")]
    [DataRow("--trace")]
    public async Task ServeAsync_Initialize_WritesOnlyMcpMessagesToOutputAndTheStatusLineToTheLog(string verbosity)
    {
        var arguments = verbosity == "--trace" ? new[] { "mcp+stdio:", "--trace", "trace.txt" } : ["mcp+stdio:", verbosity];
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(InitializeLine));
        using var output = new MemoryStream();
        var log = new StringWriter();

        await McpStdioComposition.ServeAsync(Parse(arguments), TimeProvider.System, input, output, log, TestContext.CancellationToken);

        var lines = Encoding.UTF8.GetString(output.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(1, lines);
        Assert.StartsWith("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":", lines[0]);
        Assert.Contains("\"name\":\"surl\"", lines[0]);
        Assert.AreEqual(verbosity == "-s" ? string.Empty : "Listening on mcp+stdio:" + Environment.NewLine, log.ToString());
    }

    [TestMethod]
    public async Task ServeAsync_NullLog_Throws()
    {
        using var input = new MemoryStream();
        using var output = new MemoryStream();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => McpStdioComposition.ServeAsync(Parse("mcp+stdio:"), TimeProvider.System, input, output, null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ServeAsync_NullCommandLineOrClock_Throws()
    {
        using var input = new MemoryStream();
        using var output = new MemoryStream();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => McpStdioComposition.ServeAsync(null!, TimeProvider.System, input, output, TextWriter.Null, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => McpStdioComposition.ServeAsync(Parse("mcp+stdio:"), null!, input, output, TextWriter.Null, TestContext.CancellationToken));
    }

    public TestContext TestContext { get; set; } = null!;
}
