using System.Runtime.CompilerServices;

namespace Surl.Conformance;

/// <summary>
/// Phase 7 for TELNET (ADR-0089, BL-584): every session <see cref="UpstreamCurlTalksToSurlOverTelnetTests"/>
/// runs, through pinned upstream curl and the pinned Curl port, each fed the same standard input and run
/// against its own fresh <c>surl</c>, judged by <see cref="PinnedCurlPortBesideUpstreamCurl"/> and listed or
/// not in <c>Documentation/Conformance/CurlPort/Dict-Gopher-Telnet-Tftp-Mqtt.md</c> (decision 5). No request
/// bytes are recorded (ADR-0089 amendment 5): how curl interleaves standard input with its option negotiation
/// depends on when <c>surl</c>s options arrive, and the same upstream build sent them in either order from
/// run to run. The exit code, standard output and <c>surl</c>s <c>-v</c> notes carry the comparison.
/// Inconclusive when either pinned binary is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverTelnetTests
{
    private const string FindingsFile = "Dict-Gopher-Telnet-Tftp-Mqtt.md";

    private static readonly byte[] HelloThenQuit = "hello\nquit\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverTelnetTests.Session_PlainSession_ExitsAndPrintsWhatTheRecordingHolds")]
    public Task Session_PlainSession_ExitsAndPrintsWhatTheRecordingHolds() =>
        JudgeAsync("plain-session", HelloThenQuit);

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverTelnetTests.SessionWithTerminalTypeAndEnvironment_TelnetOptionsSession_ExitsAndPrintsWhatTheRecordingHolds")]
    public Task SessionWithTerminalTypeAndEnvironment_TelnetOptionsSession_ExitsAndPrintsWhatTheRecordingHolds() =>
        JudgeAsync("telnet-options-session", HelloThenQuit, ["-t", "TTYPE=vt100", "-t", "NEW_ENV=USER,alice"]);

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverTelnetTests.Session_IacInData_ExitsAndPrintsWhatTheRecordingHolds")]
    public Task Session_IacInData_ExitsAndPrintsWhatTheRecordingHolds() =>
        JudgeAsync("iac-in-data", [0xFF, .. "ab\nquit\n"u8]);

    /// <summary>
    /// Runs <c>curl -sS [telnet options] telnet://host:port</c>, the URL without a trailing slash as the
    /// mirrored test writes it, fed <paramref name="standardInput"/>, against a <c>surl</c> serving nothing,
    /// expecting the exit code and standard output recorded in <paramref name="fixtureFolder"/>.
    /// </summary>
    private async Task JudgeAsync(
        string fixtureFolder, byte[] standardInput, string[]? telnetOptions = null, [CallerMemberName] string method = "")
    {
        var fixture = Path.Combine(
            PinnedUpstreamCurl.RepositoryRoot(), "Protocols", "Terminal", "Surl.Protocol.Telnet.UnitTests", "Fixtures", fixtureFolder);
        var expectedExitCode = int.Parse(
            await File.ReadAllTextAsync(Path.Combine(fixture, "exitcode.txt"), TestContext.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        var expectedOutput = await File.ReadAllBytesAsync(Path.Combine(fixture, "stdout.bin"), TestContext.CancellationToken);
        string[] options = ["-sS", .. telnetOptions ?? []];
        var measurementCase = new CurlPortMeasurementCase(
            method,
            $"{nameof(UpstreamCurlTalksToSurlOverTelnetTests)}.{method}",
            "telnet",
            [.. options, "telnet://{host}:{port}"],
            "telnet",
            [],
            new Dictionary<string, byte[]>(),
            run => run.ExitCode == expectedExitCode && run.StandardOutput.AsSpan().SequenceEqual(expectedOutput)
                ? null
                : $"exit {run.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "timeout"}, {run.StandardOutput.Length} bytes out, stderr: {run.StandardError.Trim()}")
        {
            RecordsClientBytes = false,
        };
        var setup = new CurlPortRunSetup
        {
            StandardInput = standardInput,
            ArgumentsForRunAsync = (_, baseUrl, _) =>
                Task.FromResult<IReadOnlyList<string>>([.. options, $"telnet://{baseUrl.Host}:{baseUrl.Port}"]),
        };

        var measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(TestContext, measurementCase, setup);

        await CurlPortFindingsFile.AssertVerdictIsListedAsync(TestContext, FindingsFile, method, measurement);
    }
}
