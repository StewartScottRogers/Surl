using System.Runtime.CompilerServices;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Phase 7 for HTTP/3 (ADR-0089, BL-590): every conversation
/// <see cref="UpstreamCurlFetchesFromSurlOverHttp3Tests"/> runs against a live <c>surl --http3</c>
/// (<c>--http3-only</c>, <c>--http3</c> and the Alt-Svc header over TCP), through pinned upstream curl
/// and the pinned Curl port, each against its own fresh <c>surl</c>, judged by
/// <see cref="PinnedCurlPortBesideUpstreamCurl"/>. One test per mirrored test, each naming it in its
/// <see cref="DescriptionAttribute"/>. On Windows upstream's side is the stunnel/static-curl 8.21.0
/// build the mirrored test runs (ADR-0082 decision 1); elsewhere the reference build. Request bytes
/// travel over QUIC, which is UDP and encrypted, so ADR-0089 decision 4 leaves them uncompared: the
/// exit code, standard output, the standard error lines the mirrored test pins, stored files and
/// <c>surl</c>'s notes carry the comparison. A test fails on a Surl defect, on a port defect
/// <c>Documentation/Conformance/CurlPort/Http3.md</c> does not list, and on a listed case that agrees.
/// Inconclusive when either pinned binary is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverHttp3Tests
{
    private const string FindingsFile = "Http3.md";

    private const string ReusedConnection = "* Reusing existing https: connection with host 127.0.0.1";

    private const string FailedToConnect = "Failed to connect to 127.0.0.1";

    private const string Mirrored = nameof(UpstreamCurlFetchesFromSurlOverHttp3Tests);

    // stunnel/static-curl's build of upstream curl's tag 8.21.0, the Windows build the mirrored
    // test measures HTTP/3 with (ADR-0082, decision 1).
    private const string Http3WindowsBuildSha256 = "589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648";

    private const string HttpVersion = "%{http_version}";

    private static readonly byte[] Hello = "hello from surl\n"u8.ToArray();

    private static readonly byte[] Large = [.. Enumerable.Range(0, 10 * 1024 * 1024).Select(index => (byte)(index % 251))];

    private static readonly byte[] Upload = [.. Enumerable.Range(0, 262_144).Select(index => (byte)(index % 251))];

    private static readonly string Credentials = $"{AccountsFile.User}:{AccountsFile.Password}";

    public TestContext TestContext { get; set; } = null!;

    private static string NullDevice => OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithHttp3Only_File_IsServedOverHttp3")]
    public Task GetWithHttp3Only_File_IsServedOverHttp3() =>
        MeasureAsync(Case(["--http3"], ["-sS", "-k", "--http3-only", "-w", HttpVersion, "{surl}/hello.txt"], ServedOverHttp3(Hello)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithHttp3_File_IsServedOverHttp3NotTheTcpFallback")]
    public Task GetWithHttp3_File_IsServedOverHttp3NotTheTcpFallback() =>
        MeasureAsync(Case(["--http3"], ["-sS", "-k", "--http3", "-w", HttpVersion, "{surl}/hello.txt"], ServedOverHttp3(Hello)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp3Tests.Head_Http3Only_ExitsZeroWithAnHttp3OkHead")]
    public Task Head_Http3Only_ExitsZeroWithAnHttp3OkHead() =>
        MeasureAsync(Case(["--http3"], ["-sS", "-k", "--http3-only", "-I", "{surl}/hello.txt"], run =>
        {
            var head = Encoding.ASCII.GetString(run.StandardOutput);
            return run.ExitCode == 0
                && head.StartsWith("HTTP/3 200 \r\n", StringComparison.Ordinal)
                && head.Contains($"content-length: {Hello.Length}\r\n", StringComparison.Ordinal)
                && !head.Contains("alt-svc", StringComparison.OrdinalIgnoreCase)
                && head.EndsWith("\r\n\r\n", StringComparison.Ordinal)
                    ? null
                    : Describe(run);
        }));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp3Tests.Head_OverTcpWithHttp3On_CarriesAltSvcNamingThePort")]
    public Task Head_OverTcpWithHttp3On_CarriesAltSvcNamingThePort() =>
        MeasureAsync(Case(["--http3"], ["-sS", "-k", "-I", "{surl}/hello.txt"], run =>
        {
            var head = Encoding.ASCII.GetString(run.StandardOutput);
            return run.ExitCode == 0
                && head.StartsWith("HTTP/2 200 \r\n", StringComparison.Ordinal)
                && System.Text.RegularExpressions.Regex.IsMatch(head, "alt-svc: h3=\":[0-9]+\"; ma=86400\r\n")
                    ? null
                    : Describe(run);
        }));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithRange_Http3Only_IsServedTheFirstTenBytesWith206OverHttp3")]
    public Task GetWithRange_Http3Only_IsServedTheFirstTenBytesWith206OverHttp3() =>
        MeasureAsync(Case(
            ["--http3"],
            ["-sS", "-k", "--http3-only", "-r", "0-9", "-w", " %{response_code} " + HttpVersion, "{surl}/hello.txt"],
            ExitsZeroWriting([.. Hello.Take(10), .. " 206 3"u8.ToArray()])));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithBasic_Http3Only_IsServedOverHttp3")]
    public async Task GetWithBasic_Http3Only_IsServedOverHttp3()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            ["--http3", "--user-file", accounts.Path],
            ["-sS", "-f", "-k", "--http3-only", "--basic", "-u", Credentials, "-w", HttpVersion, "{surl}/hello.txt"],
            ServedOverHttp3(Hello)));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithBasic_WrongPassword_IsRefusedWith401OverHttp3")]
    public async Task GetWithBasic_WrongPassword_IsRefusedWith401OverHttp3()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            ["--http3", "--user-file", accounts.Path],
            ["-sS", "-k", "--http3-only", "--basic", "-u", $"{AccountsFile.User}:wrong", "-o", NullDevice, "-w", "%{response_code} " + HttpVersion, "{surl}/hello.txt"],
            ExitsZeroWriting("401 3"u8.ToArray())));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithDigest_Http3Only_IsServedOverHttp3")]
    public async Task GetWithDigest_Http3Only_IsServedOverHttp3()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            ["--http3", "--user-file", accounts.Path],
            ["-sS", "-f", "-k", "--http3-only", "--digest", "-u", Credentials, "-w", HttpVersion, "{surl}/hello.txt"],
            ServedOverHttp3(Hello)));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp3Tests.Upload_AllowUploads_IsStoredWith201OverHttp3")]
    public async Task Upload_AllowUploads_IsStoredWith201OverHttp3()
    {
        var upload = Path.Combine(Path.GetTempPath(), $"surl-phase7-upload-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(upload, Upload, TestContext.CancellationToken);
        try
        {
            using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

            var measurement = await MeasureAsync(Case(
                ["--http3", "--allow-uploads", "--user-file", accounts.Path],
                ["-sS", "-k", "--http3-only", "-u", Credentials, "-T", upload, "-o", NullDevice, "-w", "%{response_code} " + HttpVersion, "{surl}/up.bin"],
                ExitsZeroWriting("201 3"u8.ToArray())) with
            { ComparedStoredFiles = ["up.bin"] });

            CollectionAssert.AreEqual(Upload, measurement.UpstreamSurl.StoredFiles.GetValueOrDefault("up.bin"), "upstream curl's upload was not stored whole.");
        }
        finally
        {
            File.Delete(upload);
        }
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithHttp3Only_TenMebibyteFile_IsServedWholeOverHttp3")]
    public Task GetWithHttp3Only_TenMebibyteFile_IsServedWholeOverHttp3() =>
        MeasureAsync(Case(["--http3"], ["-sS", "-k", "--http3-only", "-w", HttpVersion, "{surl}/large.bin"], ServedOverHttp3(Large)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithHttp3Only_TwoUrls_ReuseOneQuicConnection")]
    public Task GetWithHttp3Only_TwoUrls_ReuseOneQuicConnection() =>
        MeasureAsync(Case(
            ["--http3"],
            ["-sS", "-k", "--http3-only", "-v", "-w", HttpVersion, "{surl}/hello.txt", "{surl}/hello.txt"],
            run => run.ExitCode == 0
                && run.StandardOutput.AsSpan().SequenceEqual([.. Hello, .. "3"u8.ToArray(), .. Hello, .. "3"u8.ToArray()])
                && run.StandardError.Contains(ReusedConnection, StringComparison.Ordinal)
                    ? null
                    : Describe(run)) with
        { AssertedStandardErrorFragments = [ReusedConnection] });

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithHttp3Only_Http3Off_FailsToConnect")]
    public Task GetWithHttp3Only_Http3Off_FailsToConnect() =>
        MeasureAsync(Case([], ["-sS", "-k", "--http3-only", "{surl}/hello.txt"], run =>
            run.ExitCode == (OperatingSystem.IsWindows() ? 56 : 7)
            && run.StandardOutput.Length == 0
            && run.StandardError.Contains(FailedToConnect, StringComparison.Ordinal)
                ? null
                : Describe(run)) with
        { AssertedStandardErrorFragments = [FailedToConnect] });

    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithHttp3_Http3Off_FallsBackToHttp2")]
    public Task GetWithHttp3_Http3Off_FallsBackToHttp2() =>
        MeasureAsync(Case([], ["-sS", "-k", "--http3", "-w", HttpVersion, "{surl}/hello.txt"], ExitsZeroWriting([.. Hello, .. "2"u8.ToArray()])));

    private static CurlPortMeasurementCase Case(
        IReadOnlyList<string> surlOptions,
        IReadOnlyList<string> arguments,
        Func<CurlPortMeasuredRun, string?> check,
        [CallerMemberName] string testName = "") =>
        new(
            testName,
            $"{Mirrored}.{testName}",
            "https",
            arguments,
            "https",
            ["--self-signed", .. surlOptions],
            new Dictionary<string, byte[]> { ["hello.txt"] = Hello, ["large.bin"] = Large },
            check);

    private static string Describe(CurlPortMeasuredRun run) =>
        $"exit {run.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "timeout"}, {run.StandardOutput.Length} bytes out, stderr: {run.StandardError.Trim()}";

    private static Func<CurlPortMeasuredRun, string?> ExitsZeroWriting(byte[] expected) =>
        run => run.ExitCode == 0 && run.StandardOutput.AsSpan().SequenceEqual(expected) ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> ServedOverHttp3(byte[] body) =>
        ExitsZeroWriting([.. body, .. "3"u8.ToArray()]);

    private async Task<PinnedCurlPortMeasurement> MeasureAsync(CurlPortMeasurementCase measurementCase)
    {
        var setup = new CurlPortRunSetup
        {
            UpstreamBuildSha256 = OperatingSystem.IsWindows() ? Http3WindowsBuildSha256 : null,
            ComparedNotes = CurlPortRunSetup.WithoutTlsEndings,
        };
        var measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(TestContext, measurementCase, setup);
        await CurlPortFindingsFile.AssertVerdictIsListedAsync(TestContext, FindingsFile, measurementCase.Name, measurement);
        return measurement;
    }
}
