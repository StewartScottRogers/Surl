namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build fetches from a live, in-process <c>surl</c> over HTTP/2 or HTTP/1.1 on
/// TLS (<c>https</c>), with the certificate <c>--cert</c> and <c>--key</c> name and with the
/// throwaway certificate surl makes with <c>--self-signed</c> (ADR-0010, section 3; ADR-0032,
/// section 10). Inconclusive where no
/// pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlFetchesFromSurlOverHttpsTests
{
    // CURLE_PEER_FAILED_VERIFICATION: curl could not verify the server's certificate.
    private const int PeerFailedVerification = 60;

    private static readonly byte[] Hello = "hello from surl\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Get_CertSignedByTheTrustedTestCa_ExitsZeroWithTheFilesBytes()
    {
        using var authority = TestCertificateAuthority.Create();
        await using var surl = await StartSurlAsync("--cert", authority.ServerCertificateFile, "--key", authority.ServerKeyFile);

        // Measured 2026-09-29: Schannel checks revocation by default and a throwaway CA publishes
        // no revocation list, so the pinned Windows build needs --ssl-no-revoke beside --cacert
        // (as ADR-0010 measured); OpenSSL builds accept --ssl-no-revoke and ignore it.
        var result = await RunUpstreamCurlAsync(
            "-sS", "--ssl-no-revoke", "--cacert", authority.CaCertificateFile, surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
    }

    [TestMethod]
    public async Task Get_CertSignedByACaNotTrusted_ExitsPeerFailedVerification()
    {
        using var authority = TestCertificateAuthority.Create();
        await using var surl = await StartSurlAsync("--cert", authority.ServerCertificateFile, "--key", authority.ServerKeyFile);

        var result = await RunUpstreamCurlAsync("-sS", "--ssl-no-revoke", surl.UrlOf("hello.txt"));

        Assert.AreEqual(PeerFailedVerification, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    [WindowsX64ReferenceBuildOnly]
    public async Task Get_TrustedTestCaWithRevocationChecked_ExitsPeerFailedVerificationOnSchannel()
    {
        using var authority = TestCertificateAuthority.Create();
        await using var surl = await StartSurlAsync("--cert", authority.ServerCertificateFile, "--key", authority.ServerKeyFile);

        // Measured 2026-09-29: Schannel cannot learn the revocation status of a certificate from
        // a CA with no revocation list, and fails the transfer with (60).
        var result = await RunUpstreamCurlAsync("-sS", "--cacert", authority.CaCertificateFile, surl.UrlOf("hello.txt"));

        Assert.AreEqual(PeerFailedVerification, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task GetWithInsecure_ThrowawayCertificate_ExitsZeroWithTheFilesBytes()
    {
        await using var surl = await StartSurlAsync("--self-signed");

        var result = await RunUpstreamCurlAsync("-sS", "-k", surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
    }

    [TestMethod]
    public async Task Get_ThrowawayCertificate_ExitsPeerFailedVerification()
    {
        await using var surl = await StartSurlAsync("--self-signed");

        var result = await RunUpstreamCurlAsync("-sS", surl.UrlOf("hello.txt"));

        Assert.AreEqual(PeerFailedVerification, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task GetWithInsecure_SameFileTwice_ExitsZeroWithTheFilesBytesTwice()
    {
        await using var surl = await StartSurlAsync("--self-signed");

        var result = await RunUpstreamCurlAsync("-sS", "-k", surl.UrlOf("hello.txt"), surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello.Concat(Hello).ToArray(), result.StandardOutput);
    }

    [TestMethod]
    public async Task GetWithHttp11_ThrowawayCertificate_IsServedOverHttp11()
    {
        await using var surl = await StartSurlAsync("--self-signed");

        var result = await RunUpstreamCurlAsync("-sS", "-k", "--http1.1", "-w", "%{http_version}", surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello.Concat("1.1"u8.ToArray()).ToArray(), result.StandardOutput);
    }

    // curl negotiates HTTP/2 by ALPN by default where it has the HTTP2 feature (ADR-0081,
    // decision 2): the Linux and macOS reference builds do.
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task Get_ThrowawayCertificate_IsServedOverHttp2()
    {
        await using var surl = await StartSurlAsync("--self-signed");

        var result = await RunUpstreamCurlAsync("-sS", "-k", "-w", "%{http_version}", surl.UrlOf("hello.txt"), surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello.Concat("2"u8.ToArray()).Concat(Hello).Concat("2"u8.ToArray()).ToArray(), result.StandardOutput);
    }

    // The Windows reference build (Schannel) has no HTTP2 feature, so it offers only http/1.1.
    [TestMethod]
    [WindowsX64ReferenceBuildOnly]
    public async Task Get_ThrowawayCertificate_IsServedOverHttp11ToTheSchannelBuild()
    {
        await using var surl = await StartSurlAsync("--self-signed");

        var result = await RunUpstreamCurlAsync("-sS", "-k", "-w", "%{http_version}", surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello.Concat("1.1"u8.ToArray()).ToArray(), result.StandardOutput);
    }

    private Task<SurlOnLoopback> StartSurlAsync(params string[] options) =>
        SurlOnLoopback.StartAsync(
            "https", new Dictionary<string, byte[]> { ["hello.txt"] = Hello }, [], options, TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunUpstreamCurlAsync(params string[] arguments) =>
        PinnedUpstreamCurl.RunAsync(TestContext, arguments);
}
