using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Phase 7 for RTSP (ADR-0089, BL-588): every conversation <see cref="UpstreamCurlTalksToSurlOverRtspTests"/>
/// runs, through pinned upstream curl and the pinned Curl port, each against its own fresh <c>surl</c>,
/// judged by <see cref="PinnedCurlPortBesideUpstreamCurl"/>. One test per mirrored test, each naming it in
/// its <see cref="DescriptionAttribute"/>. A test fails when upstream's run is not what the mirrored test
/// asserts (a Surl defect), when the port disagrees and <c>Documentation/Conformance/CurlPort/Rtsp.md</c>
/// does not list the case, and when a listed case agrees (ADR-0089 decision 5). The libcurl rows of
/// <see cref="PinnedLibcurlTalksToSurlOverRtspTests"/> are BL-597's (ADR-0089 decision 1 (c)).
/// Inconclusive when either pinned binary is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverRtspTests
{
    private const string FindingsFile = "Documentation/Conformance/CurlPort/Rtsp.md";

    private const string Account = "tester:secret";
    private const string WrongPassword = "tester:wrong";

    private const string Public =
        "Public: OPTIONS, DESCRIBE, ANNOUNCE, SETUP, PLAY, PAUSE, TEARDOWN, GET_PARAMETER, SET_PARAMETER, RECORD";

    private static readonly Dictionary<string, byte[]> ServedFiles = new()
    {
        ["clip.bin"] = "a clip of opaque octets\n"u8.ToArray(),
    };

    private static readonly string[] AccountOptions = ["--user", Account];
    private static readonly string[] PlaintextAccountOptions = ["--user", Account, "--allow-plaintext-auth"];

    private IsolatedCurlHome localFiles = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateLocalFiles() => localFiles = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteLocalFiles() => localFiles.Dispose();

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Options_ServedRoot_Exits0WithNothingWritten")]
    public Task Options_ServedRoot_Exits0WithNothingWritten() =>
        JudgeAsync(Case(Named(), ["{surl}/"], [], run => ExitsZero(run) ?? (run.StandardOutput.Length == 0 ? null : Describe(run))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Options_Include_WritesTheHeadWithCSeqDateServerAndPublic")]
    public Task Options_Include_WritesTheHeadWithCSeqDateServerAndPublic() =>
        JudgeAsync(Case(Named(), ["-i", "{surl}/clip.bin"], [], run =>
        {
            var head = Encoding.ASCII.GetString(run.StandardOutput).Split("\r\n");
            return run.ExitCode == 0
                && head[0] == "RTSP/1.0 200 OK"
                && head.Contains("CSeq: 1")
                && head.Contains("Server: surl")
                && head.Contains(Public)
                && head.Any(line => line.StartsWith("Date: ", StringComparison.Ordinal))
                ? null
                : Describe(run);
        }));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Options_MissingPathWithFieldsVerbose_Exits0AndSendsOptionsStar")]
    public Task Options_MissingPathWithFieldsVerbose_Exits0AndSendsOptionsStar() =>
        JudgeAsync(Case(Named(), ["-v", "-H", "X-Test: 1", "-A", "agent/1", "{surl}/missing"], [], run =>
            ExitsZero(run)
                ?? (run.StandardError.Contains("> OPTIONS * RTSP/1.0", StringComparison.Ordinal)
                    && run.StandardError.Contains("> X-Test: 1", StringComparison.Ordinal)
                    && run.StandardError.Contains("> User-Agent: agent/1", StringComparison.Ordinal)
                    ? null
                    : Describe(run))) with
        {
            AssertedStandardErrorFragments = ["> OPTIONS * RTSP/1.0", "> X-Test: 1", "> User-Agent: agent/1"],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Options_WriteOutResponseCode_Writes200")]
    public Task Options_WriteOutResponseCode_Writes200() =>
        JudgeAsync(Case(Named(), ["-w", "%{response_code}", "{surl}/clip.bin"], [], Exits(0, "200")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Upload_UploadsOn_Exits0AndStoresNothingBecauseCurlSendsNoBody")]
    public async Task Upload_UploadsOn_Exits0AndStoresNothingBecauseCurlSendsNoBody()
    {
        var upload = await localFiles.WriteFileAsync("up.sdp", "v=0\r\n"u8.ToArray(), TestContext.CancellationToken);
        var stored = new List<bool>();
        var measurementCase = Case(Named(), ["-T", upload, "{surl}/up.sdp"], ["--allow-uploads"], run =>
            ExitsZero(run) ?? (stored is [false, ..] ? null : $"up.sdp stored: {string.Join(", ", stored)}"));

        await JudgeAsync(measurementCase, (surl, cancellationToken) =>
            Task.FromResult<IAsyncDisposable>(new DisposeAction(() => stored.Add(File.Exists(Path.Combine(surl.ServedDirectory!, "up.sdp"))))));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Options_AccountsButNoLogin_Exits22")]
    public Task Options_AccountsButNoLogin_Exits22() =>
        JudgeAsync(Case(Named(), ["-f", "-w", "%{response_code}", "{surl}/clip.bin"], AccountOptions, Exits(22, "401")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Options_BasicInPlainTextWithoutAllowPlaintextAuth_Exits22With403")]
    [DataRow(false)]
    [DataRow(true)]
    public Task Options_BasicInPlainTextWithoutAllowPlaintextAuth_Exits22With403(bool basicNamed)
    {
        string[] login = basicNamed ? ["--basic", "-u", Account] : ["-u", Account];
        return JudgeAsync(Case(
            Named(basicNamed ? "True" : "False"), [.. login, "-f", "-w", "%{response_code}", "{surl}/clip.bin"], AccountOptions, Exits(22, "403")));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Options_BasicWithAllowPlaintextAuth_Exits0")]
    public Task Options_BasicWithAllowPlaintextAuth_Exits0() =>
        JudgeAsync(Case(Named(), ["-f", "-u", Account, "{surl}/clip.bin"], PlaintextAccountOptions, ExitsZero));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Options_BasicWrongPasswordWithAllowPlaintextAuth_Exits22With401")]
    public Task Options_BasicWrongPasswordWithAllowPlaintextAuth_Exits22With401() =>
        JudgeAsync(Case(Named(), ["-f", "-w", "%{response_code}", "-u", WrongPassword, "{surl}/clip.bin"], PlaintextAccountOptions, Exits(22, "401")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Options_Digest_Exits0OnOneConnection")]
    public Task Options_Digest_Exits0OnOneConnection() =>
        JudgeAsync(Case(Named(), ["-f", "--digest", "-u", Account, "-w", "%{num_connects}", "{surl}/clip.bin"], AccountOptions, Exits(0, "1")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Options_DigestWrongPassword_Exits22With401")]
    public Task Options_DigestWrongPassword_Exits22With401() =>
        JudgeAsync(Case(Named(), ["-f", "--digest", "-u", WrongPassword, "-w", "%{response_code}", "{surl}/clip.bin"], AccountOptions, Exits(22, "401")));

    // The mirrored test asserts on the "> Authorization: Digest " line, whose cnonce and response differ
    // every run, so that standard error line is not compared; the relay compares the same header in the
    // request bytes with both values normalised (ADR-0089 decision 4).
    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Options_AnyAuth_Exits0WithDigest")]
    public Task Options_AnyAuth_Exits0WithDigest() =>
        JudgeAsync(Case(Named(), ["-v", "-f", "--anyauth", "-u", Account, "{surl}/clip.bin"], AccountOptions, run =>
            ExitsZero(run) ?? (run.StandardError.Contains("> Authorization: Digest ", StringComparison.Ordinal) ? null : Describe(run))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Options_BearerWithAllowPlaintextAuth_Exits0")]
    public Task Options_BearerWithAllowPlaintextAuth_Exits0() =>
        JudgeAsync(Case(Named(), ["-f", "--oauth2-bearer", "tok", "{surl}/clip.bin"], ["--user", ":tok", "--allow-plaintext-auth"], ExitsZero));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Options_AllowAnonymous_Exits0WithoutALogin")]
    public Task Options_AllowAnonymous_Exits0WithoutALogin() =>
        JudgeAsync(Case(Named(), ["-f", "{surl}/clip.bin"], [.. AccountOptions, "--allow-anonymous"], ExitsZero));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverRtspTests.Options_HeadPastMaxRequestHead_Exits22With431")]
    public Task Options_HeadPastMaxRequestHead_Exits22With431() =>
        JudgeAsync(Case(
            Named(),
            ["-f", "-w", "%{response_code}", "-H", $"X-Long: {new string('x', 200)}", "{surl}/clip.bin"],
            ["--max-request-head", "128"],
            Exits(22, "431")));

    // --- The case, the mirrored assertions and the judgement ---

    private static CurlPortMeasurementCase Case(
        (string Name, string Mirrors) test, IReadOnlyList<string> arguments, IReadOnlyList<string> surlOptions, Func<CurlPortMeasuredRun, string?> check) =>
        new(
            test.Name,
            $"{nameof(UpstreamCurlTalksToSurlOverRtspTests)}.{test.Mirrors}",
            "rtsp",
            ["-sS", "-m", "20", .. arguments],
            "rtsp",
            surlOptions,
            ServedFiles,
            check);

    /// <summary>
    /// The case's name, the first column of the findings file: the test method's name, followed by
    /// its data row in brackets when it has one; and the mirrored method, which has the same name.
    /// </summary>
    private static (string Name, string Mirrors) Named(string? dataRow = null, [CallerMemberName] string method = "") =>
        (dataRow is null ? method : $"{method}[{dataRow}]", method);

    private static string Describe(CurlPortMeasuredRun run) =>
        $"exit {run.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "timeout"}, stdout: {Encoding.ASCII.GetString(run.StandardOutput)}, stderr: {run.StandardError.Trim()}";

    private static string? ExitsZero(CurlPortMeasuredRun run) => run.ExitCode == 0 ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> Exits(int exitCode, string output) =>
        run => run.ExitCode == exitCode && Encoding.ASCII.GetString(run.StandardOutput) == output ? null : Describe(run);

    private async Task JudgeAsync(
        CurlPortMeasurementCase measurementCase, Func<SurlOnLoopback, CancellationToken, Task<IAsyncDisposable>>? arrangeAsync = null)
    {
        var measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(TestContext, measurementCase, arrangeAsync);
        var listed = (await ListedFindingsAsync()).Contains(measurementCase.Name);
        var verdict = measurement.Verdict;

        Assert.AreNotEqual(CurlPortVerdictKind.SurlDefect, verdict.Kind, $"Surl defect: upstream's run is not what {measurementCase.Mirrors} asserts: {verdict.Reason}");
        if (verdict.Kind == CurlPortVerdictKind.PortDefect)
        {
            Assert.IsTrue(listed, $"A port defect {FindingsFile} does not list:\n{verdict}");
        }
        else if (verdict.Kind == CurlPortVerdictKind.Agreement)
        {
            Assert.IsFalse(listed, $"{FindingsFile} lists {measurementCase.Name}, but the port now agrees with upstream curl; remove the finding.");
        }
    }

    private async Task<HashSet<string>> ListedFindingsAsync()
    {
        var text = await File.ReadAllTextAsync(Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), FindingsFile), TestContext.CancellationToken);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match row in Regex.Matches(text, @"^\| `([^`]+)` \|", RegexOptions.Multiline))
        {
            names.Add(row.Groups[1].Value);
        }

        return names;
    }

    private sealed class DisposeAction(Action action) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            action();
            return ValueTask.CompletedTask;
        }
    }
}
