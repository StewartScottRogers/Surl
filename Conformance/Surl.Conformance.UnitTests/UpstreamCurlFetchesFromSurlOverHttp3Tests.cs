using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build fetches from a live, in-process <c>surl --http3</c> over HTTP/3
/// and QUIC (ADR-0082), with <c>--http3-only</c> and with <c>--http3</c>. On Windows each run uses
/// the static 8.21.0 build ADR-0082 decision 1 names, since the Windows reference build has no
/// HTTP3 feature; on Linux and macOS, the reference builds. Inconclusive where that build is not
/// installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlFetchesFromSurlOverHttp3Tests
{
    // stunnel/static-curl's build of upstream curl's tag 8.21.0: the Windows build HTTP/3 is
    // measured with (ADR-0082, decision 1).
    private const string Http3WindowsBuildSha256 = "589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648";

    // curl's -w variable for the HTTP version it spoke: "3" for HTTP/3.
    private const string HttpVersion = "%{http_version}";

    private static readonly byte[] Hello = "hello from surl\n"u8.ToArray();

    // 10 MiB: far past every window the server grants (ADR-0082 decision 4) and curl's own
    // stream window of 32,768 bytes, so the answer only completes when both sides widen them.
    private static readonly byte[] Large = [.. Enumerable.Range(0, 10 * 1024 * 1024).Select(index => (byte)(index % 251))];

    private static readonly byte[] Upload = [.. Enumerable.Range(0, 262_144).Select(index => (byte)(index % 251))];

    private static readonly string Credentials = $"{AccountsFile.User}:{AccountsFile.Password}";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GetWithHttp3Only_File_IsServedOverHttp3()
    {
        await using var surl = await StartSurlAsync("--http3");

        var result = await RunUpstreamCurlAsync("-sS", "-k", "--http3-only", "-w", HttpVersion, surl.UrlOf("hello.txt"));

        AssertServedOverHttp3(Hello, result);
    }

    [TestMethod]
    public async Task GetWithHttp3_File_IsServedOverHttp3NotTheTcpFallback()
    {
        await using var surl = await StartSurlAsync("--http3");

        var result = await RunUpstreamCurlAsync("-sS", "-k", "--http3", "-w", HttpVersion, surl.UrlOf("hello.txt"));

        AssertServedOverHttp3(Hello, result);
    }

    [TestMethod]
    public async Task Head_Http3Only_ExitsZeroWithAnHttp3OkHead()
    {
        await using var surl = await StartSurlAsync("--http3");

        var result = await RunUpstreamCurlAsync("-sS", "-k", "--http3-only", "-I", surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var head = Encoding.ASCII.GetString(result.StandardOutput);
        StringAssert.StartsWith(head, "HTTP/3 200 \r\n");
        StringAssert.Contains(head, $"content-length: {Hello.Length}\r\n");
        Assert.IsFalse(head.Contains("alt-svc", StringComparison.OrdinalIgnoreCase), head);
        StringAssert.EndsWith(head, "\r\n\r\n");
    }

    // ADR-0082 decision 6: with --http3, an answer over TCP carries Alt-Svc naming the shared port.
    [TestMethod]
    public async Task Head_OverTcpWithHttp3On_CarriesAltSvcNamingThePort()
    {
        await using var surl = await StartSurlAsync("--http3");

        var result = await RunUpstreamCurlAsync("-sS", "-k", "-I", surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var head = Encoding.ASCII.GetString(result.StandardOutput);
        StringAssert.StartsWith(head, "HTTP/2 200 \r\n");
        StringAssert.Contains(head, $"alt-svc: h3=\":{surl.BaseUrl.Port}\"; ma=86400\r\n");
    }

    // A single range is answered 206 with Content-Range and the bytes it selects on every HTTP
    // version (BL-723; ADR-0133, decision 1), and curl writes just those bytes and exits 0.
    [TestMethod]
    public async Task GetWithRange_Http3Only_IsServedTheFirstTenBytesWith206OverHttp3()
    {
        await using var surl = await StartSurlAsync("--http3");

        var result = await RunUpstreamCurlAsync(
            "-sS", "-k", "--http3-only", "-r", "0-9", "-w", " %{response_code} " + HttpVersion, surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(
            Hello.Take(10).Concat(" 206 3"u8.ToArray()).ToArray(), result.StandardOutput, Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task GetWithBasic_Http3Only_IsServedOverHttp3()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("--http3", "--user-file", accounts.Path);

        var result = await RunUpstreamCurlAsync(
            "-sS", "-f", "-k", "--http3-only", "--basic", "-u", Credentials, "-w", HttpVersion, surl.UrlOf("hello.txt"));

        AssertServedOverHttp3(Hello, result);
    }

    [TestMethod]
    public async Task GetWithBasic_WrongPassword_IsRefusedWith401OverHttp3()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("--http3", "--user-file", accounts.Path);

        var result = await RunUpstreamCurlAsync(
            "-sS", "-k", "--http3-only", "--basic", "-u", $"{AccountsFile.User}:wrong", "-o", NullDevice,
            "-w", "%{response_code} " + HttpVersion, surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual("401 3"u8.ToArray(), result.StandardOutput, Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task GetWithDigest_Http3Only_IsServedOverHttp3()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("--http3", "--user-file", accounts.Path);

        var result = await RunUpstreamCurlAsync(
            "-sS", "-f", "-k", "--http3-only", "--digest", "-u", Credentials, "-w", HttpVersion, surl.UrlOf("hello.txt"));

        AssertServedOverHttp3(Hello, result);
    }

    // Under --allow-uploads, with a login, a PUT is stored and answered 201 on every HTTP version
    // (BL-723; ADR-0133, decision 2): over HTTP/3 the body is read from the request stream's DATA
    // frames into the upload, which is committed once the stream ends.
    [TestMethod]
    public async Task Upload_AllowUploads_IsStoredWith201OverHttp3()
    {
        var upload = Path.Combine(Path.GetTempPath(), $"surl-conformance-upload-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(upload, Upload, TestContext.CancellationToken);
        try
        {
            using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
            await using var surl = await StartSurlAsync("--http3", "--allow-uploads", "--user-file", accounts.Path);

            var result = await RunUpstreamCurlAsync(
                "-sS", "-k", "--http3-only", "-u", Credentials, "-T", upload, "-o", NullDevice,
                "-w", "%{response_code} " + HttpVersion, surl.UrlOf("up.bin"));

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            CollectionAssert.AreEqual("201 3"u8.ToArray(), result.StandardOutput, Encoding.ASCII.GetString(result.StandardOutput));
            CollectionAssert.AreEqual(Upload, await File.ReadAllBytesAsync(Path.Combine(surl.ServedDirectory!, "up.bin"), TestContext.CancellationToken));
        }
        finally
        {
            File.Delete(upload);
        }
    }

    [TestMethod]
    public async Task GetWithHttp3Only_TenMebibyteFile_IsServedWholeOverHttp3()
    {
        await using var surl = await StartSurlAsync("--http3");

        var result = await RunUpstreamCurlAsync("-sS", "-k", "--http3-only", "-w", HttpVersion, surl.UrlOf("large.bin"));

        AssertServedOverHttp3(Large, result);
    }

    [TestMethod]
    public async Task GetWithHttp3Only_TwoUrls_ReuseOneQuicConnection()
    {
        await using var surl = await StartSurlAsync("--http3");

        var result = await RunUpstreamCurlAsync(
            "-sS", "-k", "--http3-only", "-v", "-w", HttpVersion, surl.UrlOf("hello.txt"), surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello.Concat("3"u8.ToArray()).Concat(Hello).Concat("3"u8.ToArray()).ToArray(), result.StandardOutput);
        StringAssert.Contains(result.StandardError, "* Reusing existing https: connection with host 127.0.0.1");
    }

    // ADR-0082 decision 6: without --http3 surl has no UDP listener, so --http3-only cannot connect.
    // Measured against no UDP listener (ADR-0082, "Measured"): exit 56 on Windows, where the ICMP
    // port-unreachable is a receive failure, and exit 7 on Linux. macOS answers 7 or 55 (CI run 43398470597
    // saw 55, "Failed sending data to the peer"): the ICMP lands on curl's connect check or on its first send.
    [TestMethod]
    public async Task GetWithHttp3Only_Http3Off_FailsToConnect()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunUpstreamCurlAsync("-sS", "-k", "--http3-only", surl.UrlOf("hello.txt"));

        Assert.Contains(result.ExitCode, ExpectedHttp3OffExitCodes(), $"{result.ExitCode}: {result.StandardError}");
        Assert.AreEqual(0, result.StandardOutput.Length);
        StringAssert.Contains(result.StandardError, "Failed to connect to 127.0.0.1");
    }

    private static int[] ExpectedHttp3OffExitCodes() =>
        OperatingSystem.IsWindows() ? [56] : OperatingSystem.IsMacOS() ? [7, 55] : [7];

    [TestMethod]
    public async Task GetWithHttp3_Http3Off_FallsBackToHttp2()
    {
        await using var surl = await StartSurlAsync();

        var result = await RunUpstreamCurlAsync("-sS", "-k", "--http3", "-w", HttpVersion, surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello.Concat("2"u8.ToArray()).ToArray(), result.StandardOutput);
    }

    private static string NullDevice => OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

    private static void AssertServedOverHttp3(byte[] expectedBody, UpstreamCurlRunResult result)
    {
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(expectedBody.Length + 1, result.StandardOutput.Length, result.StandardError);
        CollectionAssert.AreEqual(expectedBody.Concat("3"u8.ToArray()).ToArray(), result.StandardOutput);
    }

    private Task<SurlOnLoopback> StartSurlAsync(params string[] options) =>
        SurlOnLoopback.StartAsync(
            "https",
            new Dictionary<string, byte[]> { ["hello.txt"] = Hello, ["large.bin"] = Large },
            [],
            ["--self-signed", .. options],
            TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunUpstreamCurlAsync(params string[] arguments) =>
        OperatingSystem.IsWindows()
            ? PinnedUpstreamCurl.RunSupplementaryBuildAsync(TestContext, Http3WindowsBuildSha256, arguments)
            : PinnedUpstreamCurl.RunAsync(TestContext, arguments);
}
