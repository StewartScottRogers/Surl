namespace Surl.Conformance;

/// <summary>
/// Runs the upstream curl build <c>UpstreamCurlBuilds.json</c> pins for the current platform,
/// for the integration tests. A test is inconclusive where no pinned build is installed, and
/// fails where the run times out.
/// </summary>
internal static class PinnedUpstreamCurl
{
    private static readonly IReadOnlyDictionary<string, string> NoEnvironmentChanges = new Dictionary<string, string>();

    /// <summary>
    /// Runs the pinned build with <paramref name="arguments"/>, writing the result to the test's log.
    /// </summary>
    public static Task<UpstreamCurlRunResult> RunAsync(TestContext testContext, params string[] arguments) =>
        RunWithStandardInputAsync(testContext, ReadOnlyMemory<byte>.Empty, arguments);

    /// <summary>
    /// Runs the pinned build with <paramref name="arguments"/>, feeding it
    /// <paramref name="standardInput"/>, writing the result to the test's log.
    /// </summary>
    public static async Task<UpstreamCurlRunResult> RunWithStandardInputAsync(
        TestContext testContext, ReadOnlyMemory<byte> standardInput, params string[] arguments)
    {
        var pins = await ReadPinsAsync(testContext);
        var location = new UpstreamCurlLocator(new FileSystemUpstreamCurlFileAccess())
            .Locate(pins, UpstreamCurlLocator.CurrentPlatform);
        return await RunLocatedAsync(testContext, location, standardInput, NoEnvironmentChanges, arguments);
    }

    /// <summary>
    /// Runs the pinned build with <paramref name="arguments"/> and each of
    /// <paramref name="environment"/>'s variables set in its environment, writing the result
    /// to the test's log.
    /// </summary>
    public static async Task<UpstreamCurlRunResult> RunWithEnvironmentAsync(
        TestContext testContext, IReadOnlyDictionary<string, string> environment, params string[] arguments)
    {
        var pins = await ReadPinsAsync(testContext);
        var location = new UpstreamCurlLocator(new FileSystemUpstreamCurlFileAccess())
            .Locate(pins, UpstreamCurlLocator.CurrentPlatform);
        return await RunLocatedAsync(testContext, location, ReadOnlyMemory<byte>.Empty, environment, arguments);
    }

    /// <summary>
    /// Runs the one supplementary build pinned with <paramref name="sha256"/> for the current
    /// platform with <paramref name="arguments"/>, for a case only that build can measure,
    /// writing the result to the test's log.
    /// </summary>
    public static Task<UpstreamCurlRunResult> RunSupplementaryBuildAsync(
        TestContext testContext, string sha256, params string[] arguments) =>
        RunSupplementaryBuildWithEnvironmentAsync(testContext, sha256, NoEnvironmentChanges, arguments);

    /// <summary>
    /// Runs the one supplementary build pinned with <paramref name="sha256"/> for the current
    /// platform with <paramref name="arguments"/> and each of <paramref name="environment"/>'s
    /// variables set in its environment, for a case only that build can measure, writing the
    /// result to the test's log.
    /// </summary>
    public static async Task<UpstreamCurlRunResult> RunSupplementaryBuildWithEnvironmentAsync(
        TestContext testContext, string sha256, IReadOnlyDictionary<string, string> environment, params string[] arguments)
    {
        var pins = (await ReadPinsAsync(testContext)).Where(pin => pin.Sha256 == sha256).ToList();
        var location = new UpstreamCurlLocator(new FileSystemUpstreamCurlFileAccess())
            .Locate(pins, UpstreamCurlLocator.CurrentPlatform, UpstreamCurlBuildRole.Supplementary);
        return await RunLocatedAsync(testContext, location, ReadOnlyMemory<byte>.Empty, environment, arguments);
    }

    /// <summary>
    /// Runs the build pinned for the current platform that supports <paramref name="protocol"/>
    /// with <paramref name="arguments"/>: the reference build when it lists the protocol, and
    /// otherwise the supplementary build pinned for it, such as ADR-0030's static-curl Windows
    /// build for <c>smb</c>. Where no pinned build of the platform lists the protocol, or none
    /// is installed, the test is inconclusive with the pin named (ADR-0026 decision 2).
    /// </summary>
    public static async Task<UpstreamCurlRunResult> RunForProtocolAsync(
        TestContext testContext, string protocol, params string[] arguments)
    {
        var pins = await ReadPinsAsync(testContext);
        var location = new UpstreamCurlLocator(new FileSystemUpstreamCurlFileAccess())
            .LocateForProtocol(pins, UpstreamCurlLocator.CurrentPlatform, protocol);
        return await RunLocatedAsync(testContext, location, ReadOnlyMemory<byte>.Empty, NoEnvironmentChanges, arguments);
    }

    /// <summary>
    /// Runs the reference build pinned for the current platform with <paramref name="arguments"/>,
    /// for cases only that build answers, such as the Windows build's <c>WinLDAP</c> binds: a
    /// supplementary build that lists <paramref name="protocol"/> is never run instead. Where the
    /// reference pin lists no <paramref name="protocol"/>, the test is inconclusive with the pin
    /// named (ADR-0026 decision 2); where it is not installed, inconclusive as
    /// <see cref="RunAsync"/> is.
    /// </summary>
    public static async Task<UpstreamCurlRunResult> RunReferenceForProtocolAsync(
        TestContext testContext, string protocol, params string[] arguments)
    {
        var pins = await ReadPinsAsync(testContext);
        var platform = UpstreamCurlLocator.CurrentPlatform;
        var reference = pins.FirstOrDefault(pin => pin.Kind == UpstreamCurlBuildKind.Curl
            && pin.Platform == platform
            && pin.Role == UpstreamCurlBuildRole.Reference);
        if (reference is not null && !reference.Protocols.Contains(protocol, StringComparer.OrdinalIgnoreCase))
        {
            Assert.Inconclusive(
                $"The reference upstream curl UpstreamCurlBuilds.json pins for {platform}, {reference.DefaultPath} ({reference.Version}), lists no {protocol}.");
        }

        var location = new UpstreamCurlLocator(new FileSystemUpstreamCurlFileAccess()).Locate(pins, platform);
        return await RunLocatedAsync(testContext, location, ReadOnlyMemory<byte>.Empty, NoEnvironmentChanges, arguments);
    }

    private static async Task<IReadOnlyList<PinnedUpstreamCurlBuild>> ReadPinsAsync(TestContext testContext) =>
        UpstreamCurlBuildPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), UpstreamCurlBuildPins.FileName), testContext.CancellationToken));

    private static async Task<UpstreamCurlRunResult> RunLocatedAsync(
        TestContext testContext,
        UpstreamCurlLocation location,
        ReadOnlyMemory<byte> standardInput,
        IReadOnlyDictionary<string, string> environment,
        string[] arguments)
    {
        if (!location.IsAvailable)
        {
            Assert.Inconclusive(location.Message);
        }

        var runner = new UpstreamCurlRunner(location, TimeSpan.FromSeconds(30), TimeProvider.System);
        var result = await runner.RunAsync(arguments, standardInput, environment, testContext.CancellationToken);

        testContext.WriteLine($"curl {string.Join(' ', arguments)}: {result}");
        Assert.IsFalse(result.TimedOut, $"{result}; stderr: {result.StandardError}");
        return result;
    }

    /// <summary>
    /// Finds the repository root: the first directory above the test output that holds <c>Surl.slnx</c>.
    /// </summary>
    public static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Surl.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Surl.slnx was not found above the test output directory.");
    }
}
