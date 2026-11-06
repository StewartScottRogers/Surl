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
    /// How long <c>surl</c>'s notes must stay unchanged after the client exits before they are read,
    /// so a connection the client opened and dropped (WinLDAP's unused one, a failed TLS handshake) is
    /// noted in every run rather than only when <c>surl</c> happened to log it first.
    /// </summary>
    private static readonly TimeSpan NotesQuietPeriod = TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan NotesSettleTimeout = TimeSpan.FromSeconds(5);

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
    public static Task<PinnedCurlPortMeasurement> MeasureAsync(
        TestContext testContext,
        CurlPortMeasurementCase measurementCase,
        Func<SurlOnLoopback, CancellationToken, Task<IAsyncDisposable>>? arrangeAsync = null) =>
        MeasureAsync(testContext, measurementCase, CurlPortRunSetup.None, arrangeAsync);

    /// <summary>
    /// As <see cref="MeasureAsync(TestContext, CurlPortMeasurementCase, Func{SurlOnLoopback, CancellationToken, Task{IAsyncDisposable}})"/>,
    /// for a case whose mirrored test names <c>surl</c>'s port before <c>surl</c> starts (a Kerberos
    /// service principal, a <c>--resolve</c> entry) or sets client environment variables (an MIT
    /// credential cache): each run's <c>surl</c> listens on <paramref name="surlPort"/> in turn, one after
    /// the other, so the case's arguments can name it literally, and both clients get
    /// <paramref name="clientEnvironment"/> (ADR-0089 decision 3: set identically for both).
    /// </summary>
    /// <param name="testContext">The running test's context.</param>
    /// <param name="measurementCase">The case.</param>
    /// <param name="surlPort">The port both runs' <c>surl</c> listens on; 0 for an ephemeral port each.</param>
    /// <param name="clientEnvironment">Variables set in both clients' environments, beside the empty home.</param>
    /// <param name="arrangeAsync">As in the overload without <paramref name="surlPort"/>.</param>
    /// <returns>The verdict, and what each run's <c>surl</c> showed.</returns>
    public static Task<PinnedCurlPortMeasurement> MeasureOnPortAsync(
        TestContext testContext,
        CurlPortMeasurementCase measurementCase,
        int surlPort,
        IReadOnlyDictionary<string, string> clientEnvironment,
        Func<SurlOnLoopback, CancellationToken, Task<IAsyncDisposable>>? arrangeAsync = null)
    {
        ArgumentNullException.ThrowIfNull(measurementCase);
        ArgumentNullException.ThrowIfNull(clientEnvironment);
        var setup = new CurlPortRunSetup
        {
            Environment = clientEnvironment,
            StartSurlAsync = cancellationToken => SurlOnLoopback.StartOnPortAsync(
                measurementCase.SurlScheme, surlPort, measurementCase.Files, ParentDirectoriesOf(measurementCase.Files.Keys), measurementCase.SurlOptions, cancellationToken),
        };
        return MeasureAsync(testContext, measurementCase, setup, arrangeAsync);
    }

    /// <summary>
    /// As <see cref="MeasureAsync(TestContext, CurlPortMeasurementCase, Func{SurlOnLoopback, CancellationToken, Task{IAsyncDisposable}})"/>,
    /// with <paramref name="setup"/> applied identically to both runs, for a case whose mirrored test runs a
    /// supplementary build, a relay of its own, several listeners or per-run arguments.
    /// </summary>
    /// <param name="testContext">The running test's context.</param>
    /// <param name="measurementCase">The case.</param>
    /// <param name="setup">What both runs need beyond the case.</param>
    /// <param name="arrangeAsync">As in the overload without <paramref name="setup"/>.</param>
    /// <returns>The verdict, and what each run's <c>surl</c> showed.</returns>
    public static async Task<PinnedCurlPortMeasurement> MeasureAsync(
        TestContext testContext,
        CurlPortMeasurementCase measurementCase,
        CurlPortRunSetup setup,
        Func<SurlOnLoopback, CancellationToken, Task<IAsyncDisposable>>? arrangeAsync = null)
    {
        RequireNoExtensionSwitch();
        var root = PinnedUpstreamCurl.RepositoryRoot();
        var upstreamPins = UpstreamCurlBuildPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, UpstreamCurlBuildPins.FileName), testContext.CancellationToken));
        var portPins = CurlPortClientPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, CurlPortClientPins.FileName), testContext.CancellationToken));
        var files = new FileSystemUpstreamCurlFileAccess();
        var upstreamLocation = setup.UpstreamBuildSha256 is { } sha256
            ? new UpstreamCurlLocator(files).Locate(
                [.. upstreamPins.Where(pin => pin.Sha256 == sha256)], UpstreamCurlLocator.CurrentPlatform, UpstreamCurlBuildRole.Supplementary)
            : new UpstreamCurlLocator(files)
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
            setup,
            (arguments, environment) => upstreamRunner.RunAsync(arguments, setup.StandardInput, environment, testContext.CancellationToken),
            arrangeAsync,
            testContext.CancellationToken);
        testContext.WriteLine($"{measurementCase.CommandLine}: {upstream}");
        var (port, portSurl) = await RunAgainstFreshSurlAsync(
            measurementCase,
            setup,
            (arguments, environment) => portRunner.RunAsync(arguments, setup.StandardInput, environment, testContext.CancellationToken),
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
        CurlPortRunSetup setup,
        Func<IReadOnlyList<string>, IReadOnlyDictionary<string, string>, Task<TResult>> runClientAsync,
        Func<SurlOnLoopback, CancellationToken, Task<IAsyncDisposable>>? arrangeAsync,
        CancellationToken cancellationToken)
    {
        var home = Directory.CreateTempSubdirectory("surl-phase7-home-");
        try
        {
            var environment = new Dictionary<string, string>
            {
                ["HOME"] = home.FullName,
                ["CURL_HOME"] = home.FullName,
                ["USERPROFILE"] = home.FullName,
            };
            foreach (var (name, value) in setup.Environment)
            {
                environment[name] = value;
            }

            TResult result;
            CurlPortSurlSide side;
            await using (var surl = await StartSurlAsync(measurementCase, setup, cancellationToken))
            {
                (result, side) = await RunClientAgainstAsync(surl, measurementCase, setup, environment, runClientAsync, arrangeAsync, cancellationToken);
            }

            if (setup.ReadStoredFilesAfterStopAsync is { } readStoredFilesAsync)
            {
                side = side with { StoredFiles = await readStoredFilesAsync(cancellationToken) };
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

    private static Task<SurlOnLoopback> StartSurlAsync(
        CurlPortMeasurementCase measurementCase, CurlPortRunSetup setup, CancellationToken cancellationToken) =>
        setup.StartSurlAsync is { } startSurlAsync
            ? startSurlAsync(cancellationToken)
            : SurlOnLoopback.StartAsync(measurementCase.SurlScheme, measurementCase.Files, ParentDirectoriesOf(measurementCase.Files.Keys), measurementCase.SurlOptions, cancellationToken);

    private static async Task<(TResult Result, CurlPortSurlSide Surl)> RunClientAgainstAsync<TResult>(
        SurlOnLoopback surl,
        CurlPortMeasurementCase measurementCase,
        CurlPortRunSetup setup,
        IReadOnlyDictionary<string, string> environment,
        Func<IReadOnlyList<string>, IReadOnlyDictionary<string, string>, Task<TResult>> runClientAsync,
        Func<SurlOnLoopback, CancellationToken, Task<IAsyncDisposable>>? arrangeAsync,
        CancellationToken cancellationToken)
    {
        var listener = surl.BaseUrlOf(measurementCase.SurlScheme);
        var front = setup.StartFrontRelay?.Invoke(listener, cancellationToken);
        await using var frontRelay = front?.Relay;
        var target = front?.BaseUrl ?? listener;
        await using var relay = measurementCase.RecordsClientBytes ? ClientBytesRelay.Start(target, cancellationToken) : null;
        var clientBaseUrl = relay?.BaseUrlFor(target) ?? target;
        var arguments = setup.ArgumentsForRunAsync is { } argumentsForRunAsync
            ? await argumentsForRunAsync(surl, clientBaseUrl, cancellationToken)
            : measurementCase.ArgumentsFor(clientBaseUrl);

        await using var arrangement = arrangeAsync is null ? null : await arrangeAsync(surl, cancellationToken);
        var result = await runClientAsync(arguments, environment);
        var settledNotes = await SettledNotesAsync(surl, cancellationToken);
        var notes = setup.ComparedNotes is { } comparedNotes ? comparedNotes(settledNotes) : settledNotes;
        var side = new CurlPortSurlSide(listener.Port, notes, await ReadStoredFilesAsync(surl, measurementCase, cancellationToken));
        if (relay is not null)
        {
            await relay.WaitForConnectionsAsync(RelayDrainTimeout, cancellationToken);
            side = side with { RelayPort = relay.Port, ClientRequestBytes = relay.Transcript.ToConnections() };
        }

        return (result, side);
    }

    /// <summary>
    /// <c>surl</c>'s notes once they have stayed unchanged for <see cref="NotesQuietPeriod"/>, or as they
    /// stand after <see cref="NotesSettleTimeout"/>, in exchange order.
    /// </summary>
    internal static async Task<string> SettledNotesAsync(SurlOnLoopback surl, CancellationToken cancellationToken)
    {
        var deadline = TimeProvider.System.GetTimestamp() + (long)(NotesSettleTimeout.TotalSeconds * TimeProvider.System.TimestampFrequency);
        var notes = surl.Log;
        while (TimeProvider.System.GetTimestamp() < deadline)
        {
            await Task.Delay(NotesQuietPeriod, TimeProvider.System, cancellationToken);
            var later = surl.Log;
            if (later == notes)
            {
                break;
            }

            notes = later;
        }

        return InExchangeOrder(notes);
    }

    /// <summary>
    /// <paramref name="notes"/> with each exchange's lines (<c>#2 * …</c>) grouped by exchange number,
    /// each exchange's own lines and the lines before the first exchange kept in the order <c>surl</c>
    /// wrote them. Two connections a client opens at once are served side by side, so which exchange's
    /// line <c>surl</c> writes first is the scheduler's choice, not the client's (ADR-0089 amendment 3).
    /// </summary>
    internal static string InExchangeOrder(string notes)
    {
        var exchange = 0;
        var lines = new List<(int Exchange, int Index, string Line)>();
        var trailingNewline = notes.EndsWith('\n');
        foreach (var line in (trailingNewline ? notes[..^1] : notes).Split('\n'))
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, @"^#(\d+) ");
            if (match.Success)
            {
                exchange = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }

            lines.Add((exchange, lines.Count, line));
        }

        var ordered = string.Join('\n', lines.OrderBy(entry => entry.Exchange).ThenBy(entry => entry.Index).Select(entry => entry.Line));
        return trailingNewline ? ordered + "\n" : ordered;
    }

    private static async Task<IReadOnlyDictionary<string, byte[]>> ReadStoredFilesAsync(
        SurlOnLoopback surl, CurlPortMeasurementCase measurementCase, CancellationToken cancellationToken)
    {
        var stored = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (surl.ServedDirectory is not { } servedDirectory)
        {
            return stored;
        }

        foreach (var name in measurementCase.ComparedStoredFiles)
        {
            var path = Path.Combine(servedDirectory, name);
            if (File.Exists(path))
            {
                stored[name] = await File.ReadAllBytesAsync(path, cancellationToken);
            }
        }

        return stored;
    }

    internal static async Task<string> SurlCommitAsync(string root, CancellationToken cancellationToken)
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
