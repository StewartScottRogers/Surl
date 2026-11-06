using System.Runtime.CompilerServices;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Phase 7 for HTTP/1.1 and HTTPS (ADR-0089, BL-580): every conversation of
/// <see cref="UpstreamCurlFetchesFromSurlOverHttp11Tests"/>, <see cref="UpstreamCurlFetchesFromSurlOverHttpsTests"/>,
/// <see cref="UpstreamCurlLogsInToSurlOverHttpTests"/>, <see cref="UpstreamCurlTrustsSurlsHostCertificateTests"/> and
/// <see cref="UpstreamCurlLogsInToSurlWithKerberosTests"/>, run through the upstream build the mirrored test runs
/// and through the pinned Curl port, each against its own fresh <c>surl</c>, judged by
/// <see cref="PinnedCurlPortBesideUpstreamCurl"/> and held to <c>Documentation/Conformance/CurlPort/Http.md</c>
/// by <see cref="CurlPortFindingsFile"/>. Each test is named as the test it mirrors (the Kerberos class's
/// Negotiate tests carry a <c>Kerberos</c> prefix, its names colliding with the login class's), and each
/// keeps that test's <c>[OSCondition]</c>. Upstream curl is right; the port is the thing measured
/// (ADR-0003 decision 6). Inconclusive where either pinned binary is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverHttpTests
{
    private const string FindingsFile = "Http.md";

    private const string Http11 = nameof(UpstreamCurlFetchesFromSurlOverHttp11Tests) + ".";

    private const string Https = nameof(UpstreamCurlFetchesFromSurlOverHttpsTests) + ".";

    private const string Logins = nameof(UpstreamCurlLogsInToSurlOverHttpTests) + ".";

    private const string HostCertificates = nameof(UpstreamCurlTrustsSurlsHostCertificateTests) + ".";

    private const string Kerberos = nameof(UpstreamCurlLogsInToSurlWithKerberosTests) + ".";

    // CURLE_HTTP_RETURNED_ERROR: with -f, a status of 400 or more.
    private const int HttpReturnedError = 22;

    // CURLE_PEER_FAILED_VERIFICATION, and for SSH a remote key that was not OK.
    private const int PeerFailedVerification = 60;

    // CURLE_LOGIN_DENIED.
    private const int LoginDenied = 67;

    // CURLE_AUTH_ERROR.
    private const int AuthError = 94;

    // stunnel/static-curl's Windows build of upstream curl's tag 8.21.0 on OpenSSL, unpatched (ADR-0042, ADR-0063).
    private const string StaticCurlWindowsBuildSha256 = "589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648";

    private const string RemoteKeyNotOk = "curl: (60) SSL peer certificate or SSH remote key was not OK";

    private const string WebHost = "web.surl.test";

    private const string MailHost = "mail.surl.test";

    private const string Recipient = "\"tester@SURL.TEST\"@example.com";

    private static readonly byte[] Hello = "hello from surl\n"u8.ToArray();

    private static readonly byte[] SftpFile = "hello world\n"u8.ToArray();

    private static readonly byte[] Mail = "From: a@x\r\nSubject: hi\r\n\r\nhello\r\n"u8.ToArray();

    private static readonly string Credentials = $"{AccountsFile.User}:{AccountsFile.Password}";

    private static readonly string KerberosCredentials = $"{KerberosTestKdcOnLoopback.UserPrincipal}:{KerberosTestKdcOnLoopback.Password}";

    private readonly List<string> temporaryDirectories = [];

    public TestContext TestContext { get; set; } = null!;

    [TestCleanup]
    public void DeleteTemporaryDirectories()
    {
        foreach (var directory in temporaryDirectories.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_File_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_File_ExitsZeroWithTheFilesBytes")]
    public Task Get_File_ExitsZeroWithTheFilesBytes() =>
        MeasureAsync(Case(Http11, "http", [], ["-s", "{surl}/hello.txt"], Expect(0, Hello)));

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_EmptyFile_ExitsZeroWithEmptyOutput"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_EmptyFile_ExitsZeroWithEmptyOutput")]
    public Task Get_EmptyFile_ExitsZeroWithEmptyOutput() =>
        MeasureAsync(Case(Http11, "http", [], ["-s", "{surl}/empty.txt"], Expect(0, [])));

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttp11Tests.Head_File_ExitsZeroWithAnHttp11OkStatusLine"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp11Tests.Head_File_ExitsZeroWithAnHttp11OkStatusLine")]
    public Task Head_File_ExitsZeroWithAnHttp11OkStatusLine() =>
        MeasureAsync(Case(Http11, "http", [], ["-sI", "{surl}/hello.txt"], Expect(0).And(run =>
            (Encoding.ASCII.GetString(run.StandardOutput).StartsWith("HTTP/1.1 200", StringComparison.Ordinal)
                ? null
                : $"the status line is not HTTP/1.1 200: {Encoding.ASCII.GetString(run.StandardOutput)}"))));

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttp11Tests.GetOverHttp10_File_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp11Tests.GetOverHttp10_File_ExitsZeroWithTheFilesBytes")]
    public Task GetOverHttp10_File_ExitsZeroWithTheFilesBytes() =>
        MeasureAsync(Case(Http11, "http", [], ["-s", "-0", "{surl}/hello.txt"], Expect(0, Hello)));

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttp11Tests.GetWithFail_MissingFile_ExitsHttpReturnedError"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp11Tests.GetWithFail_MissingFile_ExitsHttpReturnedError")]
    public Task GetWithFail_MissingFile_ExitsHttpReturnedError() =>
        MeasureAsync(Case(Http11, "http", [], ["-s", "--fail", "{surl}/missing"], Expect(HttpReturnedError)));

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_SameFileTwice_ExitsZeroWithTheFilesBytesTwice"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_SameFileTwice_ExitsZeroWithTheFilesBytesTwice")]
    public Task Get_SameFileTwice_ExitsZeroWithTheFilesBytesTwice() =>
        MeasureAsync(Case(Http11, "http", [], ["-s", "{surl}/hello.txt", "{surl}/hello.txt"], Expect(0, [.. Hello, .. Hello])));

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttpsTests.Get_CertSignedByTheTrustedTestCa_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttpsTests.Get_CertSignedByTheTrustedTestCa_ExitsZeroWithTheFilesBytes")]
    public async Task Get_CertSignedByTheTrustedTestCa_ExitsZeroWithTheFilesBytes()
    {
        using var authority = TestCertificateAuthority.Create();

        await MeasureAsync(Case(
            Https, "https", CertificateOptions(authority),
            ["-sS", "--ssl-no-revoke", "--cacert", authority.CaCertificateFile, "{surl}/hello.txt"], Expect(0, Hello)));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttpsTests.Get_CertSignedByACaNotTrusted_ExitsPeerFailedVerification"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttpsTests.Get_CertSignedByACaNotTrusted_ExitsPeerFailedVerification")]
    public async Task Get_CertSignedByACaNotTrusted_ExitsPeerFailedVerification()
    {
        using var authority = TestCertificateAuthority.Create();

        await MeasureAsync(Case(
            Https, "https", CertificateOptions(authority), ["-sS", "--ssl-no-revoke", "{surl}/hello.txt"], Expect(PeerFailedVerification, [])));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttpsTests.Get_TrustedTestCaWithRevocationChecked_ExitsPeerFailedVerificationOnSchannel"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttpsTests.Get_TrustedTestCaWithRevocationChecked_ExitsPeerFailedVerificationOnSchannel")]
    public async Task Get_TrustedTestCaWithRevocationChecked_ExitsPeerFailedVerificationOnSchannel()
    {
        using var authority = TestCertificateAuthority.Create();

        await MeasureAsync(Case(
            Https, "https", CertificateOptions(authority),
            ["-sS", "--cacert", authority.CaCertificateFile, "{surl}/hello.txt"], Expect(PeerFailedVerification)));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttpsTests.GetWithInsecure_ThrowawayCertificate_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttpsTests.GetWithInsecure_ThrowawayCertificate_ExitsZeroWithTheFilesBytes")]
    public Task GetWithInsecure_ThrowawayCertificate_ExitsZeroWithTheFilesBytes() =>
        MeasureAsync(Case(Https, "https", ["--self-signed"], ["-sS", "-k", "{surl}/hello.txt"], Expect(0, Hello)));

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttpsTests.Get_ThrowawayCertificate_ExitsPeerFailedVerification"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttpsTests.Get_ThrowawayCertificate_ExitsPeerFailedVerification")]
    public Task Get_ThrowawayCertificate_ExitsPeerFailedVerification() =>
        MeasureAsync(Case(Https, "https", ["--self-signed"], ["-sS", "{surl}/hello.txt"], Expect(PeerFailedVerification, [])));

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttpsTests.GetWithInsecure_SameFileTwice_ExitsZeroWithTheFilesBytesTwice"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttpsTests.GetWithInsecure_SameFileTwice_ExitsZeroWithTheFilesBytesTwice")]
    public Task GetWithInsecure_SameFileTwice_ExitsZeroWithTheFilesBytesTwice() =>
        MeasureAsync(Case(
            Https, "https", ["--self-signed"], ["-sS", "-k", "{surl}/hello.txt", "{surl}/hello.txt"], Expect(0, [.. Hello, .. Hello])));

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttpsTests.GetWithHttp11_ThrowawayCertificate_IsServedOverHttp11"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttpsTests.GetWithHttp11_ThrowawayCertificate_IsServedOverHttp11")]
    public Task GetWithHttp11_ThrowawayCertificate_IsServedOverHttp11() =>
        MeasureAsync(Case(
            Https, "https", ["--self-signed"], ["-sS", "-k", "--http1.1", "-w", "%{http_version}", "{surl}/hello.txt"],
            Expect(0, [.. Hello, .. "1.1"u8])));

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttpsTests.Get_ThrowawayCertificate_IsServedOverHttp2"/>.</summary>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttpsTests.Get_ThrowawayCertificate_IsServedOverHttp2")]
    public Task Get_ThrowawayCertificate_IsServedOverHttp2() =>
        MeasureAsync(Case(
            Https, "https", ["--self-signed"], ["-sS", "-k", "-w", "%{http_version}", "{surl}/hello.txt", "{surl}/hello.txt"],
            Expect(0, [.. Hello, .. "2"u8, .. Hello, .. "2"u8])));

    /// <summary>Mirrors <see cref="UpstreamCurlFetchesFromSurlOverHttpsTests.Get_ThrowawayCertificate_IsServedOverHttp11ToTheSchannelBuild"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlFetchesFromSurlOverHttpsTests.Get_ThrowawayCertificate_IsServedOverHttp11ToTheSchannelBuild")]
    public Task Get_ThrowawayCertificate_IsServedOverHttp11ToTheSchannelBuild() =>
        MeasureAsync(Case(
            Https, "https", ["--self-signed"], ["-sS", "-k", "-w", "%{http_version}", "{surl}/hello.txt"], Expect(0, [.. Hello, .. "1.1"u8])));

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.BasicOverHttps_Account_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.BasicOverHttps_Account_ExitsZeroWithTheFilesBytes")]
    public async Task BasicOverHttps_Account_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            Logins, "https", ["--self-signed", "--user-file", accounts.Path], ["-sS", "-k", "-u", Credentials, "{surl}/hello.txt"], Expect(0, Hello)));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.BasicOverHttp_Account_IsRefusedWithHttpReturnedError"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.BasicOverHttp_Account_IsRefusedWithHttpReturnedError")]
    public async Task BasicOverHttp_Account_IsRefusedWithHttpReturnedError()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            Logins, "http", ["--user-file", accounts.Path], ["-sS", "-f", "-u", Credentials, "{surl}/hello.txt"],
            Expect(HttpReturnedError, [], "403")));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.BasicOverHttp_AllowPlaintextAuth_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.BasicOverHttp_AllowPlaintextAuth_ExitsZeroWithTheFilesBytes")]
    public async Task BasicOverHttp_AllowPlaintextAuth_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            Logins, "http", ["--allow-plaintext-auth", "--user-file", accounts.Path], ["-sS", "-f", "-u", Credentials, "{surl}/hello.txt"],
            Expect(0, Hello)));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.BasicOverHttps_WrongPassword_IsRefusedWithHttpReturnedError"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.BasicOverHttps_WrongPassword_IsRefusedWithHttpReturnedError")]
    public async Task BasicOverHttps_WrongPassword_IsRefusedWithHttpReturnedError()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            Logins, "https", ["--self-signed", "--user-file", accounts.Path],
            ["-sS", "-f", "-k", "-u", $"{AccountsFile.User}:wrong", "{surl}/hello.txt"], Expect(HttpReturnedError, [], "401")));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.DigestOverHttp_Account_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.DigestOverHttp_Account_ExitsZeroWithTheFilesBytes")]
    public async Task DigestOverHttp_Account_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            Logins, "http", ["--user-file", accounts.Path], ["-sS", "-f", "--digest", "-u", Credentials, "{surl}/hello.txt"], Expect(0, Hello)));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.DigestOverHttp_WrongPassword_IsRefusedWithHttpReturnedError"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.DigestOverHttp_WrongPassword_IsRefusedWithHttpReturnedError")]
    public async Task DigestOverHttp_WrongPassword_IsRefusedWithHttpReturnedError()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            Logins, "http", ["--user-file", accounts.Path],
            ["-sS", "-f", "--digest", "-u", $"{AccountsFile.User}:wrong", "{surl}/hello.txt"], Expect(HttpReturnedError, [], "401")));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.AwsSigV4OverHttp_Account_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.AwsSigV4OverHttp_Account_ExitsZeroWithTheFilesBytes")]
    public async Task AwsSigV4OverHttp_Account_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(SignedByTheClock(Case(
            Logins, "http", ["--user-file", accounts.Path],
            ["-sS", "--aws-sigv4", "aws:amz:us-east-1:s3", "-u", Credentials, "{surl}/hello.txt"], Expect(0, Hello))));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.AwsSigV4UploadOverHttp_ExitsZeroJudgedOverTheBody"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.AwsSigV4UploadOverHttp_ExitsZeroJudgedOverTheBody")]
    [DataRow("ec2", "secret", "--data-binary", "201", DisplayName = "ec2, --data-binary: signed over the body, stored")]
    [DataRow("ec2", "wrong", "--data-binary", "401", DisplayName = "ec2, --data-binary, wrong secret: refused once the body is read")]
    [DataRow("s3", "secret", "-T", "201", DisplayName = "s3, -T: curl sends UNSIGNED-PAYLOAD, stored")]
    [DataRow("ec2", "secret", "-T", "401", DisplayName = "ec2, -T: curl signs the empty body's hash, so the body sent is not the one signed")]
    public async Task AwsSigV4UploadOverHttp_ExitsZeroJudgedOverTheBody(string service, string secret, string uploadOption, string expectedStatus)
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        var upload = Path.Combine(NewTemporaryDirectory(), "body.txt");
        await File.WriteAllBytesAsync(upload, "body"u8.ToArray(), TestContext.CancellationToken);
        string[] uploadArguments = uploadOption == "-T" ? ["-T", upload] : ["-X", "PUT", "--data-binary", "@" + upload];

        await MeasureAsync(SignedByTheClock(Case(
            Logins,
            "http",
            ["--allow-uploads", "--user-file", accounts.Path],
            ["-sS", "--aws-sigv4", $"aws:amz:us-east-1:{service}", "-u", $"{AccountsFile.User}:{secret}", .. uploadArguments, "-w", "%{http_code}", "{surl}/upload"],
            Expect(0, Encoding.ASCII.GetBytes(expectedStatus)),
            $"({service}, {secret}, {uploadOption})")));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.AwsSigV4OverHttp_WrongSecret_ExitsZeroWithNoBody"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.AwsSigV4OverHttp_WrongSecret_ExitsZeroWithNoBody")]
    public async Task AwsSigV4OverHttp_WrongSecret_ExitsZeroWithNoBody()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(SignedByTheClock(Case(
            Logins, "http", ["--user-file", accounts.Path],
            ["-sS", "--aws-sigv4", "aws:amz:us-east-1:s3", "-u", $"{AccountsFile.User}:wrong", "{surl}/hello.txt"], Expect(0, []))));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.AwsSigV4OverHttp_WrongSecret_IsRefusedWithHttpReturnedError"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.AwsSigV4OverHttp_WrongSecret_IsRefusedWithHttpReturnedError")]
    public async Task AwsSigV4OverHttp_WrongSecret_IsRefusedWithHttpReturnedError()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(SignedByTheClock(Case(
            Logins, "http", ["--user-file", accounts.Path],
            ["-sS", "-f", "--aws-sigv4", "aws:amz:us-east-1:s3", "-u", $"{AccountsFile.User}:wrong", "{surl}/hello.txt"],
            Expect(HttpReturnedError, [], "401"))));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.DigestOverHttpOnSspi_OnlyMd5Offered_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.DigestOverHttpOnSspi_OnlyMd5Offered_ExitsZeroWithTheFilesBytes")]
    public Task DigestOverHttpOnSspi_OnlyMd5Offered_ExitsZeroWithTheFilesBytes() =>
        MeasureDigestThroughRelayAsync("MD5", Expect(0, Hello), string.Empty);

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.DigestOverHttpOnSspi_OnlyShaOffered_ExitsAuthError"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.DigestOverHttpOnSspi_OnlyShaOffered_ExitsAuthError")]
    [DataRow("SHA-256")]
    [DataRow("SHA-512-256")]
    public Task DigestOverHttpOnSspi_OnlyShaOffered_ExitsAuthError(string algorithm) =>
        MeasureDigestThroughRelayAsync(algorithm, Expect(AuthError, []), $"({algorithm})");

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.DigestOverHttp_OnlyShaOffered_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.DigestOverHttp_OnlyShaOffered_ExitsZeroWithTheFilesBytes")]
    [DataRow("SHA-256")]
    [DataRow("SHA-512-256")]
    public Task DigestOverHttp_OnlyShaOffered_ExitsZeroWithTheFilesBytes(string algorithm) =>
        MeasureDigestThroughRelayAsync(algorithm, Expect(0, Hello), $"({algorithm})");

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.BearerOverHttps_ConfiguredToken_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.BearerOverHttps_ConfiguredToken_ExitsZeroWithTheFilesBytes")]
    public async Task BearerOverHttps_ConfiguredToken_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            Logins, "https", ["--self-signed", "--user-file", accounts.Path],
            ["-sS", "-f", "-k", "--oauth2-bearer", AccountsFile.BearerToken, "{surl}/hello.txt"], Expect(0, Hello)));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.BearerOverHttps_OtherToken_IsRefusedWithHttpReturnedError"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.BearerOverHttps_OtherToken_IsRefusedWithHttpReturnedError")]
    public async Task BearerOverHttps_OtherToken_IsRefusedWithHttpReturnedError()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            Logins, "https", ["--self-signed", "--user-file", accounts.Path],
            ["-sS", "-f", "-k", "--oauth2-bearer", "another-token", "{surl}/hello.txt"], Expect(HttpReturnedError, null, "401")));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.NtlmOverHttp_Account_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.NtlmOverHttp_Account_ExitsZeroWithTheFilesBytes")]
    public async Task NtlmOverHttp_Account_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            Logins, "http", ["--auth", "ntlm", "--user-file", accounts.Path], ["-sS", "--ntlm", "-u", Credentials, "{surl}/hello.txt"], Expect(0, Hello)));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.NtlmOverHttp_WrongPassword_IsRefusedWithHttpReturnedError"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.NtlmOverHttp_WrongPassword_IsRefusedWithHttpReturnedError")]
    public async Task NtlmOverHttp_WrongPassword_IsRefusedWithHttpReturnedError()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            Logins, "http", ["--auth", "ntlm", "--user-file", accounts.Path],
            ["-sS", "-f", "--ntlm", "-u", $"{AccountsFile.User}:wrong", "{surl}/hello.txt"], Expect(HttpReturnedError, [], "401")));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.NegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.NegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes")]
    public async Task NegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(
            Case(Logins, "http", ["--auth", "negotiate", "--user-file", accounts.Path], ["-sS", "--negotiate", "-u", Credentials, "{surl}/hello.txt"], Expect(0, Hello))
                with
            { RecordsClientBytes = false },
            new CurlPortRunSetup { UpstreamBuildSha256 = StaticCurlWindowsBuildSha256 });
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.NegotiateOverHttp_WrongPassword_IsRefusedWithHttpReturnedError"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.NegotiateOverHttp_WrongPassword_IsRefusedWithHttpReturnedError")]
    public async Task NegotiateOverHttp_WrongPassword_IsRefusedWithHttpReturnedError()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(
            Case(
                Logins, "http", ["--auth", "negotiate", "--user-file", accounts.Path],
                ["-sS", "-f", "--negotiate", "-u", $"{AccountsFile.User}:wrong", "{surl}/hello.txt"], Expect(HttpReturnedError, [], "401"))
                with
            { RecordsClientBytes = false },
            new CurlPortRunSetup { UpstreamBuildSha256 = StaticCurlWindowsBuildSha256 });
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.NegotiateOverHttp_WindowsReferenceBuild_SendsNoTokenAndIsRefused"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.NegotiateOverHttp_WindowsReferenceBuild_SendsNoTokenAndIsRefused")]
    public async Task NegotiateOverHttp_WindowsReferenceBuild_SendsNoTokenAndIsRefused()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            Logins, "http", ["--auth", "negotiate", "--user-file", accounts.Path],
            ["-sS", "-f", "--negotiate", "-u", Credentials, "{surl}/hello.txt"], Expect(HttpReturnedError, [], "SEC_E_NO_CREDENTIALS")));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.NoCredentials_Account_IsRefusedWithHttpReturnedError"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.NoCredentials_Account_IsRefusedWithHttpReturnedError")]
    public async Task NoCredentials_Account_IsRefusedWithHttpReturnedError()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(Case(
            Logins, "http", ["--user-file", accounts.Path], ["-sS", "-f", "{surl}/hello.txt"], Expect(HttpReturnedError, null, "401")));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlOverHttpTests.NoCredentials_NoAccount_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverHttpTests.NoCredentials_NoAccount_ExitsZeroWithTheFilesBytes")]
    public Task NoCredentials_NoAccount_ExitsZeroWithTheFilesBytes() =>
        MeasureAsync(Case(Logins, "http", [], ["-sS", "{surl}/hello.txt"], Expect(0, Hello)));

    /// <summary>Mirrors <see cref="UpstreamCurlTrustsSurlsHostCertificateTests.Download_RsaHostCertificateOnTheWindowsBuild_AgreesThePlainKeyAndExits60"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlTrustsSurlsHostCertificateTests.Download_RsaHostCertificateOnTheWindowsBuild_AgreesThePlainKeyAndExits60")]
    public Task Download_RsaHostCertificateOnTheWindowsBuild_AgreesThePlainKeyAndExits60() =>
        MeasureHostCertificateRefusedAsync(
            SshTestHostCertificates.RsaHostKey, SshTestHostCertificates.RsaHostCertificate, "rsa-sha2-512", CurlPortRunSetup.None);

    /// <summary>Mirrors <see cref="UpstreamCurlTrustsSurlsHostCertificateTests.Download_Ed25519HostCertificateOnTheOpenSslWindowsBuild_AgreesThePlainKeyAndExits60"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlTrustsSurlsHostCertificateTests.Download_Ed25519HostCertificateOnTheOpenSslWindowsBuild_AgreesThePlainKeyAndExits60")]
    public Task Download_Ed25519HostCertificateOnTheOpenSslWindowsBuild_AgreesThePlainKeyAndExits60() =>
        MeasureHostCertificateRefusedAsync(
            SshTestHostCertificates.Ed25519HostKey,
            SshTestHostCertificates.Ed25519HostCertificate,
            "ssh-ed25519",
            new CurlPortRunSetup { UpstreamBuildSha256 = StaticCurlWindowsBuildSha256 });

    /// <summary>Mirrors <see cref="UpstreamCurlTrustsSurlsHostCertificateTests.Download_Ed25519HostCertificateOnOpenSslBuilds_AgreesThePlainKeyAndExits60"/>.</summary>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlTrustsSurlsHostCertificateTests.Download_Ed25519HostCertificateOnOpenSslBuilds_AgreesThePlainKeyAndExits60")]
    public Task Download_Ed25519HostCertificateOnOpenSslBuilds_AgreesThePlainKeyAndExits60() =>
        MeasureHostCertificateRefusedAsync(
            SshTestHostCertificates.Ed25519HostKey, SshTestHostCertificates.Ed25519HostCertificate, "ssh-ed25519", CurlPortRunSetup.None);

    /// <summary>Mirrors <see cref="UpstreamCurlTrustsSurlsHostCertificateTests.Download_RsaHostCertificateWithThePlainKeyPinned_DownloadsTheFile"/>.</summary>
    [TestMethod]
    [Description("Mirrors UpstreamCurlTrustsSurlsHostCertificateTests.Download_RsaHostCertificateWithThePlainKeyPinned_DownloadsTheFile")]
    public async Task Download_RsaHostCertificateWithThePlainKeyPinned_DownloadsTheFile()
    {
        var measurementCase = await HostCertificateCaseAsync(
            SshTestHostCertificates.RsaHostKey,
            SshTestHostCertificates.RsaHostCertificate,
            ["-sS", "--hostpubsha256", "<surl's ssh-rsa host key SHA-256>", "-u", Credentials, "{surl}/a.txt"],
            Expect(0, SftpFile));

        await MeasureAsync(measurementCase, new CurlPortRunSetup
        {
            ComparedNotes = CurlPortRunSetup.WithoutPacketDumps,
            ArgumentsForRunAsync = (surl, baseUrl, _) => Task.FromResult<IReadOnlyList<string>>(
                ["-sS", "--hostpubsha256", SshLogNotes.HostKeySha256(surl.Log, "ssh-rsa"), "-u", Credentials, new Uri(baseUrl, "a.txt").AbsoluteUri]),
        });
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlWithKerberosTests.NegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DoNotParallelize]
    [Description("Mirrors UpstreamCurlLogsInToSurlWithKerberosTests.NegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes")]
    public async Task KerberosNegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes()
    {
        await using var kdc = await KerberosTestKdcOnLoopback.StartAsync([$"HTTP/{WebHost}"], TestContext.CancellationToken);

        await MeasureKerberosNegotiateAsync(kdc, await WriteKerberosAccountsFileAsync(), Expect(0, Hello));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlWithKerberosTests.NegotiateOverHttp_NoAccount_IsRefusedWithHttpReturnedError"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DoNotParallelize]
    [Description("Mirrors UpstreamCurlLogsInToSurlWithKerberosTests.NegotiateOverHttp_NoAccount_IsRefusedWithHttpReturnedError")]
    public async Task KerberosNegotiateOverHttp_NoAccount_IsRefusedWithHttpReturnedError()
    {
        await using var kdc = await KerberosTestKdcOnLoopback.StartAsync([$"HTTP/{WebHost}"], TestContext.CancellationToken);
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureKerberosNegotiateAsync(kdc, accounts.Path, Expect(HttpReturnedError, [], "401"));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverSmtp_Account_StoresTheMessageInTheAccountsInbox"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DoNotParallelize]
    [Description("Mirrors UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverSmtp_Account_StoresTheMessageInTheAccountsInbox")]
    [DataRow(false, DisplayName = "without --sasl-ir")]
    [DataRow(true, DisplayName = "with --sasl-ir")]
    public async Task GssapiOverSmtp_Account_StoresTheMessageInTheAccountsInbox(bool initialResponse)
    {
        await using var kdc = await KerberosTestKdcOnLoopback.StartAsync([$"smtp/{MailHost}"], TestContext.CancellationToken);
        var accounts = await WriteKerberosAccountsFileAsync();
        var inboxes = new List<IReadOnlyList<byte[]>>();

        await MeasureSendMailAsync(kdc, accounts, initialResponse, inboxes, Expect(0).And(run =>
            (inboxes[0].Count == 1 && inboxes[0][0].AsSpan().EndsWith(Mail)
                ? null
                : $"the inbox holds {inboxes[0].Count} messages, not the one sent")));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverSmtp_NoAccount_ExitsLoginDenied"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DoNotParallelize]
    [Description("Mirrors UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverSmtp_NoAccount_ExitsLoginDenied")]
    public async Task GssapiOverSmtp_NoAccount_ExitsLoginDenied()
    {
        await using var kdc = await KerberosTestKdcOnLoopback.StartAsync([$"smtp/{MailHost}"], TestContext.CancellationToken);
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureSendMailAsync(kdc, accounts.Path, initialResponse: false, [], Expect(LoginDenied));
    }

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverImap_Account_FetchesTheDeliveredMessage"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DoNotParallelize]
    [Description("Mirrors UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverImap_Account_FetchesTheDeliveredMessage")]
    [DataRow(false, DisplayName = "without --sasl-ir")]
    [DataRow(true, DisplayName = "with --sasl-ir")]
    public Task GssapiOverImap_Account_FetchesTheDeliveredMessage(bool initialResponse) =>
        MeasureFetchDeliveredMessageAsync("imap", "/INBOX;UID=1", initialResponse);

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverImap_NoAccount_ExitsLoginDenied"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DoNotParallelize]
    [Description("Mirrors UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverImap_NoAccount_ExitsLoginDenied")]
    public Task GssapiOverImap_NoAccount_ExitsLoginDenied() =>
        MeasureReadWithoutKerberosAccountAsync("imap", "/INBOX;UID=1");

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverPop3_Account_RetrievesTheDeliveredMessage"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DoNotParallelize]
    [Description("Mirrors UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverPop3_Account_RetrievesTheDeliveredMessage")]
    [DataRow(false, DisplayName = "without --sasl-ir")]
    [DataRow(true, DisplayName = "with --sasl-ir")]
    public Task GssapiOverPop3_Account_RetrievesTheDeliveredMessage(bool initialResponse) =>
        MeasureFetchDeliveredMessageAsync("pop3", "/1", initialResponse);

    /// <summary>Mirrors <see cref="UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverPop3_NoAccount_ExitsLoginDenied"/>.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DoNotParallelize]
    [Description("Mirrors UpstreamCurlLogsInToSurlWithKerberosTests.GssapiOverPop3_NoAccount_ExitsLoginDenied")]
    public Task GssapiOverPop3_NoAccount_ExitsLoginDenied() =>
        MeasureReadWithoutKerberosAccountAsync("pop3", "/1");

    private static Expectation Expect(int exitCode, byte[]? output = null, string? standardErrorContains = null) =>
        new(exitCode, output, standardErrorContains);

    private static string[] CertificateOptions(TestCertificateAuthority authority) =>
        ["--cert", authority.ServerCertificateFile, "--key", authority.ServerKeyFile];

    // An AWS Signature Version 4 request carries x-amz-date and a signature over it, so two runs a
    // second apart send different bytes for a reason that is the clock's, not curl's (ADR-0089
    // decision 4: timing is never compared). Exit code, output and surl's notes still are.
    private static CurlPortMeasurementCase SignedByTheClock(CurlPortMeasurementCase measurementCase) =>
        measurementCase with { RecordsClientBytes = false };

    private static CurlPortMeasurementCase Case(
        string mirroredClass,
        string scheme,
        IReadOnlyList<string> surlOptions,
        IReadOnlyList<string> arguments,
        Expectation mirroredAssertions,
        string row = "",
        [CallerMemberName] string testName = "") =>
        new(
            testName + row,
            mirroredClass + (testName.StartsWith("KerberosNegotiate", StringComparison.Ordinal) ? testName["Kerberos".Length..] : testName),
            scheme,
            arguments,
            scheme,
            surlOptions,
            new Dictionary<string, byte[]> { ["hello.txt"] = Hello, ["empty.txt"] = [], ["a.txt"] = SftpFile },
            mirroredAssertions.Check)
        {
            AssertedStandardErrorFragments = mirroredAssertions.StandardErrorContains is { } fragment ? [fragment] : [],
        };

    private async Task<PinnedCurlPortMeasurement> MeasureAsync(CurlPortMeasurementCase measurementCase, CurlPortRunSetup? setup = null)
    {
        setup ??= CurlPortRunSetup.None;
        if (measurementCase.SurlScheme == "https" && setup.ComparedNotes is null)
        {
            setup = setup with { ComparedNotes = CurlPortRunSetup.WithoutTlsEndings };
        }

        var measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(TestContext, measurementCase, setup);
        await CurlPortFindingsFile.AssertVerdictIsListedAsync(TestContext, FindingsFile, measurementCase.Name, measurement);
        return measurement;
    }

    // curl --digest through a relay that leaves only surl's Digest challenge for algorithm.
    private async Task MeasureDigestThroughRelayAsync(
        string algorithm, Expectation checkMirroredAssertions, string row, [CallerMemberName] string testName = "")
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureAsync(
            Case(Logins, "http", ["--user-file", accounts.Path], ["-sS", "-f", "--digest", "-u", Credentials, "{surl}/hello.txt"], checkMirroredAssertions, row, testName),
            new CurlPortRunSetup
            {
                StartFrontRelay = (listener, cancellationToken) =>
                {
                    var relay = DigestChallengeRelay.Start(listener, algorithm, cancellationToken);
                    return (relay, new Uri($"http://127.0.0.1:{relay.Port}/"));
                },
            });
    }

    private async Task<CurlPortMeasurementCase> HostCertificateCaseAsync(
        string hostKeyText,
        string certificateText,
        IReadOnlyList<string> arguments,
        Expectation checkMirroredAssertions,
        [CallerMemberName] string testName = "")
    {
        var directory = NewTemporaryDirectory();
        var hostKey = Path.Combine(directory, "host_key");
        var certificate = Path.Combine(directory, "host_key-cert.pub");
        await File.WriteAllTextAsync(hostKey, hostKeyText, TestContext.CancellationToken);
        await File.WriteAllTextAsync(certificate, certificateText, TestContext.CancellationToken);
        return Case(
            HostCertificates,
            "sftp",
            ["-v", "--hostkey", hostKey, "--hostcert", certificate, "--user", Credentials],
            arguments,
            checkMirroredAssertions,
            testName: testName);
    }

    // Each run writes a known_hosts naming its own surl's port with an @cert-authority line, which
    // libssh2 does not read, so both clients should refuse the host key with (60) (ADR-0068).
    private async Task MeasureHostCertificateRefusedAsync(
        string hostKeyText, string certificateText, string plainHostKeyAlgorithm, CurlPortRunSetup setup, [CallerMemberName] string testName = "")
    {
        var knownHosts = Path.Combine(NewTemporaryDirectory(), "known_hosts");
        var measurementCase = await HostCertificateCaseAsync(
            hostKeyText,
            certificateText,
            ["-sS", "--knownhosts", knownHosts, "-u", Credentials, "{surl}/a.txt"],
            Expect(PeerFailedVerification, [], RemoteKeyNotOk).And(run =>
                run.StandardError.Split('\n')[0].TrimEnd('\r') == RemoteKeyNotOk ? null : $"stderr does not start {RemoteKeyNotOk}: {run.StandardError}"),
            testName);

        var measurement = await MeasureAsync(
            measurementCase,
            setup with
            {
                ComparedNotes = CurlPortRunSetup.WithoutPacketDumps,
                ArgumentsForRunAsync = async (surl, baseUrl, cancellationToken) =>
                {
                    await File.WriteAllTextAsync(
                        knownHosts,
                        $"@cert-authority [127.0.0.1]:{baseUrl.Port} {SshTestHostCertificates.CertificateAuthorityPublicKey}\n",
                        cancellationToken);
                    return measurementCase.ArgumentsFor(baseUrl);
                },
            });

        StringAssert.Contains(
            string.Join('\n', SshLogNotes.NegotiatedAlgorithms(measurement.UpstreamSurl.Notes)), $"host key {plainHostKeyAlgorithm},");
    }

    private async Task MeasureKerberosNegotiateAsync(
        KerberosTestKdcOnLoopback kdc, string accountsPath, Expectation checkMirroredAssertions, [CallerMemberName] string testName = "")
    {
        // Kerberos tokens carry a fresh session key and time stamp on every run, so their request
        // bytes are not curl's to compare, as TLS bytes are not (ADR-0089 decision 4).
        var measurementCase = Case(
            Kerberos,
            "http",
            ["--auth", "negotiate", "--keytab", kdc.KeytabPath, "--user-file", accountsPath],
            ["-sS", "-f", "--negotiate", "-u", KerberosCredentials, "--resolve", $"{WebHost}:<port>:127.0.0.1", $"http://{WebHost}:<port>/hello.txt"],
            checkMirroredAssertions,
            testName: testName) with
        { RecordsClientBytes = false };

        await MeasureAsync(measurementCase, new CurlPortRunSetup
        {
            ArgumentsForRunAsync = (_, baseUrl, _) => Task.FromResult<IReadOnlyList<string>>(
                ["-sS", "-f", "--negotiate", "-u", KerberosCredentials, "--resolve", $"{WebHost}:{baseUrl.Port}:127.0.0.1", $"http://{WebHost}:{baseUrl.Port}/hello.txt"]),
        });
    }

    // Sends mail.txt over smtp with GSSAPI to a surl over a fresh data directory per run, and
    // compares each run's inbox once that surl has stopped. The URL ends /c so curl sends EHLO c.
    private async Task MeasureSendMailAsync(
        KerberosTestKdcOnLoopback kdc,
        string accountsPath,
        bool initialResponse,
        List<IReadOnlyList<byte[]>> inboxes,
        Expectation checkMirroredAssertions,
        [CallerMemberName] string testName = "")
    {
        var mail = Path.Combine(NewTemporaryDirectory(), "mail.txt");
        await File.WriteAllBytesAsync(mail, Mail, TestContext.CancellationToken);
        var dataDirectories = new List<string>();
        var measurementCase = Case(
            Kerberos,
            "smtp",
            [],
            SendMailArguments(mail, initialResponse, "<port>"),
            checkMirroredAssertions,
            initialResponse ? "(with --sasl-ir)" : string.Empty,
            testName) with
        {
            RecordsClientBytes = false,
            ComparedStoredFiles = ["INBOX message 1, after its trace fields", "INBOX message 2, after its trace fields"],
        };

        await MeasureAsync(measurementCase, new CurlPortRunSetup
        {
            StartSurlAsync = cancellationToken =>
            {
                dataDirectories.Add(NewTemporaryDirectory());
                return StartMailSurlAsync(["smtp"], dataDirectories[^1], kdc, accountsPath, cancellationToken);
            },
            ArgumentsForRunAsync = (_, baseUrl, _) => Task.FromResult<IReadOnlyList<string>>(
                SendMailArguments(mail, initialResponse, baseUrl.Port.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            ReadStoredFilesAfterStopAsync = async cancellationToken =>
            {
                var inbox = await StoredMail.ReadInboxAsync(dataDirectories[^1], KerberosTestKdcOnLoopback.UserPrincipal, cancellationToken);
                inboxes.Add(inbox);
                return inbox.Select((message, index) => (message, index)).ToDictionary(
                    entry => $"INBOX message {entry.index + 1}, after its trace fields",
                    entry => entry.message.AsSpan().EndsWith(Mail) ? Mail : entry.message);
            },
        });
    }

    // Delivers mail.txt once over smtp with upstream curl into a template data directory, then
    // reads it back over mailScheme with GSSAPI from a surl over a copy of that directory per run,
    // so both clients read the very same stored message, trace fields included.
    private async Task MeasureFetchDeliveredMessageAsync(
        string mailScheme, string path, bool initialResponse, [CallerMemberName] string testName = "")
    {
        await using var kdc = await KerberosTestKdcOnLoopback.StartAsync(
            [$"smtp/{MailHost}", $"{ServiceOf(mailScheme)}/{MailHost}"], TestContext.CancellationToken);
        var accounts = await WriteKerberosAccountsFileAsync();
        var template = NewTemporaryDirectory();
        await using (var surl = await StartMailSurlAsync(["smtp"], template, kdc, accounts, TestContext.CancellationToken))
        {
            var mail = Path.Combine(NewTemporaryDirectory(), "mail.txt");
            await File.WriteAllBytesAsync(mail, Mail, TestContext.CancellationToken);
            var smtpPort = surl.BaseUrlOf("smtp").Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var delivery = await PinnedUpstreamCurl.RunAsync(TestContext, SendMailArguments(mail, initialResponse: false, smtpPort));
            Assert.AreEqual(0, delivery.ExitCode, $"The SMTP delivery failed: {delivery.StandardError}");
        }

        var stored = await StoredMail.ReadInboxAsync(template, KerberosTestKdcOnLoopback.UserPrincipal, TestContext.CancellationToken);
        await MeasureReadMailAsync(
            kdc,
            accounts,
            template,
            mailScheme,
            path,
            initialResponse,
            Expect(0).And(run => (stored.Count == 1 && run.StandardOutput.AsSpan().SequenceEqual(stored[0])
                ? null
                : $"wrote {run.StandardOutput.Length} bytes, not the {stored.Count} stored message(s)")),
            testName);
    }

    // surl has only AccountsFile's tester, not tester@SURL.TEST, so the ticket names no account.
    private async Task MeasureReadWithoutKerberosAccountAsync(string mailScheme, string path, [CallerMemberName] string testName = "")
    {
        await using var kdc = await KerberosTestKdcOnLoopback.StartAsync([$"{ServiceOf(mailScheme)}/{MailHost}"], TestContext.CancellationToken);
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await MeasureReadMailAsync(kdc, accounts.Path, NewTemporaryDirectory(), mailScheme, path, initialResponse: false, Expect(LoginDenied, []), testName);
    }

    private async Task MeasureReadMailAsync(
        KerberosTestKdcOnLoopback kdc,
        string accountsPath,
        string template,
        string mailScheme,
        string path,
        bool initialResponse,
        Expectation checkMirroredAssertions,
        string testName)
    {
        var measurementCase = Case(
            Kerberos,
            mailScheme,
            [],
            ReadMailArguments(mailScheme, path, initialResponse, "<port>"),
            checkMirroredAssertions,
            initialResponse ? "(with --sasl-ir)" : string.Empty,
            testName) with
        { RecordsClientBytes = false };

        await MeasureAsync(measurementCase, new CurlPortRunSetup
        {
            StartSurlAsync = cancellationToken =>
            {
                var dataDirectory = NewTemporaryDirectory();
                CopyDirectory(template, dataDirectory);
                return StartMailSurlAsync([mailScheme], dataDirectory, kdc, accountsPath, cancellationToken);
            },
            ArgumentsForRunAsync = (_, baseUrl, _) => Task.FromResult<IReadOnlyList<string>>(
                ReadMailArguments(mailScheme, path, initialResponse, baseUrl.Port.ToString(System.Globalization.CultureInfo.InvariantCulture))),
        });
    }

    private static string[] SendMailArguments(string mail, bool initialResponse, string port) =>
        [
            "-sS", "-u", KerberosCredentials, "--login-options", "AUTH=GSSAPI", .. InitialResponseOption(initialResponse),
            "--resolve", $"{MailHost}:{port}:127.0.0.1", "--mail-from", "a@x", "--mail-rcpt", Recipient, "-T", mail,
            $"smtp://{MailHost}:{port}/c",
        ];

    private static string[] ReadMailArguments(string mailScheme, string path, bool initialResponse, string port) =>
        [
            "-sS", "-u", KerberosCredentials, "--login-options", "AUTH=GSSAPI", .. InitialResponseOption(initialResponse),
            "--resolve", $"{MailHost}:{port}:127.0.0.1", $"{mailScheme}://{MailHost}:{port}{path}",
        ];

    private static string[] InitialResponseOption(bool initialResponse) => initialResponse ? ["--sasl-ir"] : [];

    // ADR-0057 decision 2: the service each mail scheme's tickets are for.
    private static string ServiceOf(string mailScheme) => mailScheme == "pop3" ? "pop" : mailScheme;

    private static Task<SurlOnLoopback> StartMailSurlAsync(
        string[] schemes, string dataDirectory, KerberosTestKdcOnLoopback kdc, string accountsPath, CancellationToken cancellationToken) =>
        SurlOnLoopback.StartOverDirectoryAsync(
            schemes, dataDirectory, ["--auth", "gssapi", "--keytab", kdc.KeytabPath, "--user-file", accountsPath], cancellationToken);

    // A --user-file whose one account is the KDC's user principal; its password is never used.
    private async Task<string> WriteKerberosAccountsFileAsync()
    {
        var path = Path.Combine(NewTemporaryDirectory(), "users.txt");
        await File.WriteAllTextAsync(path, $"{KerberosTestKdcOnLoopback.UserPrincipal}:unused\n", TestContext.CancellationToken);
        return path;
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
        }
    }

    private string NewTemporaryDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("surl-phase7-http-").FullName;
        temporaryDirectories.Add(directory);
        return directory;
    }
}

/// <summary>
/// A Phase 7 test's copy of its mirrored test's assertions on upstream curl's run: the exit code, the
/// whole standard output when given, a fragment of standard error when given (whose lines are then also
/// compared with the port's, ADR-0089 decision 4), and any further assertion.
/// </summary>
internal sealed record Expectation(int ExitCode, byte[]? Output, string? StandardErrorContains)
{
    private Func<CurlPortMeasuredRun, string?>? further;

    /// <summary>Gets this expectation with <paramref name="furtherAssertion"/> checked after the rest.</summary>
    public Expectation And(Func<CurlPortMeasuredRun, string?> furtherAssertion) => this with { further = furtherAssertion };

    /// <summary>Gets what in <paramref name="run"/> the mirrored test would not accept, or <see langword="null"/>.</summary>
    public string? Check(CurlPortMeasuredRun run) =>
        run.ExitCode != ExitCode
            ? $"exited {run.ExitCode}, not {ExitCode}; stderr: {run.StandardError}"
            : Output is not null && !run.StandardOutput.AsSpan().SequenceEqual(Output)
                ? $"wrote {run.StandardOutput.Length} bytes, not the {Output.Length} expected: {Encoding.Latin1.GetString(run.StandardOutput)}"
                : StandardErrorContains is not null && !run.StandardError.Contains(StandardErrorContains, StringComparison.Ordinal)
                    ? $"stderr does not contain {StandardErrorContains}: {run.StandardError}"
                    : further?.Invoke(run);
}
