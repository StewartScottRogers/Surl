using System.Text;

namespace Surl.Conformance;

/// <summary>
/// ADR-0076's pinned OpenLDAP build - upstream curl 8.21.0 whose <c>ldap</c> and <c>ldaps</c> run
/// over <c>lib/openldap.c</c> - binds, upgrades and searches a live, in-process <c>surl ldap://</c>
/// and <c>surl ldaps://</c>, with the exit code and output ADR-0076's measurements expect: the
/// searches BL-311 proves with <c>WinLDAP</c>, the anonymous bind, <c>StartTLS</c> for
/// <c>--ssl</c> and <c>--ssl-reqd</c>, <c>ldaps</c> with <c>-k</c> and <c>--cacert</c>, and the
/// root-DSE <c>supportedSASLMechanisms</c> search followed by a SASL bind with each mechanism
/// ADR-0072 decision 4 offers that the build has (with and without <c>--sasl-ir</c>, and
/// <c>--oauth2-bearer</c>). surl serves <see cref="LdapConformanceDirectory"/>. The build is pinned
/// for <c>linux-x64</c> only (ADR-0076 decision 4): elsewhere the tests are inconclusive, naming the
/// pin (ADR-0026 decision 2).
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests
{
    private const string OpenLdap = "OpenLDAP";
    private const string Account = LdapConformanceDirectory.Account;
    private const string WrongPassword = LdapConformanceDirectory.WrongPassword;

    // lib/openldap.c ends every entry with one more newline than WinLDAP's lib/ldap.c (ADR-0076 decision 5).
    private const string BaseEntry = "DN: dc=example,dc=com\n\tobjectClass: domain\n\n\n";

    private static readonly string[] AccountOptions = ["--user", Account];
    private static readonly string[] PlainTextAccountOptions = ["--user", Account, "--allow-plaintext-auth"];

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public Task RequireTheOpenLdapBuildAsync() => PinnedUpstreamCurl.RequireBuildLinkedAgainstAsync(TestContext, OpenLdap);

    [TestMethod]
    public async Task Search_BaseWithSimpleBind_Exits0WithTheBaseEntry()
    {
        await using var surl = await StartSurlAsync(PlainTextAccountOptions);

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Search_OneLevelWithAttributes_Exits0WithTheChildrenAndOnlyThoseAttributes()
    {
        await using var surl = await StartSurlAsync(PlainTextAccountOptions);

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl) + "?cn,mail?one");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(
            "DN: cn=alice,dc=example,dc=com\n\tcn: alice\n\n\tmail: alice@example.com\n\n\n"
            + "DN: cn=bob,dc=example,dc=com\n\tcn: bob\n\n\tmail: bob@other.example\n\n\n"
            + "DN: ou=many,dc=example,dc=com\n\n",
            Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Search_SubtreeWithFilter_Exits0WithAliceOnly()
    {
        await using var surl = await StartSurlAsync(PlainTextAccountOptions);

        var result = await RunCurlAsync(
            "-u", Account, BaseUrl(surl) + "?cn?sub?(&(objectClass=person)(|(cn=al*)(sn>=K))(!(mail=*@other.example)))");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("DN: cn=alice,dc=example,dc=com\n\tcn: alice\n\n\n", Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Search_NonAsciiValue_Exits0WithTheValueInBase64()
    {
        await using var surl = await StartSurlAsync(PlainTextAccountOptions);

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl) + "?description?sub?(cn=bob)");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(
            "DN: cn=bob,dc=example,dc=com\n\tdescription:: Y2Fmw6k=\n\n\n", Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Search_NoEntryMatches_Exits0WithNothingWritten()
    {
        await using var surl = await StartSurlAsync(PlainTextAccountOptions);

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl) + "??sub?(cn=nobody)");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Search_BaseNotInTheDirectory_Exits39NoSuchObject()
    {
        await using var surl = await StartSurlAsync(PlainTextAccountOptions);

        var result = await RunCurlAsync("-u", Account, surl.BaseUrl.AbsoluteUri + "dc=nowhere");

        Assert.AreEqual(39, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "search failed No such object");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Search_PastTheSearchBound_Exits0WithTheFirstTenThousandEntries()
    {
        await using var surl = await StartSurlAsync(PlainTextAccountOptions);

        var result = await RunCurlAsync("-u", Account, surl.BaseUrl.AbsoluteUri + "ou=many,dc=example,dc=com??one");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var dnLines = Encoding.UTF8.GetString(result.StandardOutput)
            .Split('\n')
            .Where(line => line.StartsWith("DN: ", StringComparison.Ordinal))
            .ToList();
        Assert.HasCount(10000, dnLines);
        Assert.AreEqual("DN: cn=entry0,ou=many,dc=example,dc=com", dnLines[0]);
        Assert.AreEqual("DN: cn=entry9999,ou=many,dc=example,dc=com", dnLines[^1]);
    }

    [TestMethod]
    public async Task Search_InMemoryDirectory_Exits39NoSuchObject()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync("ldap", PlainTextAccountOptions, TestContext.CancellationToken);

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl));

        Assert.AreEqual(39, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "search failed No such object");
    }

    [TestMethod]
    public async Task Bind_WrongPassword_Exits67LoginDenied()
    {
        await using var surl = await StartSurlAsync(PlainTextAccountOptions);

        var result = await RunCurlAsync("-u", WrongPassword, BaseUrl(surl));

        Assert.AreEqual(67, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Login denied");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Bind_NoAccounts_Exits67LoginDenied()
    {
        await using var surl = await StartSurlAsync("--allow-plaintext-auth");

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl));

        Assert.AreEqual(67, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Login denied");
    }

    [TestMethod]
    public async Task Bind_PlainTextWithoutAllowPlaintextAuth_Exits38CannotBind()
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl));

        Assert.AreEqual(38, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "LDAP: cannot bind");
    }

    [TestMethod]
    public async Task Bind_NoUserUnderAllowAnonymous_BindsAnonymouslyAndExits0WithTheBaseEntry()
    {
        await using var surl = await StartSurlAsync("--allow-anonymous");

        var result = await RunCurlAsync(BaseUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Bind_NoUserWithoutAllowAnonymous_Exits38CannotBind()
    {
        await using var surl = await StartSurlAsync(PlainTextAccountOptions);

        var result = await RunCurlAsync(BaseUrl(surl));

        Assert.AreEqual(38, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "LDAP: cannot bind");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task StartTls_SslReqdWithNoCertificate_Exits1UnsupportedProtocol()
    {
        await using var surl = await StartSurlAsync(PlainTextAccountOptions);

        var result = await RunCurlAsync("--ssl-reqd", "-u", Account, BaseUrl(surl));

        Assert.AreEqual(1, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Unsupported protocol");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task StartTls_SslWithNoCertificate_CarriesOnInClearWithAVersion2BindAndExits0()
    {
        await using var surl = await StartSurlAsync(PlainTextAccountOptions);

        var result = await RunCurlAsync("--ssl", "-u", Account, BaseUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    [DataRow("--ssl-reqd")]
    [DataRow("--ssl")]
    public async Task StartTls_Insecure_UpgradesAndExits0WithTheBaseEntryWithoutAllowPlaintextAuth(string sslOption)
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--self-signed"]);

        var result = await RunCurlAsync("-k", sslOption, "-u", Account, BaseUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task StartTls_UntrustedCertificate_Exits64UseSslFailedNamingTheVerifyResult()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--self-signed"]);

        var result = await RunCurlAsync("--ssl-reqd", "-u", Account, BaseUrl(surl));

        // Measured: lib/openldap.c reports a StartTLS handshake that fails verification as 64, not ldaps's 60.
        Assert.AreEqual(64, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "self-signed certificate");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Ldaps_Insecure_Exits0WithTheBaseEntry()
    {
        await using var surl = await StartSurlAsync("ldaps", [.. AccountOptions, "--self-signed"]);

        var result = await RunCurlAsync("-k", "-u", Account, BaseUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Ldaps_UntrustedCertificate_Exits60()
    {
        await using var surl = await StartSurlAsync("ldaps", [.. AccountOptions, "--self-signed"]);

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl));

        Assert.AreEqual(60, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "self-signed certificate");
    }

    [TestMethod]
    public async Task Ldaps_CertificateTrustedThroughCacert_Exits0WithTheBaseEntry()
    {
        using var authority = TestCertificateAuthority.Create();
        await using var surl = await StartSurlAsync(
            "ldaps", [.. AccountOptions, "--cert", authority.ServerCertificateFile, "--key", authority.ServerKeyFile]);

        var result = await RunCurlAsync("--cacert", authority.CaCertificateFile, "-u", Account, BaseUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    [DataRow("PLAIN", false)]
    [DataRow("PLAIN", true)]
    [DataRow("LOGIN", false)]
    [DataRow("LOGIN", true)]
    public async Task SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry(string mechanism, bool initialResponse)
    {
        await using var surl = await StartSurlAsync(PlainTextAccountOptions);

        var result = await RunCurlAsync(
            [.. SaslInitialResponse(initialResponse), "--login-options", $"AUTH={mechanism}", "-u", Account, BaseUrl(surl)]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    [DataRow("PLAIN")]
    [DataRow("LOGIN")]
    [DataRow("CRAM-MD5")]
    public async Task SaslBind_WrongPassword_Exits67LoginDenied(string mechanism)
    {
        await using var surl = await StartSurlAsync(PlainTextAccountOptions);

        var result = await RunCurlAsync("--login-options", $"AUTH={mechanism}", "-u", WrongPassword, BaseUrl(surl));

        Assert.AreEqual(67, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Login denied");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task SaslBind_PlainOverStartTls_Exits0WithTheBaseEntryWithoutAllowPlaintextAuth()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--self-signed"]);

        var result = await RunCurlAsync("-k", "--ssl-reqd", "--login-options", "AUTH=PLAIN", "-u", Account, BaseUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task SaslBind_PlainNotOfferedInClearWithoutAllowPlaintextAuth_Exits67LoginDenied()
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync("--login-options", "AUTH=PLAIN", "-u", Account, BaseUrl(surl));

        Assert.AreEqual(67, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    [DataRow("cram-md5", "--login-options", "AUTH=CRAM-MD5")]
    [DataRow("digest-md5", "--login-options", "AUTH=DIGEST-MD5")]
    [DataRow("digest-md5", "--digest", null)]
    [DataRow("ntlm", "--login-options", "AUTH=NTLM")]
    [DataRow("ntlm", "--ntlm", null)]
    public async Task SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry(
        string authWord, string curlOption, string? curlOptionArgument)
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--auth", authWord]);

        var result = await RunCurlAsync(
            [curlOption, .. curlOptionArgument is null ? [] : (string[])[curlOptionArgument], "-u", Account, BaseUrl(surl)]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    [DataRow("digest-md5", "--digest")]
    [DataRow("ntlm", "--ntlm")]
    public async Task SaslBind_ChallengeResponseMechanismWrongPassword_Exits67LoginDenied(string authWord, string curlOption)
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--auth", authWord]);

        var result = await RunCurlAsync(curlOption, "-u", WrongPassword, BaseUrl(surl));

        Assert.AreEqual(67, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task SaslBind_AnyMechanism_PicksOneSurlOffersAndExits0WithTheBaseEntry()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--auth", "digest-md5,cram-md5,plain"]);

        var result = await RunCurlAsync("--login-options", "AUTH=*", "-u", Account, BaseUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    [DataRow("OAUTHBEARER", false)]
    [DataRow("OAUTHBEARER", true)]
    [DataRow("XOAUTH2", false)]
    [DataRow("XOAUTH2", true)]
    public async Task SaslBind_BearerToken_Exits0WithTheBaseEntry(string mechanism, bool initialResponse)
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("--user-file", accounts.Path, "--allow-plaintext-auth");

        var result = await RunCurlAsync(
        [
            .. SaslInitialResponse(initialResponse), "--login-options", $"AUTH={mechanism}",
            "-u", $"{AccountsFile.User}:", "--oauth2-bearer", AccountsFile.BearerToken, BaseUrl(surl),
        ]);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task SaslBind_WrongBearerToken_Exits67LoginDenied()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("--user-file", accounts.Path, "--allow-plaintext-auth");

        var result = await RunCurlAsync(
            "-u", $"{AccountsFile.User}:", "--oauth2-bearer", "not-the-token", BaseUrl(surl));

        Assert.AreEqual(67, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task SaslBind_ExternalWithAClientCertificateOverLdaps_Exits0WithTheBaseEntry()
    {
        using var authority = TestCertificateAuthority.Create();
        await using var surl = await StartSurlAsync(
            "ldaps",
            [
                .. AccountOptions, "--cert", authority.ServerCertificateFile, "--key", authority.ServerKeyFile,
                "--cacert", authority.CaCertificateFile,
            ]);

        var result = await RunCurlAsync(
            "--cacert", authority.CaCertificateFile, "--cert", authority.ClientCertificateFile, "--key", authority.ClientKeyFile,
            "--login-options", "AUTH=EXTERNAL", "-u", $"{TestCertificateAuthority.ClientCommonName}:", BaseUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task SaslBind_ExternalWithoutAClientCertificate_Exits67LoginDenied()
    {
        await using var surl = await StartSurlAsync("ldaps", [.. AccountOptions, "--self-signed"]);

        var result = await RunCurlAsync(
            "-k", "--login-options", "AUTH=EXTERNAL", "-u", $"{TestCertificateAuthority.ClientCommonName}:", BaseUrl(surl));

        Assert.AreEqual(67, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task SaslBind_Gssapi_Exits67BecauseTheBuildHasNoGssApi()
    {
        await using var surl = await StartSurlAsync(PlainTextAccountOptions);

        var result = await RunCurlAsync("--login-options", "AUTH=GSSAPI", "-u", Account, BaseUrl(surl));

        Assert.AreEqual(67, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    private static string[] SaslInitialResponse(bool initialResponse) => initialResponse ? ["--sasl-ir"] : [];

    private static string BaseUrl(SurlOnLoopback surl) => surl.BaseUrl.AbsoluteUri + LdapConformanceDirectory.BaseDn;

    private Task<SurlOnLoopback> StartSurlAsync(params string[] options) => StartSurlAsync("ldap", options);

    private Task<SurlOnLoopback> StartSurlAsync(string scheme, string[] options) =>
        SurlOnLoopback.StartAsync(
            scheme, LdapConformanceDirectory.Files, LdapConformanceDirectory.Subdirectories, options, TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunCurlAsync(params string[] arguments) =>
        PinnedUpstreamCurl.RunBuildLinkedAgainstAsync(TestContext, OpenLdap, ["-sS", "-m", "20", .. arguments]);
}
