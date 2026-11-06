namespace Surl.Conformance;

/// <summary>
/// Phase 7's one call for a libcurl row (ADR-0089 decision 1 (c)): runs a <see cref="CurlPortLibcurlCase"/>
/// through upstream's libcurl driver, calling the pinned upstream <c>libcurl-4.dll</c>, and through the Curl
/// port's twin of it, calling the port's pinned managed <c>Libcurl</c>, each against its own fresh
/// <c>surl</c>, and judges the two runs with <see cref="CurlPortLibcurlComparison"/>. Inconclusive off
/// Windows, when either pinned library is missing, and when <c>CURL_EXTENSIONS</c> or <c>SURL_EXTENSIONS</c>
/// is set (decision 3). The port is the thing measured, never an expected value (ADR-0003 decision 6).
/// </summary>
internal static class PinnedCurlPortLibcurlBesideUpstreamLibcurl
{
    private static readonly string[] ExtensionSwitches = ["CURL_EXTENSIONS", "SURL_EXTENSIONS"];

    /// <summary>
    /// Runs <paramref name="libcurlCase"/> through both drivers, each against the <c>surl</c>
    /// <paramref name="startSurlAsync"/> starts afresh for it, and judges the two runs.
    /// </summary>
    public static async Task<PinnedCurlPortMeasurement> MeasureAsync(
        TestContext testContext, CurlPortLibcurlCase libcurlCase, Func<CancellationToken, Task<SurlOnLoopback>> startSurlAsync)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Only the Windows reference build ships a shared libcurl to pin, and the Curl port's library is pinned only for win-x64 (ADR-0071 decision 10, ADR-0089 decision 8).");
        }

        foreach (var name in ExtensionSwitches.Where(name => Environment.GetEnvironmentVariable(name) is not null))
        {
            Assert.Inconclusive($"{name} is set in the test process; ADR-0089 decision 3 runs both clients and surl as shipped, without it.");
        }

        var cancellationToken = testContext.CancellationToken;
        var root = PinnedUpstreamCurl.RepositoryRoot();
        var upstreamPins = UpstreamCurlBuildPins.Parse(await File.ReadAllTextAsync(Path.Combine(root, UpstreamCurlBuildPins.FileName), cancellationToken));
        var portPins = CurlPortClientPins.Parse(await File.ReadAllTextAsync(Path.Combine(root, CurlPortClientPins.FileName), cancellationToken));
        var files = new FileSystemUpstreamCurlFileAccess();
        var upstreamLibrary = new UpstreamCurlLocator(files).LocateLibrary(upstreamPins, UpstreamCurlLocator.CurrentPlatform);
        if (upstreamLibrary.Build is null)
        {
            Assert.Inconclusive(upstreamLibrary.Message);
        }

        var portLibrary = new CurlPortClientLocator(files).LocateLibrary(portPins, upstreamPins, UpstreamCurlLocator.CurrentPlatform);
        if (portLibrary.Client is null)
        {
            Assert.Inconclusive(portLibrary.Message);
        }

        var upstream = await RunAgainstFreshSurlAsync(
            testContext, libcurlCase.Script, new Dictionary<string, string>(), libcurlCase, startSurlAsync);
        var port = await RunAgainstFreshSurlAsync(
            testContext,
            PinnedLibcurlDriver.CurlPortTwinOf(libcurlCase.Script),
            new Dictionary<string, string> { [PinnedLibcurlDriver.CurlPortLibcurlVariable] = portLibrary.Path! },
            libcurlCase,
            startSurlAsync);

        var verdict = CurlPortLibcurlComparison.Compare(
            libcurlCase, upstream, upstreamLibrary.Build, port, portLibrary.Client, await PinnedCurlPortBesideUpstreamCurl.SurlCommitAsync(root, cancellationToken));
        testContext.WriteLine($"{libcurlCase.Name}: {verdict}");
        return new PinnedCurlPortMeasurement(verdict, upstream.Surl, port.Surl);
    }

    /// <summary>
    /// Turns a mirrored test's assertions into a <see cref="CurlPortLibcurlCase.CheckMirroredAssertions"/>:
    /// <see langword="null"/> when they pass on upstream's run, else the failed assertion's message.
    /// </summary>
    public static Func<CurlPortLibcurlRun, string?> Asserting(Action<CurlPortLibcurlRun> assertions) =>
        run =>
        {
            try
            {
                assertions(run);
                return null;
            }
            catch (AssertFailedException failure)
            {
                return failure.Message;
            }
        };

    private static async Task<CurlPortLibcurlRun> RunAgainstFreshSurlAsync(
        TestContext testContext,
        string script,
        IReadOnlyDictionary<string, string> environment,
        CurlPortLibcurlCase libcurlCase,
        Func<CancellationToken, Task<SurlOnLoopback>> startSurlAsync)
    {
        await using var surl = await startSurlAsync(testContext.CancellationToken);
        var (exitCode, lines, standardError) = await PinnedLibcurlDriver.RunScriptAsync(
            testContext, script, environment, libcurlCase.ArgumentsFor(surl.BaseUrl.Authority));
        var notes = await PinnedCurlPortBesideUpstreamCurl.SettledNotesAsync(surl, testContext.CancellationToken);
        var stored = await ReadStoredFilesAsync(surl, libcurlCase.ComparedStoredFiles, testContext.CancellationToken);
        return new CurlPortLibcurlRun(exitCode, lines, standardError, new CurlPortSurlSide(surl.BaseUrl.Port, notes, stored));
    }

    private static async Task<IReadOnlyDictionary<string, byte[]>> ReadStoredFilesAsync(
        SurlOnLoopback surl, IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        var stored = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var path = Path.Combine(surl.ServedDirectory!, name);
            if (File.Exists(path))
            {
                stored[name] = await File.ReadAllBytesAsync(path, cancellationToken);
            }
        }

        return stored;
    }
}
