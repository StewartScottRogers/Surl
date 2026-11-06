namespace Surl.Conformance;

/// <summary>
/// Runs the Curl port client <c>CurlPortBuilds.json</c> pins for the current platform, for the
/// extended servers' integration tests only (ADR-0083 decisions 1 to 3). A test is inconclusive
/// where no pinned client is installed, and fails where the run times out. Never use it for an
/// original protocol: those are measured only against upstream curl, through
/// <see cref="PinnedUpstreamCurl"/>.
/// </summary>
internal static class PinnedCurlPortClientOnThisMachine
{
    private static readonly IReadOnlyDictionary<string, string> NoEnvironmentChanges = new Dictionary<string, string>();

    /// <summary>
    /// Runs the pinned client with <paramref name="arguments"/>, writing the result to the test's log.
    /// </summary>
    public static async Task<CurlPortClientRunResult> RunAsync(TestContext testContext, params string[] arguments)
    {
        var runner = RunnerFor(await LocateAsync(testContext));
        var result = await runner.RunAsync(arguments, ReadOnlyMemory<byte>.Empty, NoEnvironmentChanges, testContext.CancellationToken);

        testContext.WriteLine($"curl port client {string.Join(' ', arguments)}: {result}");
        Assert.IsFalse(result.TimedOut, $"{result}; stderr: {result.StandardError}");
        return result;
    }

    /// <summary>
    /// Returns a runner for <paramref name="location"/>'s client, or marks the test inconclusive
    /// with the location's message when no pinned client is available on this machine.
    /// </summary>
    internal static CurlPortClientRunner RunnerFor(CurlPortClientLocation location)
    {
        if (!location.IsAvailable)
        {
            Assert.Inconclusive(location.Message);
        }

        return new CurlPortClientRunner(location, TimeSpan.FromSeconds(30), TimeProvider.System);
    }

    internal static async Task<CurlPortClientLocation> LocateAsync(TestContext testContext)
    {
        var root = PinnedUpstreamCurl.RepositoryRoot();
        var pins = CurlPortClientPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, CurlPortClientPins.FileName), testContext.CancellationToken));
        var upstreamPins = UpstreamCurlBuildPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, UpstreamCurlBuildPins.FileName), testContext.CancellationToken));

        return new CurlPortClientLocator(new FileSystemUpstreamCurlFileAccess())
            .Locate(pins, upstreamPins, UpstreamCurlLocator.CurrentPlatform);
    }
}
