using System.Text;
using Surl.Composition;
using Surl.Protocol.Abstractions;
using static System.Environment;

namespace Surl.Console;

/// <summary>ADR-0111 decision 5: <c>surl mcp+stdio:</c> serves one MCP session on the process's standard streams.</summary>
[TestClass]
public sealed class CommandLineRunnerMcpStdioTests
{
    private const string InitializeLine =
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}}\n";

    public TestContext TestContext { get; set; } = null!;

    private static CommandLineRunner CreateRunner(IEnvironmentVariableReader environmentVariables, Stream input, Stream standardOutput) =>
        new(
            _ => new FakeListenerFactory(),
            _ => true,
            _ => DataDirectoryLockOutcome.NoLock,
            TimeProvider.System,
            new UnitTestReadOnlyContentFileSystem(),
            environmentVariables: environmentVariables,
            openStandardInput: () => input,
            openStandardOutput: () => standardOutput);

    [TestMethod]
    public void ComposeExtendedSchemes_TheSwitchUnset_RegistersMcpStdio()
    {
        var runner = CreateRunner(FakeEnvironmentVariableReader.Empty, Stream.Null, Stream.Null);

        CollectionAssert.Contains(runner.ComposeExtendedSchemes().ToArray(), "mcp+stdio");
    }

    [TestMethod]
    public void ComposeExtendedSchemes_TheSwitchAtZero_RegistersNoMcpStdio()
    {
        var runner = CreateRunner(FakeEnvironmentVariableReader.ExtensionsTurnedOff, Stream.Null, Stream.Null);

        CollectionAssert.DoesNotContain(runner.ComposeExtendedSchemes().ToArray(), "mcp+stdio");
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("--log-level", "trace")]
    public async Task RunAsync_McpStdioInitialize_StandardOutputHoldsOnlyTheMcpMessage(params string[] options)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(InitializeLine));
        using var standardOutput = new MemoryStream();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(FakeEnvironmentVariableReader.Empty, input, standardOutput);

        var exitCode = await runner.RunAsync([.. options, "mcp+stdio:"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        var lines = Encoding.UTF8.GetString(standardOutput.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(1, lines);
        Assert.StartsWith("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":", lines[0]);
        Assert.AreEqual(string.Empty, output.ToString());
        Assert.AreEqual("Listening on mcp+stdio:" + NewLine, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_McpStdioBesideAnotherListenUrl_IsRefusedWithFailedInit()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(FakeEnvironmentVariableReader.Empty, Stream.Null, Stream.Null);

        var exitCode = await runner.RunAsync(["mcp+stdio:", "http://127.0.0.1:0/"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual(
            "surl: mcp+stdio: serves standard input and output and cannot be combined with another listen URL" + NewLine, error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_McpStdioWithTheSwitchAtZero_RefusesItsSchemeAsAnUnknownScheme()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = CreateRunner(FakeEnvironmentVariableReader.ExtensionsTurnedOff, Stream.Null, Stream.Null);

        var exitCode = await runner.RunAsync(["mcp+stdio:"], output, error, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.AreEqual("surl: (1) Protocol \"mcp+stdio\" not supported" + NewLine, error.ToString());
    }
}
