namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build logs in to a live, in-process <c>surl</c> over <c>http</c>
/// and <c>https</c> with Basic, Digest, Bearer and NTLM, against one account read from a
/// <c>--user-file</c>, and is served or refused as ADR-0032 section 4 says: a plain-text secret
/// over <c>http://</c> is refused with <c>403</c> unless <c>--allow-plaintext-auth</c>, a wrong
/// password gets <c>401</c>, and with no account an anonymous read is served. Every refusal is
/// asserted through <c>-f</c>, which turns the status into curl's exit code 22.
/// Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlLogsInToSurlOverHttpTests
{
    // CURLE_HTTP_RETURNED_ERROR: with -f, a status of 400 or more.
    private const int HttpReturnedError = 22;

    // CURLE_AUTH_ERROR: an authentication function returned an error.
    private const int AuthError = 94;

    private static readonly byte[] Hello = "hello from surl\n"u8.ToArray();

    private static readonly string Credentials = $"{AccountsFile.User}:{AccountsFile.Password}";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task BasicOverHttps_Account_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("https", "--self-signed", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-k", "-u", Credentials, surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
    }

    [TestMethod]
    public async Task BasicOverHttp_Account_IsRefusedWithHttpReturnedError()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("http", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-f", "-u", Credentials, surl.UrlOf("hello.txt"));

        Assert.AreEqual(HttpReturnedError, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "403");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task BasicOverHttp_AllowPlaintextAuth_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("http", "--allow-plaintext-auth", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-f", "-u", Credentials, surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
    }

    [TestMethod]
    public async Task BasicOverHttps_WrongPassword_IsRefusedWithHttpReturnedError()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("https", "--self-signed", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-f", "-k", "-u", $"{AccountsFile.User}:wrong", surl.UrlOf("hello.txt"));

        Assert.AreEqual(HttpReturnedError, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "401");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task DigestOverHttp_Account_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("http", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-f", "--digest", "-u", Credentials, surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
    }

    [TestMethod]
    public async Task DigestOverHttp_WrongPassword_IsRefusedWithHttpReturnedError()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("http", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-f", "--digest", "-u", $"{AccountsFile.User}:wrong", surl.UrlOf("hello.txt"));

        Assert.AreEqual(HttpReturnedError, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "401");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task DigestOverHttpOnSspi_OnlyMd5Offered_ExitsZeroWithTheFilesBytes()
    {
        var result = await RunDigestThroughRelayAsync("MD5");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("SHA-256")]
    [DataRow("SHA-512-256")]
    public async Task DigestOverHttpOnSspi_OnlyShaOffered_ExitsAuthError(string algorithm)
    {
        // Measured 2026-09-29 (ADR-0032, "What upstream curl 8.21.0 does with a Digest
        // challenge"): the Windows reference build does Digest through SSPI, which answers only
        // MD5, and gives up with (94) before sending any Authorization.
        var result = await RunDigestThroughRelayAsync(algorithm);

        Assert.AreEqual(AuthError, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("SHA-256")]
    [DataRow("SHA-512-256")]
    public async Task DigestOverHttp_OnlyShaOffered_ExitsZeroWithTheFilesBytes(string algorithm)
    {
        var result = await RunDigestThroughRelayAsync(algorithm);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
    }

    [TestMethod]
    public async Task BearerOverHttps_ConfiguredToken_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("https", "--self-signed", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-f", "-k", "--oauth2-bearer", AccountsFile.BearerToken, surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
    }

    [TestMethod]
    public async Task BearerOverHttps_OtherToken_IsRefusedWithHttpReturnedError()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("https", "--self-signed", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-f", "-k", "--oauth2-bearer", "another-token", surl.UrlOf("hello.txt"));

        Assert.AreEqual(HttpReturnedError, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "401");
    }

    [TestMethod]
    public async Task NtlmOverHttp_Account_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("http", "--auth", "ntlm", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "--ntlm", "-u", Credentials, surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
    }

    [TestMethod]
    public async Task NtlmOverHttp_WrongPassword_IsRefusedWithHttpReturnedError()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("http", "--auth", "ntlm", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-f", "--ntlm", "-u", $"{AccountsFile.User}:wrong", surl.UrlOf("hello.txt"));

        Assert.AreEqual(HttpReturnedError, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "401");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task NoCredentials_Account_IsRefusedWithHttpReturnedError()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("http", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-f", surl.UrlOf("hello.txt"));

        Assert.AreEqual(HttpReturnedError, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "401");
    }

    [TestMethod]
    public async Task NoCredentials_NoAccount_ExitsZeroWithTheFilesBytes()
    {
        await using var surl = await StartSurlAsync("http");

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", surl.UrlOf("hello.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(Hello, result.StandardOutput);
    }

    // curl --digest through a relay that leaves only surl's Digest challenge for algorithm.
    private async Task<UpstreamCurlRunResult> RunDigestThroughRelayAsync(string algorithm)
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("http", "--user-file", accounts.Path);
        await using var relay = DigestChallengeRelay.Start(surl.BaseUrl, algorithm, TestContext.CancellationToken);

        return await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-f", "--digest", "-u", Credentials, relay.UrlOf("hello.txt"));
    }

    private Task<SurlOnLoopback> StartSurlAsync(string scheme, params string[] options) =>
        SurlOnLoopback.StartAsync(
            scheme, new Dictionary<string, byte[]> { ["hello.txt"] = Hello }, [], options, TestContext.CancellationToken);
}
