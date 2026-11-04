using System.Globalization;
using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// ADR-0046 decision 9's <c>Surl.Console</c> checks: every registered scheme has exactly one
/// <c>--aihelp</c> protocol topic, every protocol topic's schemes are registered, and every
/// <c>--aihelp</c> example writes what it shows when run through <see cref="CommandLineRunner"/>.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerAiHelpTests
{
    private const string PortPlaceholder = "<port>";

    private const string PathPlaceholder = "<path>";

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> Examples =>
        AiHelpExamples.All.Select(example => new object[] { example.Topic, example.Title });

    [TestMethod]
    public void RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic()
    {
        var registeredSchemes = ComposeRegisteredSchemes();

        foreach (var scheme in registeredSchemes)
        {
            var claimingTopics = AiHelpTopics.All
                .Where(topic => topic.Schemes.Contains(scheme, StringComparer.Ordinal))
                .Select(topic => topic.Name)
                .ToArray();
            Assert.HasCount(
                1,
                claimingTopics,
                $"Scheme \"{scheme}\" is claimed by the --aihelp topics [{string.Join(", ", claimingTopics)}], not by exactly one.");
        }
    }

    [TestMethod]
    public void ProtocolTopics_ClaimOnlyRegisteredSchemes()
    {
        var registeredSchemes = ComposeRegisteredSchemes().ToHashSet(StringComparer.Ordinal);

        var unregistered = AiHelpTopics.All
            .SelectMany(topic => topic.Schemes
                .Where(scheme => !registeredSchemes.Contains(scheme))
                .Select(scheme => $"{topic.Name}: {scheme}"))
            .ToArray();

        Assert.IsEmpty(unregistered, $"Topics claim schemes no registered server serves: {string.Join(", ", unregistered)}.");
    }

    [TestMethod]
    [DynamicData(nameof(Examples))]
    public async Task RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows(string topic, string title)
    {
        var example = AiHelpExamples.All.Single(candidate => candidate.Topic == topic && candidate.Title == title);
        var dataDirectory = Path.Join(Path.GetTempPath(), "surl-aihelp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDirectory);
        try
        {
            var (exitCode, output, error) = await RunExampleAsync(example, dataDirectory);

            Assert.AreEqual(example.ExitCode, exitCode, "exit code");
            CollectionAssert.AreEqual(Substitute(example.Output, dataDirectory), output, "stdout lines");
            CollectionAssert.AreEqual(Substitute(example.Error, dataDirectory), error, "stderr lines");
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    private static IReadOnlyList<string> ComposeRegisteredSchemes() =>
        new CommandLineRunner(
                _ => new FakeListenerFactory(),
                _ => true,
                _ => DataDirectoryLockOutcome.NoLock,
                TimeProvider.System)
            .ComposeRegisteredSchemes();

    private static string[] Substitute(IEnumerable<string> lines, string dataDirectory) =>
        [.. lines.Select(line => line
            .Replace(PortPlaceholder, FakeListenerFactory.BoundPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace(PathPlaceholder, dataDirectory, StringComparison.Ordinal))];

    private static DataDirectoryLockOutcome TakeLockAsThePreconditionSays(AiHelpExamplePrecondition precondition, string path) =>
        precondition == AiHelpExamplePrecondition.DataDirectoryHeldByAnotherSurl
            ? DataDirectoryLockOutcome.InUse(path)
            : DataDirectoryLockOutcome.Taken(new FakeLockHolder());

    // The files AiHelpExamplePrecondition.KeytabAndUserFileExist names; no other example reads a file.
    private static byte[] ReadStartFileAsThePreconditionSays(string path) => path switch
    {
        "http.keytab" => TestKeytabFiles.AesOnly,
        "users.txt" => "alice:secret\n"u8.ToArray(),
        _ => throw new FileNotFoundException(path),
    };

    private static string[] SplitLines(string text) =>
        text.Length == 0 ? [] : text.TrimEnd('\n').Split('\n').Select(line => line.TrimEnd('\r')).ToArray();

    private async Task<(SurlExitCode ExitCode, string[] Output, string[] Error)> RunExampleAsync(
        AiHelpExample example, string dataDirectory)
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = new CommandLineRunner(
                _ => factory,
                DataDirectoryProbe.CanOpen,
                path => TakeLockAsThePreconditionSays(example.Precondition, path),
                TimeProvider.System,
                readStartFile: ReadStartFileAsThePreconditionSays)
            .RunAsync(Substitute(example.Arguments, dataDirectory), output, error, stop.Token);
        if (example.ServesUntilStopped)
        {
            await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
            await stop.CancelAsync();
        }

        var exitCode = await running;
        return (exitCode, SplitLines(output.ToString()), SplitLines(error.ToString()));
    }
}
