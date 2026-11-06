using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build fetches from a live, in-process <c>surl</c> over HTTP/2 (ADR-0081):
/// by prior knowledge and by the <c>h2c</c> Upgrade on <c>http</c>, and by ALPN <c>h2</c> on
/// <c>https</c>. On Windows each run uses the static 8.21.0 build ADR-0081 decision 1 names, since
/// the Windows reference build has no HTTP2 feature; on Linux and macOS, the reference builds.
/// Inconclusive where that build is not installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlFetchesFromSurlOverHttp2Tests
{
    // stunnel/static-curl's build of upstream curl's tag 8.21.0: the Windows build HTTP/2 is
    // measured with (ADR-0081, decision 1).
    private const string Http2WindowsBuildSha256 = "589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648";

    // curl's -w variable for the HTTP version it spoke: "2" for HTTP/2.
    private const string HttpVersion = "%{http_version}";

    private static readonly byte[] Hello = "hello from surl\n"u8.ToArray();

    // Four times the default initial window of 65,535 bytes (RFC 9113, section 6.9.2), so the
    // answer only completes when the server honours curl's WINDOW_UPDATEs and curl the server's.
    private static readonly byte[] Large = [.. Enumerable.Range(0, 262_144).Select(index => (byte)(index % 251))];

    private static readonly string Credentials = $"{AccountsFile.User}:{AccountsFile.Password}";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GetWithPriorKnowledge_File_IsServedOverHttp2()
    {
        await using var surl = await StartSurlAsync("http");

        var result = await RunUpstreamCurlAsync("-sS", "--http2-prior-knowledge", "-w", HttpVersion, surl.UrlOf("hello.txt"));

        AssertServedOverHttp2(Hello, result);
    }

    [TestMethod]
    public async Task GetWithHttp2_File_IsUpgradedToHttp2()
    {
        await using var surl = await StartSurlAsync("http");

        var result = await RunUpstreamCurlAsync("-sS", "--http2", "-w", HttpVersion, surl.UrlOf("hello.txt"));

        AssertServedOverHttp2(Hello, result);
    }

    [TestMethod]
    public async Task GetWithHttp2AndInclude_File_ShowsThe101ThenTheHttp2Head()
    {
        await using var surl = await StartSurlAsync("http");

        var result = await RunUpstreamCurlAsync("-sS", "-i", "--http2", surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var output = Encoding.ASCII.GetString(result.StandardOutput);
        StringAssert.StartsWith(output, "HTTP/1.1 101 Switching Protocols\r\n");
        StringAssert.Contains(output, "\r\n\r\nHTTP/2 200 \r\n");
        StringAssert.EndsWith(output, "\r\n\r\nhello from surl\n");
    }

    [TestMethod]
    public async Task Get_HttpsByAlpn_IsServedOverHttp2()
    {
        await using var surl = await StartSurlAsync("https", "--self-signed");

        var result = await RunUpstreamCurlAsync("-sS", "-k", "-w", HttpVersion, surl.UrlOf("hello.txt"));

        AssertServedOverHttp2(Hello, result);
    }

    [TestMethod]
    public async Task Head_HttpsByAlpn_ExitsZeroWithAnHttp2OkHead()
    {
        await using var surl = await StartSurlAsync("https", "--self-signed");

        var result = await RunUpstreamCurlAsync("-sS", "-k", "-I", surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var head = Encoding.ASCII.GetString(result.StandardOutput);
        StringAssert.StartsWith(head, "HTTP/2 200 \r\n");
        StringAssert.Contains(head, $"content-length: {Hello.Length}\r\n");
        StringAssert.EndsWith(head, "\r\n\r\n");
    }

    // Measured 2026-11-05 (BL-723; ADR-0133, decision 1): a single range is answered 206 with
    // Content-Range and the bytes it selects, and curl writes just those bytes and exits 0.
    [TestMethod]
    public async Task GetWithRange_HttpsByAlpn_IsServedTheFirstTenBytesWith206OverHttp2()
    {
        await using var surl = await StartSurlAsync("https", "--self-signed");

        var result = await RunUpstreamCurlAsync("-sS", "-k", "-r", "0-9", "-w", " %{response_code} " + HttpVersion, surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(
            Hello.Take(10).Concat(" 206 2"u8.ToArray()).ToArray(), result.StandardOutput, Encoding.ASCII.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task GetWithBasic_HttpsByAlpn_IsServedOverHttp2()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("https", "--self-signed", "--user-file", accounts.Path);

        var result = await RunUpstreamCurlAsync(
            "-sS", "-f", "-k", "--basic", "-u", Credentials, "-w", HttpVersion, surl.UrlOf("hello.txt"));

        AssertServedOverHttp2(Hello, result);
    }

    [TestMethod]
    public async Task GetWithBasic_PriorKnowledgeAllowPlaintextAuth_IsServedOverHttp2()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("http", "--allow-plaintext-auth", "--user-file", accounts.Path);

        var result = await RunUpstreamCurlAsync(
            "-sS", "-f", "--http2-prior-knowledge", "--basic", "-u", Credentials, "-w", HttpVersion, surl.UrlOf("hello.txt"));

        AssertServedOverHttp2(Hello, result);
    }

    [TestMethod]
    public async Task GetWithBasic_WrongPassword_IsRefusedWith401OverHttp2()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("https", "--self-signed", "--user-file", accounts.Path);

        var result = await RunUpstreamCurlAsync(
            "-sS", "-k", "--basic", "-u", $"{AccountsFile.User}:wrong", "-o", NullDevice, "-w", "%{response_code} " + HttpVersion, surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual("401 2"u8.ToArray(), result.StandardOutput);
    }

    [TestMethod]
    public async Task GetWithDigest_PriorKnowledge_IsServedOverHttp2()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("http", "--user-file", accounts.Path);

        var result = await RunUpstreamCurlAsync(
            "-sS", "-f", "--http2-prior-knowledge", "--digest", "-u", Credentials, "-w", HttpVersion, surl.UrlOf("hello.txt"));

        AssertServedOverHttp2(Hello, result);
    }

    [TestMethod]
    public async Task GetWithDigest_HttpsByAlpn_IsServedOverHttp2()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("https", "--self-signed", "--user-file", accounts.Path);

        var result = await RunUpstreamCurlAsync(
            "-sS", "-f", "-k", "--digest", "-u", Credentials, "-w", HttpVersion, surl.UrlOf("hello.txt"));

        AssertServedOverHttp2(Hello, result);
    }

    // Measured 2026-11-05 (BL-723; ADR-0133, decision 2): under --allow-uploads, with a login, a
    // PUT of a body four times the initial window is read through the stream's flow control,
    // stored, and answered 201, and curl exits 0.
    [TestMethod]
    public async Task Upload_AllowUploads_IsStoredWith201OverHttp2()
    {
        var upload = Path.Combine(Path.GetTempPath(), $"surl-conformance-upload-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(upload, Large, TestContext.CancellationToken);
        try
        {
            using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
            await using var surl = await StartSurlAsync("https", "--self-signed", "--allow-uploads", "--user-file", accounts.Path);

            var result = await RunUpstreamCurlAsync(
                "-sS", "-k", "-u", Credentials, "-T", upload, "-o", NullDevice, "-w", "%{response_code} " + HttpVersion, surl.UrlOf("up.bin"));

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            CollectionAssert.AreEqual("201 2"u8.ToArray(), result.StandardOutput, Encoding.ASCII.GetString(result.StandardOutput));
            CollectionAssert.AreEqual(Large, await File.ReadAllBytesAsync(Path.Combine(surl.ServedDirectory!, "up.bin"), TestContext.CancellationToken));
        }
        finally
        {
            File.Delete(upload);
        }
    }

    [TestMethod]
    public async Task Get_FileLargerThanTheInitialWindow_IsServedWholeOverHttp2()
    {
        await using var surl = await StartSurlAsync("https", "--self-signed");

        var result = await RunUpstreamCurlAsync("-sS", "-k", "-w", HttpVersion, surl.UrlOf("large.bin"));

        AssertServedOverHttp2(Large, result);
    }

    [TestMethod]
    public async Task GetWithPriorKnowledge_FileLargerThanTheInitialWindow_IsServedWholeOverHttp2()
    {
        await using var surl = await StartSurlAsync("http");

        var result = await RunUpstreamCurlAsync("-sS", "--http2-prior-knowledge", "-w", HttpVersion, surl.UrlOf("large.bin"));

        AssertServedOverHttp2(Large, result);
    }

    [TestMethod]
    public async Task Get_TwoUrls_ReuseOneHttp2Connection()
    {
        await using var surl = await StartSurlAsync("https", "--self-signed");

        var result = await RunUpstreamCurlAsync(
            "-sS", "-k", "-v", "-w", HttpVersion, surl.UrlOf("hello.txt"), surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello.Concat("2"u8.ToArray()).Concat(Hello).Concat("2"u8.ToArray()).ToArray(), result.StandardOutput);
        Assert.AreEqual(1, CountOf(result.StandardError, "* Established connection to "), result.StandardError);
        StringAssert.Contains(result.StandardError, "* Reusing existing https: connection with host 127.0.0.1");
    }

    private static string NullDevice => OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

    private static int CountOf(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private static void AssertServedOverHttp2(byte[] expectedBody, UpstreamCurlRunResult result)
    {
        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(expectedBody.Concat("2"u8.ToArray()).ToArray(), result.StandardOutput);
    }

    private Task<SurlOnLoopback> StartSurlAsync(string scheme, params string[] options) =>
        SurlOnLoopback.StartAsync(
            scheme,
            new Dictionary<string, byte[]> { ["hello.txt"] = Hello, ["large.bin"] = Large },
            [],
            options,
            TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunUpstreamCurlAsync(params string[] arguments) =>
        OperatingSystem.IsWindows()
            ? PinnedUpstreamCurl.RunSupplementaryBuildAsync(TestContext, Http2WindowsBuildSha256, arguments)
            : PinnedUpstreamCurl.RunAsync(TestContext, arguments);
}
