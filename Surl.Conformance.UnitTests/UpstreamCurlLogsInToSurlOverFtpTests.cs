namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build logs in to a live, in-process <c>surl ftp://</c> or
/// <c>surl ftps://</c> and downloads a file, each login case ADR-0052 decision 12 lists: curl's
/// anonymous login with and without <c>--allow-anonymous</c>, an account over plaintext refused
/// and accepted with <c>--allow-plaintext-auth</c>, and an account over <c>ftp://</c> with
/// <c>--ssl-reqd</c>, <c>--ftp-ssl-control</c> and <c>--ftp-ssl-ccc</c> and over implicit
/// <c>ftps://</c>, with the exit code curl gives (ADR-0052 decisions 3 and 5). Inconclusive
/// where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlLogsInToSurlOverFtpTests
{
    // CURLE_USE_SSL_FAILED: --ssl-reqd and the server offers no TLS.
    private const int UseSslFailed = 64;

    // CURLE_LOGIN_DENIED: the server answered USER or PASS with 530.
    private const int LoginDenied = 67;

    private static readonly byte[] HelloWorld = "hello world\n"u8.ToArray();

    private static readonly string Account = $"{AccountsFile.User}:{AccountsFile.Password}";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Download_AnonymousLoginWithAllowAnonymous_WritesTheFile()
    {
        await using var surl = await StartSurlAsync("ftp", "--allow-anonymous");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
    }

    [TestMethod]
    public async Task Download_AnonymousLoginWithoutAllowAnonymous_ExitsLoginDenied()
    {
        await using var surl = await StartSurlAsync("ftp");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", surl.UrlOf("a.txt"));

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        Assert.AreEqual("curl: (67) Access denied: 530", result.StandardError.Trim());
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Download_AccountOverPlaintextWithoutAllowPlaintextAuth_ExitsLoginDenied()
    {
        await using var surl = await StartSurlAsync("ftp", "--user", Account);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        Assert.AreEqual("curl: (67) Access denied: 530", result.StandardError.Trim());
    }

    [TestMethod]
    public async Task Download_AccountOverPlaintextWithAllowPlaintextAuth_WritesTheFile()
    {
        await using var surl = await StartSurlAsync("ftp", "--user", Account, "--allow-plaintext-auth");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
    }

    [TestMethod]
    public async Task Download_WrongPasswordWithAllowPlaintextAuth_ExitsLoginDenied()
    {
        await using var surl = await StartSurlAsync("ftp", "--user", Account, "--allow-plaintext-auth");

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-u", $"{AccountsFile.User}:wrong", surl.UrlOf("a.txt"));

        Assert.AreEqual(LoginDenied, result.ExitCode, result.StandardError);
        Assert.AreEqual("curl: (67) Access denied: 530", result.StandardError.Trim());
    }

    [TestMethod]
    [DataRow("--ssl-reqd")]
    [DataRow("--ftp-ssl-control")]
    [DataRow("--ssl-reqd --ftp-ssl-ccc")]
    public async Task Download_AccountOverExplicitTls_WritesTheFile(string tlsOptions)
    {
        await using var surl = await StartSurlAsync("ftp", "--user", Account, "--self-signed");

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, ["-sS", "-k", .. tlsOptions.Split(' '), "-u", Account, surl.UrlOf("a.txt")]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
    }

    [TestMethod]
    public async Task Download_AccountOverImplicitFtps_WritesTheFile()
    {
        await using var surl = await StartSurlAsync("ftps", "--user", Account, "--self-signed");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-k", "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
    }

    [TestMethod]
    public async Task Download_SslReqdWithNoCertificate_ExitsUseSslFailed()
    {
        await using var surl = await StartSurlAsync("ftp", "--user", Account);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "--ssl-reqd", "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(UseSslFailed, result.ExitCode, result.StandardError);
        Assert.AreEqual("curl: (64) Requested SSL level failed", result.StandardError.Trim());
    }

    private Task<SurlOnLoopback> StartSurlAsync(string scheme, params string[] options) =>
        SurlOnLoopback.StartAsync(
            scheme, new Dictionary<string, byte[]> { ["a.txt"] = HelloWorld }, [], options, TestContext.CancellationToken);
}
