using System.Runtime.CompilerServices;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Phase 7 for Gopher (ADR-0089, BL-584): every conversation <see cref="UpstreamCurlFetchesFromSurlOverGopherTests"/>
/// runs, through pinned upstream curl and the pinned Curl port, each against its own fresh <c>surl</c>,
/// judged by <see cref="PinnedCurlPortBesideUpstreamCurl"/> and listed or not in
/// <c>Documentation/Conformance/CurlPort/Dict-Gopher-Telnet-Tftp-Mqtt.md</c> (decision 5). The mirrored
/// class runs no <c>gophers://</c> conversation, so none runs here. The relay records every
/// <c>gopher://</c> connection. Inconclusive when either pinned binary is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverGopherTests
{
    private const string FindingsFile = "Dict-Gopher-Telnet-Tftp-Mqtt.md";

    private static readonly byte[] FileBytes = "Hello from Surl.\n"u8.ToArray();

    private static readonly Dictionary<string, byte[]> ServedFiles = new() { ["file.txt"] = FileBytes };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverGopherTests.Fetch_FileSelector_ExitsZeroWithTheFilesBytes")]
    public Task Fetch_FileSelector_ExitsZeroWithTheFilesBytes() =>
        JudgeAsync("0/file.txt", [], _ => FileBytes);

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverGopherTests.FetchWithListDirectories_RootMenu_ExitsZeroWithAMenuOfTheServedDirectory")]
    public Task FetchWithListDirectories_RootMenu_ExitsZeroWithAMenuOfTheServedDirectory() =>
        JudgeAsync(
            string.Empty,
            ["--list-directories"],
            port => Encoding.ASCII.GetBytes($"0file.txt\t/file.txt\t127.0.0.1\t{port}\r\n1sub\t/sub\t127.0.0.1\t{port}\r\n.\r\n"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverGopherTests.Fetch_MissingSelector_ExitsZeroWithTheErrorMenu")]
    public Task Fetch_MissingSelector_ExitsZeroWithTheErrorMenu() =>
        JudgeAsync("0/missing.txt", [], _ => "3Nothing is served at this selector.\t\terror.host\t1\r\n.\r\n"u8.ToArray());

    /// <summary>
    /// Runs <c>curl -sS {surl}/<paramref name="selector"/></c> against a <c>surl</c> serving <c>file.txt</c>
    /// and an empty <c>sub</c> folder, as the mirrored test does, expecting exit 0 and the output
    /// <paramref name="expectedOutput"/> gives for that run's <c>surl</c> port.
    /// </summary>
    private async Task JudgeAsync(
        string selector, string[] surlOptions, Func<int, byte[]> expectedOutput, [CallerMemberName] string method = "")
    {
        var measurementCase = new CurlPortMeasurementCase(
            method,
            $"{nameof(UpstreamCurlFetchesFromSurlOverGopherTests)}.{method}",
            "gopher",
            ["-sS", CurlPortMeasurementCase.SurlBaseUrlPlaceholder + selector],
            "gopher",
            surlOptions,
            ServedFiles,
            run => run.ExitCode == 0 && run.StandardOutput.AsSpan().SequenceEqual(expectedOutput(run.Surl.Port))
                ? null
                : $"exit {run.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "timeout"}, {run.StandardOutput.Length} bytes out, stderr: {run.StandardError.Trim()}");

        var measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(
            TestContext,
            measurementCase,
            new CurlPortRunSetup
            {
                StartSurlAsync = cancellationToken => SurlOnLoopback.StartAsync("gopher", ServedFiles, ["sub"], surlOptions, cancellationToken),
            });

        await CurlPortFindingsFile.AssertVerdictIsListedAsync(TestContext, FindingsFile, method, measurement);
    }
}
