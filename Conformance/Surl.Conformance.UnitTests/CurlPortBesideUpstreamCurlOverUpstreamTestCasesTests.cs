using System.Globalization;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Phase 7's corpus part (b) (ADR-0089 decision 1(b), BL-591): every upstream test case the platform's
/// passing list names - the cases Surl's runner passes with pinned upstream curl as the client - is
/// played again with the pinned Curl port as the client, each run against its own fresh server halves.
/// Upstream's run of each case is played in the same test, so upstream stays the oracle: a case it no
/// longer passes is a Surl defect, never a port finding. The run is judged against
/// <c>Documentation/Conformance/CurlPort/Upstream-Test-Cases.md</c> by decision 5's rule.
/// </summary>
/// <remarks>
/// The port is described by its own <c>--version</c>, as runtests describes the curl it tests: a case
/// runs with the port's own <c>%VERSION</c>, <c>%VERNUM</c>, <c>%CURL</c> and features, and a case
/// runtests would skip for the port (a protocol its build lacks) is not comparable, not a finding.
/// Windows only until another port pin exists (decision 8): elsewhere the port is unavailable and the
/// test is Inconclusive.
/// </remarks>
[TestClass]
public sealed class CurlPortBesideUpstreamCurlOverUpstreamTestCasesTests
{
    private const string FindingsFileName = "Upstream-Test-Cases.md";

    private static readonly string[] ExtensionSwitches = ["CURL_EXTENSIONS", "SURL_EXTENSIONS"];

    private static readonly TimeSpan CaseTimeout = TimeSpan.FromSeconds(60);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("UpstreamTestCases")]
    public async Task RunBesideAsync_EveryCaseOnThePassingList_PortDefectsAreTheListedFindings()
    {
        foreach (var name in ExtensionSwitches.Where(name => Environment.GetEnvironmentVariable(name) is not null))
        {
            Assert.Inconclusive($"{name} is set in the test process; ADR-0089 decision 3 runs both clients and surl as shipped, without it.");
        }

        var root = PinnedUpstreamCurl.RepositoryRoot();
        var pins = UpstreamCurlBuildPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, UpstreamCurlBuildPins.FileName), TestContext.CancellationToken));
        var locator = new UpstreamCurlLocator(new FileSystemUpstreamCurlFileAccess());
        var location = locator.Locate(pins, UpstreamCurlLocator.CurrentPlatform);
        if (!location.IsAvailable)
        {
            Assert.Inconclusive(location.Message);
        }

        var smbLocation = locator.LocateForProtocol(pins, UpstreamCurlLocator.CurrentPlatform, "smb");
        var port = await PortClientAsync();
        var paths = await PassingCasesAsync(root, location.Build!.Platform);

        var work = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "upstream-test-case-runs", Path.GetRandomFileName()));
        try
        {
            var run = new UpstreamTestCasesAgainstSurlTests.CaseRun(location, smbLocation.IsAvailable ? smbLocation : null, work.FullName, TestContext);
            var rows = await run.RunBesideAsync(paths, port);

            var findingsFile = Path.Combine(root, "Documentation", "Conformance", "CurlPort", FindingsFileName);
            var listed = UpstreamTestCasePortComparison.ListedFindings(await File.ReadAllLinesAsync(findingsFile, TestContext.CancellationToken));
            var comparison = UpstreamTestCasePortComparison.Compare(rows, listed);
            await WriteMeasurementAsync(run, port, comparison, work.FullName);

            Assert.HasCount(paths.Count, rows);
            Assert.IsTrue(comparison.Passes, comparison.DescribeFailures());
        }
        finally
        {
            DeleteBestEffort(work);
        }
    }

    // The port described by its own --version, as runtests describes the curl it runs.
    private async Task<UpstreamTestCasesAgainstSurlTests.CaseClient> PortClientAsync()
    {
        var location = await PinnedCurlPortClientOnThisMachine.LocateAsync(TestContext);
        if (!location.IsAvailable)
        {
            Assert.Inconclusive(location.Message);
        }

        var runner = new CurlPortClientRunner(location, CaseTimeout, TimeProvider.System);
        var version = await runner.RunAsync(["--version"], ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>(), TestContext.CancellationToken);
        var report = CurlVersionReport.Parse(Encoding.UTF8.GetString(version.StandardOutput));
        TestContext.WriteLine($"Curl port: {report.VersionLine}");

        return new UpstreamTestCasesAgainstSurlTests.CaseClient(new CurlPortTestCaseClient(runner), report, "port-");
    }

    private async Task<List<string>> PassingCasesAsync(string root, string platform)
    {
        var listPath = Path.Combine(root, UpstreamTestCasePassingList.Folder, UpstreamTestCasePassingList.FileNameFor(platform));
        var passing = UpstreamTestCasePassingList.Parse(await File.ReadAllTextAsync(listPath, TestContext.CancellationToken)).ToHashSet();

        return [.. UpstreamTestCasesAgainstSurlTests.TestCaseFiles()
            .Where(path => passing.Contains(int.Parse(Path.GetFileName(path)["test".Length..], CultureInfo.InvariantCulture)))];
    }

    // Writes what Upstream-Test-Cases.md states - the pins, the counts per family and every port
    // defect - to the log and to a results file, so the file can be updated from one run.
    private async Task WriteMeasurementAsync(
        UpstreamTestCasesAgainstSurlTests.CaseRun run, UpstreamTestCasesAgainstSurlTests.CaseClient port, UpstreamTestCasePortComparison comparison, string work)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"Port: {port.Client.Description}\n");
        foreach (var build in run.PairedBuilds)
        {
            text.Append(CultureInfo.InvariantCulture, $"Upstream: {build.Sha256} {build.Version}\n");
        }

        text.Append('\n').Append(comparison.FormatFamilyTable()).Append('\n');
        foreach (var row in comparison.Rows.Where(row => row.Verdict != CurlPortVerdictKind.Agreement))
        {
            text.Append(row).Append('\n');
        }

        var resultsFile = Path.Combine(TestContext.TestRunResultsDirectory ?? work, "curl-port-upstream-test-cases.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(resultsFile)!);
        await File.WriteAllTextAsync(resultsFile, text.ToString(), TestContext.CancellationToken);
        TestContext.AddResultFile(resultsFile);
        TestContext.WriteLine(text.ToString());
    }

    private static void DeleteBestEffort(DirectoryInfo directory)
    {
        try
        {
            directory.Delete(recursive: true);
        }
        catch (IOException)
        {
            // A file a client still held; the folder is the test run's to clean.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }
}
