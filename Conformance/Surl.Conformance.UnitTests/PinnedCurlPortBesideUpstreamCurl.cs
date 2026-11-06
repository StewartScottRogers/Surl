using System.Diagnostics;

namespace Surl.Conformance;

/// <summary>
/// Phase 7's one call (ADR-0089): runs a <see cref="CurlPortMeasurementCase"/> through the pinned
/// upstream build <see cref="UpstreamCurlLocator.LocateForProtocol"/> pairs it with and through the
/// pinned Curl port client, each against its own fresh <c>surl</c>, puts a <see cref="ClientBytesRelay"/> in front of each <c>surl</c> when the case
/// <see cref="CurlPortMeasurementCase.RecordsClientBytes"/>, and judges the two runs with
/// <see cref="CurlPortComparison"/>. A missing upstream build or port client makes the test
/// Inconclusive with the missing file's name. The port is the thing measured here, never an
/// expected value (ADR-0003 decision 6).
/// </summary>
internal static class PinnedCurlPortBesideUpstreamCurl
{
    /// <summary>
    /// The switches ADR-0089 decision 3 says neither client nor <c>surl</c> runs with: both run as shipped.
    /// </summary>
    private static readonly string[] ExtensionSwitches = ["CURL_EXTENSIONS", "SURL_EXTENSIONS"];

    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan RelayDrainTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Runs <paramref name="measurementCase"/> through both clients and judges the two runs.
    /// </summary>
    /// <param name="testContext">The running test's context.</param>
    /// <param name="measurementCase">The case.</param>
    /// <param name="arrangeAsync">
    /// Run once per client, against that run's own <c>surl</c>, after <c>surl</c> starts and before the
    /// client does, as the mirrored test arranges its <c>surl</c> (a connection held open, say). What it
    /// returns is disposed after the client exits and before <c>surl</c> stops, so it can also look at
    /// what <c>surl</c> served. <see langword="null"/> arranges nothing.
    /// </param>
    /// <returns>The verdict, and what each run's <c>surl</c> showed.</returns>
    public static async Task<PinnedCurlPortMeasurement> MeasureAsync(
        TestContext testContext,
        CurlPortMeasurementCase measurementCase,
        Func<SurlOnLoopback, CancellationToken, Task<IAsyncDisposable>>? arrangeAsync = null)
    {
        RequireNoExtensionSwitch();
        var root = PinnedUpstreamCurl.RepositoryRoot();
        var upstreamPins = UpstreamCurlBuildPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, UpstreamCurlBuildPins.FileName), testContext.CancellationToken));
        var portPins = CurlPortClientPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, CurlPortClientPins.FileName), testContext.CancellationToken));
        var files = new FileSystemUpstreamCurlFileAccess();
        var upstreamLocation = new UpstreamCurlLocator(files)
            .LocateForProtocol(upstreamPins, UpstreamCurlLocator.CurrentPlatform, measurementCase.UpstreamProtocol);
        if (!upstreamLocation.IsAvailable)
        {
            Assert.Inconclusive(upstreamLocation.Message);
        }

        var portLocation = new CurlPortClientLocator(files).Locate(portPins, upstreamPins, UpstreamCurlLocator.CurrentPlatform);
        var portRunner = PinnedCurlPortClientOnThisMachine.RunnerFor(portLocation);
        var upstreamRunner = new UpstreamCurlRunner(upstreamLocation, ClientTimeout, TimeProvider.System);

        var (upstream, upstreamSurl) = await RunAgainstFreshSurlAsync(
            measurementCase,
            (arguments, environment) => upstreamRunner.RunAsync(arguments, ReadOnlyMemory<byte>.Empty, environment, testContext.CancellationToken),
            arrangeAsync,
            testContext.CancellationToken);
        testContext.WriteLine($"{measurementCase.CommandLine}: {upstream}");
        var (port, portSurl) = await RunAgainstFreshSurlAsync(
            measurementCase,
            (arguments, environment) => portRunner.RunAsync(arguments, ReadOnlyMemory<byte>.Empty, environment, testContext.CancellationToken),
            arrangeAsync,
            testContext.CancellationToken);
        testContext.WriteLine($"{measurementCase.CommandLine}: {port}");

        var verdict = CurlPortComparison.Compare(
            measurementCase, upstream, upstreamSurl, port, portSurl, await SurlCommitAsync(root, testContext.CancellationToken));
        testContext.WriteLine($"{measurementCase.Name}: {verdict}");
        return new PinnedCurlPortMeasurement(verdict, upstreamSurl, portSurl);
    }

    private static void RequireNoExtensionSwitch()
    {
        foreach (var name in ExtensionSwitches)
        {
            if (Environment.GetEnvironmentVariable(name) is not null)
            {
                Assert.Inconclusive($"{name} is set in the test process; ADR-0089 decision 3 runs both clients and surl as shipped, without it.");
            }
        }
    }

    private static async Task<(TResult Result, CurlPortSurlSide Surl)> RunAgainstFreshSurlAsync<TResult>(
        CurlPortMeasurementCase measurementCase,
        Func<IReadOnlyList<string>, IReadOnlyDictionary<string, string>, Task<TResult>> runClientAsync,
        Func<SurlOnLoopback, CancellationToken, Task<IAsyncDisposable>>? arrangeAsync,
        CancellationToken cancellationToken)
    {
        var home = Directory.CreateTempSubdirectory("surl-phase7-home-");
        try
        {
            await using var surl = await SurlOnLoopback.StartAsync(
                measurementCase.SurlScheme, measurementCase.Files, ParentDirectoriesOf(measurementCase.Files.Keys), measurementCase.SurlOptions, cancellationToken);
            await using var relay = measurementCase.RecordsClientBytes ? ClientBytesRelay.Start(surl.BaseUrl, cancellationToken) : null;
            await using var arrangement = arrangeAsync is null ? null : await arrangeAsync(surl, cancellationToken);
            var environment = new Dictionary<string, string>
            {
                ["HOME"] = home.FullName,
                ["CURL_HOME"] = home.FullName,
                ["USERPROFILE"] = home.FullName,
            };

            var result = await runClientAsync(measurementCase.ArgumentsFor(relay?.BaseUrlFor(surl.BaseUrl) ?? surl.BaseUrl), environment);
            var side = new CurlPortSurlSide(surl.BaseUrl.Port, surl.Log, await ReadStoredFilesAsync(surl, measurementCase, cancellationToken));
            if (relay is not null)
            {
                await relay.WaitForConnectionsAsync(RelayDrainTimeout, cancellationToken);
                side = side with { RelayPort = relay.Port, ClientRequestBytes = relay.Transcript.ToConnections() };
            }

            return (result, side);
        }
        finally
        {
            home.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Every folder a served file sits in, ancestors first (<c>d1</c>, then <c>d1/d2</c> for
    /// <c>d1/d2/a.txt</c>), so <c>surl</c>'s content can hold a file below the top folder.
    /// </summary>
    internal static IReadOnlyList<string> ParentDirectoriesOf(IEnumerable<string> fileNames)
    {
        var directories = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var name in fileNames)
        {
            for (var slash = name.IndexOf('/', StringComparison.Ordinal); slash > 0; slash = name.IndexOf('/', slash + 1))
            {
                directories.Add(name[..slash]);
            }
        }

        return [.. directories];
    }

    private static async Task<IReadOnlyDictionary<string, byte[]>> ReadStoredFilesAsync(
        SurlOnLoopback surl, CurlPortMeasurementCase measurementCase, CancellationToken cancellationToken)
    {
        var stored = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var name in measurementCase.ComparedStoredFiles)
        {
            var path = Path.Combine(surl.ServedDirectory!, name);
            if (File.Exists(path))
            {
                stored[name] = await File.ReadAllBytesAsync(path, cancellationToken);
            }
        }

        return stored;
    }

    private static async Task<string> SurlCommitAsync(string root, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "-C", root, "rev-parse", "HEAD" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var git = Process.Start(startInfo)!;
            var commit = (await git.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
            await git.WaitForExitAsync(cancellationToken);
            return git.ExitCode == 0 && commit.Length > 0 ? commit : "unknown (git rev-parse HEAD failed)";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "unknown (git is not on PATH)";
        }
    }
}
