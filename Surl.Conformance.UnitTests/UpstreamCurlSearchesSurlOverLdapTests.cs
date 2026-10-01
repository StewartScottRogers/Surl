using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Windows reference build - upstream curl 8.21.0 over <c>WinLDAP</c> - searches a
/// live, in-process <c>surl ldap://</c> and <c>surl ldaps://</c>, with the exit code and output
/// each case of ADR-0072 decision 10 expects: searches by scope, filter and attributes, the
/// simple bind accepted and refused, the plain-text rule, the NTLM, <c>GSS-SPNEGO</c> and
/// <c>DIGEST-MD5</c> binds with their security layers, <c>ldaps</c> and <c>--ssl-reqd</c>.
/// surl serves a temporary <c>--directory</c> whose <c>.surl/ldap/directory.ldif</c> holds the
/// ADR's entries. Inconclusive, with the pin named, where the platform's reference pin lists no
/// <c>ldap</c>, as on Linux and macOS (ADR-0026); BL-312 proves the OpenLDAP build there.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlSearchesSurlOverLdapTests
{
    private const string Account = "alice:secret";
    private const string WrongPassword = "alice:wrong";
    private const int ManyEntries = 10001;

    private const string BaseEntry = "DN: dc=example,dc=com\n\tobjectClass: domain\n\n";

    private static readonly string[] AccountOptions = ["--user", Account];

    private static readonly Dictionary<string, byte[]> DirectoryFiles = new()
    {
        [Path.Combine(".surl", "ldap", "directory.ldif")] = Encoding.UTF8.GetBytes(DirectoryLdif()),
    };

    private static readonly string[] DirectorySubdirectories = [".surl", Path.Combine(".surl", "ldap")];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Search_BaseWithSimpleBind_Exits0WithTheBaseEntry()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--allow-plaintext-auth"]);

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Search_OneLevelWithAttributes_Exits0WithTheChildrenAndOnlyThoseAttributes()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--allow-plaintext-auth"]);

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl) + "?cn,mail?one");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(
            "DN: cn=alice,dc=example,dc=com\n\tcn: alice\n\n\tmail: alice@example.com\n\n"
            + "DN: cn=bob,dc=example,dc=com\n\tcn: bob\n\n\tmail: bob@other.example\n\n"
            + "DN: ou=many,dc=example,dc=com\n",
            Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Search_SubtreeWithFilter_Exits0WithAliceOnly()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--allow-plaintext-auth"]);

        var result = await RunCurlAsync(
            "-u", Account, BaseUrl(surl) + "?cn?sub?(&(objectClass=person)(|(cn=al*)(sn>=K))(!(mail=*@other.example)))");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("DN: cn=alice,dc=example,dc=com\n\tcn: alice\n\n", Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Search_NonAsciiValue_Exits0WithTheValueInBase64()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--allow-plaintext-auth"]);

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl) + "?description?sub?(cn=bob)");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(
            "DN: cn=bob,dc=example,dc=com\n\tdescription:: Y2Fmw6k=\n\n", Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Search_NoEntryMatches_Exits0WithNothingWritten()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--allow-plaintext-auth"]);

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl) + "??sub?(cn=nobody)");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Search_BaseNotInTheDirectory_Exits39NoSuchObject()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--allow-plaintext-auth"]);

        var result = await RunCurlAsync("-u", Account, surl.BaseUrl.AbsoluteUri + "dc=nowhere");

        Assert.AreEqual(39, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "No Such Object");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Search_PastTheSearchBound_Exits0WithTheFirstTenThousandEntries()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--allow-plaintext-auth"]);

        var result = await RunCurlAsync("-u", Account, surl.BaseUrl.AbsoluteUri + "ou=many,dc=example,dc=com??one");

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        var output = Encoding.UTF8.GetString(result.StandardOutput);
        var dnLines = output.Split('\n').Where(line => line.StartsWith("DN: ", StringComparison.Ordinal)).ToList();
        Assert.HasCount(10000, dnLines);
        Assert.AreEqual("DN: cn=entry0,ou=many,dc=example,dc=com", dnLines[0]);
        Assert.AreEqual("DN: cn=entry9999,ou=many,dc=example,dc=com", dnLines[^1]);
    }

    [TestMethod]
    public async Task Bind_WrongPassword_Exits38InvalidCredentials()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--allow-plaintext-auth"]);

        var result = await RunCurlAsync("-u", WrongPassword, BaseUrl(surl));

        Assert.AreEqual(38, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Invalid Credentials");
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    public async Task Bind_NoAccounts_Exits38InvalidCredentials()
    {
        await using var surl = await StartSurlAsync("--allow-plaintext-auth");

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl));

        Assert.AreEqual(38, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Invalid Credentials");
    }

    [TestMethod]
    public async Task Bind_PlainTextWithoutAllowPlaintextAuth_Exits38ConfidentialityRequired()
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl));

        Assert.AreEqual(38, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Confidentiality Required");
    }

    [TestMethod]
    public async Task Bind_AllowAnonymous_Exits0WithTheBaseEntry()
    {
        await using var surl = await StartSurlAsync("--allow-anonymous");

        var result = await RunCurlAsync("-u", "anyone:anything", BaseUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    [DataRow("ntlm", "--ntlm")]
    [DataRow("negotiate", "--negotiate")]
    [DataRow("digest-md5", "--digest")]
    public async Task Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer(string mechanism, string curlOption)
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--auth", mechanism]);

        var result = await RunCurlAsync(curlOption, "-u", Account, BaseUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual(BaseEntry, Encoding.UTF8.GetString(result.StandardOutput));
    }

    [TestMethod]
    public async Task Bind_NtlmWrongPassword_Exits38InvalidCredentials()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--auth", "ntlm"]);

        var result = await RunCurlAsync("--ntlm", "-u", WrongPassword, BaseUrl(surl));

        Assert.AreEqual(38, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Invalid Credentials");
    }

    [TestMethod]
    public async Task Bind_NtlmNotAccepted_Exits38AuthenticationMethodNotSupported()
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync("--ntlm", "-u", Account, BaseUrl(surl));

        Assert.AreEqual(38, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Authentication Method Not Supported");
    }

    [TestMethod]
    public async Task Bind_NoUserTheWindowsUserWithoutAnAccount_Exits38InvalidCredentials()
    {
        await using var surl = await StartSurlAsync([.. AccountOptions, "--auth", "ntlm,negotiate"]);

        var result = await RunCurlAsync(BaseUrl(surl));

        Assert.AreEqual(38, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Invalid Credentials");
    }

    [TestMethod]
    public async Task Ldaps_Insecure_Exits38ServerDownBecauseWinLdapChecksTheCertificateItself()
    {
        await using var surl = await StartSurlAsync("ldaps", [.. AccountOptions, "--self-signed"]);

        var result = await RunCurlAsync("-k", "-u", Account, BaseUrl(surl));

        Assert.AreEqual(38, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Server Down");
    }

    [TestMethod]
    public async Task Ldaps_UntrustedCertificate_Exits60()
    {
        await using var surl = await StartSurlAsync("ldaps", [.. AccountOptions, "--self-signed"]);

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl));

        Assert.AreEqual(60, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task Search_SslReqd_Exits4ExplicitTlsNotSupported()
    {
        await using var surl = await StartSurlAsync(AccountOptions);

        var result = await RunCurlAsync("--ssl-reqd", "-u", Account, BaseUrl(surl));

        Assert.AreEqual(4, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "explicit TLS not supported");
    }

    [TestMethod]
    public async Task Search_InMemoryDirectory_Exits39NoSuchObject()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync(
            "ldap", [.. AccountOptions, "--allow-plaintext-auth"], TestContext.CancellationToken);

        var result = await RunCurlAsync("-u", Account, BaseUrl(surl));

        Assert.AreEqual(39, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "No Such Object");
    }

    private static string BaseUrl(SurlOnLoopback surl) => surl.BaseUrl.AbsoluteUri + "dc=example,dc=com";

    /// <summary>ADR-0072 decision 10's directory: the base, two people and 10001 entries under <c>ou=many</c>.</summary>
    private static string DirectoryLdif()
    {
        var ldif = new StringBuilder()
            .Append("version: 1\n\n")
            .Append("dn: dc=example,dc=com\nobjectClass: domain\n\n")
            .Append("dn: cn=alice,dc=example,dc=com\nobjectClass: person\ncn: alice\nsn: Smith\nmail: alice@example.com\n\n")
            .Append("dn: cn=bob,dc=example,dc=com\nobjectClass: person\ncn: bob\nsn: Jones\nmail: bob@other.example\n")
            .Append("description:: Y2Fmw6k=\n\n")
            .Append("dn: ou=many,dc=example,dc=com\nobjectClass: organizationalUnit\nou: many\n\n");
        for (var entry = 0; entry < ManyEntries; entry++)
        {
            ldif.Append(System.Globalization.CultureInfo.InvariantCulture, $"dn: cn=entry{entry},ou=many,dc=example,dc=com\nobjectClass: person\ncn: entry{entry}\n\n");
        }

        return ldif.ToString();
    }

    private Task<SurlOnLoopback> StartSurlAsync(params string[] options) => StartSurlAsync("ldap", options);

    private Task<SurlOnLoopback> StartSurlAsync(string scheme, string[] options) =>
        SurlOnLoopback.StartAsync(scheme, DirectoryFiles, DirectorySubdirectories, options, TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunCurlAsync(params string[] arguments) =>
        PinnedUpstreamCurl.RunReferenceForProtocolAsync(TestContext, "ldap", ["-sS", "-m", "20", .. arguments]);
}
