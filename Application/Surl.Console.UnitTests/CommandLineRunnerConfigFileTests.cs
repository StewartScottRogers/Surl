using System.Text;
using Surl.Composition;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary><c>surl -K &lt;file&gt;</c> run end to end (ADR-0101 decisions 1 and 5).</summary>
[TestClass]
public sealed class CommandLineRunnerConfigFileTests
{
    private static readonly string NewLine = Environment.NewLine;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RunAsync_ConfigFileOnDisk_ServesTheListenUrlItNames()
    {
        var path = Path.Combine(Path.GetTempPath(), "surl-config-" + Guid.NewGuid().ToString("N") + ".cfg");
        await File.WriteAllTextAsync(path, "# a test config\nurl = http://127.0.0.1:0/\n", TestContext.CancellationToken);
        try
        {
            var (exitCode, output, error) = await ServeUntilListeningAsync(null, "-K", path);

            Assert.AreEqual(SurlExitCode.Ok, exitCode);
            Assert.AreEqual("Listening on http://127.0.0.1:<port>/" + NewLine, ReplacePort(output));
            Assert.AreEqual(string.Empty, error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RunAsync_ConfigFileMissing_WritesCouldNotReadConfigFileAndReturnsCouldNotReadFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "surl-missing-" + Guid.NewGuid().ToString("N") + ".cfg");

        var (exitCode, output, error) = await RunAsync(null, "-K", path, "http://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exitCode);
        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual($"surl: (37) Could not read config file {path}{NewLine}", error);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RunAsync_ConfigFileIsADirectory_ReturnsCouldNotReadFile()
    {
        var path = Path.GetTempPath();

        var (exitCode, _, error) = await RunAsync(null, "-K", path, "http://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exitCode);
        Assert.AreEqual($"surl: (37) Could not read config file {path}{NewLine}", error);
    }

    [TestMethod]
    public async Task RunAsync_ConfigFromStandardInput_ServesTheListenUrlItNames()
    {
        var source = new DiskConfigFileSource(() => new MemoryStream(Encoding.UTF8.GetBytes("url = http://127.0.0.1:0/\n")), fileNamesIgnoreCase: false);

        var (exitCode, output, error) = await ServeUntilListeningAsync(source, "-K", "-");

        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual("Listening on http://127.0.0.1:<port>/" + NewLine, ReplacePort(output));
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public async Task RunAsync_BadConfigLine_WritesTheRefusalAtItsLineAndTheTryLineAndReturnsFailedInit()
    {
        var source = new FakeConfigFileSource(new Dictionary<string, string> { ["a.cfg"] = "url = http://127.0.0.1:0/\nnosuchoption = 1\n" });

        var (exitCode, output, error) = await RunAsync(source, "-K", "a.cfg");

        Assert.AreEqual(SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(
            "surl: a.cfg:2: option --nosuchoption: is unknown" + NewLine
            + "surl: try 'surl --help' or 'surl --manual' for more information" + NewLine,
            error);
    }

    private static string ReplacePort(string output) =>
        System.Text.RegularExpressions.Regex.Replace(output, @":\d+/", ":<port>/");

    private async Task<(SurlExitCode ExitCode, string Output, string Error)> RunAsync(
        Surl.Cli.IConfigFileSource? source, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CreateRunner(new FakeListenerFactory(), source)
            .RunAsync(args, output, error, TestContext.CancellationToken);

        return (exitCode, output.ToString(), error.ToString());
    }

    private async Task<(SurlExitCode ExitCode, string Output, string Error)> ServeUntilListeningAsync(
        Surl.Cli.IConfigFileSource? source, params string[] args)
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = CreateRunner(factory, source).RunAsync(args, output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        return (exitCode, output.ToString(), error.ToString());
    }

    private static CommandLineRunner CreateRunner(FakeListenerFactory factory, Surl.Cli.IConfigFileSource? source) =>
        new(
            _ => factory,
            _ => true,
            _ => DataDirectoryLockOutcome.Taken(new FakeLockHolder()),
            TimeProvider.System,
            new UnitTestReadOnlyContentFileSystem(),
            configFiles: source);
}
