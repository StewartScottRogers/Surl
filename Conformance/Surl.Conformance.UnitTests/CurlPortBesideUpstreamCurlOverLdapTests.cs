using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Phase 7 for LDAP and LDAPS (ADR-0089, BL-586): every conversation
/// <see cref="UpstreamCurlSearchesSurlOverLdapTests"/> (the <c>WinLDAP</c> reference build),
/// <see cref="UpstreamCurlBindsToSurlOverLdapWithKerberosTests"/> and
/// <see cref="UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests"/> (ADR-0076's OpenLDAP build) run,
/// through pinned upstream curl and the pinned Curl port, each against its own fresh <c>surl</c>,
/// judged by <see cref="PinnedCurlPortBesideUpstreamCurl"/>. One test per mirrored test, each naming
/// it in its <see cref="DescriptionAttribute"/>; the method's prefix (<c>WinLdap_</c>,
/// <c>WinLdapKerberos_</c>, <c>OpenLdap_</c>) names the mirrored class, because the two builds' classes
/// share method names. Each case is paired with the build <see cref="UpstreamCurlLocator.LocateForProtocol"/>
/// picks for <c>ldap</c> on the platform (ADR-0089 decision 2): <c>WinLDAP</c> on Windows, OpenLDAP on
/// Linux. An <c>OpenLdap_</c> test is Inconclusive wherever no OpenLDAP build is pinned, as its mirrored
/// test is. A test fails when upstream's run is not what the mirrored test asserts (a Surl defect),
/// when the port disagrees and <c>Documentation/Conformance/CurlPort/Ldap.md</c> does not list the case,
/// and when a listed case agrees (ADR-0089 decision 5). Bytes are not compared where the bind seals the
/// conversation or carries a random challenge (NTLM, <c>GSS-SPNEGO</c>, <c>DIGEST-MD5</c>, <c>CRAM-MD5</c>,
/// Kerberos) or where StartTLS upgrades it, as they would be the security library's, not curl's.
/// Inconclusive when either pinned binary is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverLdapTests
{
    private const string FindingsFile = "Documentation/Conformance/CurlPort/Ldap.md";

    private const string WinLdap = nameof(UpstreamCurlSearchesSurlOverLdapTests);
    private const string WinLdapKerberos = nameof(UpstreamCurlBindsToSurlOverLdapWithKerberosTests);
    private const string OpenLdap = nameof(UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests);

    private const string OpenLdapLibrary = "OpenLDAP";
    private const string Account = LdapConformanceDirectory.Account;
    private const string WrongPassword = LdapConformanceDirectory.WrongPassword;
    private const string Base = "{surl}/" + LdapConformanceDirectory.BaseDn;
    private const string AliceDn = "cn=alice,dc=example,dc=com";

    // The host ADR-0078 measured GSSAPI with; ldap/ldap.surl.test is the service principal.
    private const string KerberosHost = "ldap.surl.test";

    private const string WinLdapBaseEntry = "DN: dc=example,dc=com\n\tobjectClass: domain\n\n";

    // lib/openldap.c ends every entry with one more newline than WinLDAP's lib/ldap.c (ADR-0076 decision 5).
    private const string OpenLdapBaseEntry = "DN: dc=example,dc=com\n\tobjectClass: domain\n\n\n";

    private static readonly string[] AccountOptions = ["--user", Account];
    private static readonly string[] PlainTextAccountOptions = ["--user", Account, "--allow-plaintext-auth"];

    /// <summary>
    /// <see cref="LdapConformanceDirectory.Files"/> keyed with <c>/</c>, so the helper creates the
    /// <c>.surl/ldap</c> folders the directory file sits in on every platform.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, byte[]> DirectoryFiles = new Dictionary<string, byte[]>
    {
        [".surl/ldap/directory.ldif"] = LdapConformanceDirectory.Files[Path.Combine(".surl", "ldap", "directory.ldif")],
    };

    private static readonly IReadOnlyDictionary<string, byte[]> NoFiles = new Dictionary<string, byte[]>();

    public TestContext TestContext { get; set; } = null!;

    // --- UpstreamCurlSearchesSurlOverLdapTests (WinLDAP) ---

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Search_BaseWithSimpleBind_Exits0WithTheBaseEntry")]
    public Task WinLdap_Search_BaseWithSimpleBind_Exits0WithTheBaseEntry() =>
        JudgeAsync(WinLdapCase(Named(), ["-u", Account, Base], PlainTextAccountOptions, ExitsZeroWriting(WinLdapBaseEntry)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Search_OneLevelWithAttributes_Exits0WithTheChildrenAndOnlyThoseAttributes")]
    public Task WinLdap_Search_OneLevelWithAttributes_Exits0WithTheChildrenAndOnlyThoseAttributes() =>
        JudgeAsync(WinLdapCase(Named(), ["-u", Account, Base + "?cn,mail?one"], PlainTextAccountOptions, ExitsZeroWriting(
            "DN: cn=alice,dc=example,dc=com\n\tcn: alice\n\n\tmail: alice@example.com\n\n"
            + "DN: cn=bob,dc=example,dc=com\n\tcn: bob\n\n\tmail: bob@other.example\n\n"
            + "DN: ou=many,dc=example,dc=com\n")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Search_SubtreeWithFilter_Exits0WithAliceOnly")]
    public Task WinLdap_Search_SubtreeWithFilter_Exits0WithAliceOnly() =>
        JudgeAsync(WinLdapCase(
            Named(),
            ["-u", Account, Base + "?cn?sub?(&(objectClass=person)(|(cn=al*)(sn>=K))(!(mail=*@other.example)))"],
            PlainTextAccountOptions,
            ExitsZeroWriting("DN: cn=alice,dc=example,dc=com\n\tcn: alice\n\n")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Search_NonAsciiValue_Exits0WithTheValueInBase64")]
    public Task WinLdap_Search_NonAsciiValue_Exits0WithTheValueInBase64() =>
        JudgeAsync(WinLdapCase(
            Named(), ["-u", Account, Base + "?description?sub?(cn=bob)"], PlainTextAccountOptions, ExitsZeroWriting("DN: cn=bob,dc=example,dc=com\n\tdescription:: Y2Fmw6k=\n\n")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Search_NoEntryMatches_Exits0WithNothingWritten")]
    public Task WinLdap_Search_NoEntryMatches_Exits0WithNothingWritten() =>
        JudgeAsync(WinLdapCase(Named(), ["-u", Account, Base + "??sub?(cn=nobody)"], PlainTextAccountOptions, ExitsZeroWriting(string.Empty)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Search_BaseNotInTheDirectory_Exits39NoSuchObject")]
    public Task WinLdap_Search_BaseNotInTheDirectory_Exits39NoSuchObject() =>
        JudgeAsync(Failing(WinLdapCase(
            Named(), ["-u", Account, "{surl}/dc=nowhere"], PlainTextAccountOptions, ExitsWithErrorWritingNothing(39, "No Such Object")), "No Such Object"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Search_PastTheSearchBound_Exits0WithTheFirstTenThousandEntries")]
    public Task WinLdap_Search_PastTheSearchBound_Exits0WithTheFirstTenThousandEntries() =>
        JudgeAsync(WinLdapCase(Named(), ["-u", Account, "{surl}/ou=many,dc=example,dc=com??one"], PlainTextAccountOptions, ExitsZeroWritingTheFirstTenThousandEntries));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Bind_WrongPassword_Exits38InvalidCredentials")]
    public Task WinLdap_Bind_WrongPassword_Exits38InvalidCredentials() =>
        JudgeAsync(Failing(WinLdapCase(
            Named(), ["-u", WrongPassword, Base], PlainTextAccountOptions, ExitsWithErrorWritingNothing(38, "Invalid Credentials")), "Invalid Credentials"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Bind_NoAccounts_Exits38InvalidCredentials")]
    public Task WinLdap_Bind_NoAccounts_Exits38InvalidCredentials() =>
        JudgeAsync(Failing(WinLdapCase(Named(), ["-u", Account, Base], ["--allow-plaintext-auth"], ExitsWithError(38, "Invalid Credentials")), "Invalid Credentials"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Bind_PlainTextWithoutAllowPlaintextAuth_Exits38ConfidentialityRequired")]
    public Task WinLdap_Bind_PlainTextWithoutAllowPlaintextAuth_Exits38ConfidentialityRequired() =>
        JudgeAsync(Failing(WinLdapCase(Named(), ["-u", Account, Base], AccountOptions, ExitsWithError(38, "Confidentiality Required")), "Confidentiality Required"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Bind_AllowAnonymous_Exits0WithTheBaseEntry")]
    public Task WinLdap_Bind_AllowAnonymous_Exits0WithTheBaseEntry() =>
        JudgeAsync(WinLdapCase(Named(), ["-u", "anyone:anything", Base], ["--allow-anonymous"], ExitsZeroWriting(WinLdapBaseEntry)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer")]
    [DataRow("ntlm", "--ntlm")]
    [DataRow("negotiate", "--negotiate")]
    [DataRow("digest-md5", "--digest")]
    public Task WinLdap_Bind_SaslOrSicilyWithAccount_Exits0WithTheBaseEntryThroughTheSecurityLayer(string mechanism, string curlOption) =>
        JudgeAsync(Sealed(WinLdapCase(
            Named($"{mechanism}, {curlOption}"), [curlOption, "-u", Account, Base], [.. AccountOptions, "--auth", mechanism], ExitsZeroWriting(WinLdapBaseEntry))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Bind_NtlmWrongPassword_Exits38InvalidCredentials")]
    public Task WinLdap_Bind_NtlmWrongPassword_Exits38InvalidCredentials() =>
        JudgeAsync(Failing(Sealed(WinLdapCase(
            Named(), ["--ntlm", "-u", WrongPassword, Base], [.. AccountOptions, "--auth", "ntlm"], ExitsWithError(38, "Invalid Credentials"))), "Invalid Credentials"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Bind_NtlmNotAccepted_Exits38AuthenticationMethodNotSupported")]
    public Task WinLdap_Bind_NtlmNotAccepted_Exits38AuthenticationMethodNotSupported() =>
        JudgeAsync(Failing(Sealed(WinLdapCase(
            Named(), ["--ntlm", "-u", Account, Base], AccountOptions, ExitsWithError(38, "Authentication Method Not Supported"))), "Authentication Method Not Supported"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Bind_NoUserTheWindowsUserWithoutAnAccount_Exits38InvalidCredentials")]
    public Task WinLdap_Bind_NoUserTheWindowsUserWithoutAnAccount_Exits38InvalidCredentials() =>
        JudgeAsync(Failing(Sealed(WinLdapCase(
            Named(), [Base], [.. AccountOptions, "--auth", "ntlm,negotiate"], ExitsWithError(38, "Invalid Credentials"))), "Invalid Credentials"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Ldaps_Insecure_Exits38ServerDownBecauseWinLdapChecksTheCertificateItself")]
    public Task WinLdap_Ldaps_Insecure_Exits38ServerDownBecauseWinLdapChecksTheCertificateItself() =>
        JudgeAsync(Failing(WinLdapCase(
            Named(), ["-k", "-u", Account, Base], [.. AccountOptions, "--self-signed"], ExitsWithError(38, "Server Down"), "ldaps"), "Server Down"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Ldaps_UntrustedCertificate_Exits60")]
    public Task WinLdap_Ldaps_UntrustedCertificate_Exits60() =>
        JudgeAsync(WinLdapCase(Named(), ["-u", Account, Base], [.. AccountOptions, "--self-signed"], Exits(60), "ldaps"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Search_SslReqd_Exits4ExplicitTlsNotSupported")]
    public Task WinLdap_Search_SslReqd_Exits4ExplicitTlsNotSupported() =>
        JudgeAsync(Failing(WinLdapCase(
            Named(), ["--ssl-reqd", "-u", Account, Base], AccountOptions, ExitsWithError(4, "explicit TLS not supported")), "explicit TLS not supported"));

    // The mirrored test starts surl with no --directory; the helper always serves a directory, so
    // this case serves an empty one, which holds no directory.ldif and so no entry, as memory does.
    [TestMethod]
    [Description("Mirrors UpstreamCurlSearchesSurlOverLdapTests.Search_InMemoryDirectory_Exits39NoSuchObject")]
    public Task WinLdap_Search_InMemoryDirectory_Exits39NoSuchObject() =>
        JudgeAsync(Failing(WinLdapCase(Named(), ["-u", Account, Base], PlainTextAccountOptions, ExitsWithError(39, "No Such Object")) with
        {
            Files = NoFiles,
        }, "No Such Object"));

    // --- UpstreamCurlBindsToSurlOverLdapWithKerberosTests (WinLDAP) ---

    // WinLDAP asks for ldap/<host name>:<port> for a URL naming localhost, so the port is chosen before
    // the KDC starts and both runs' surl listen on it, one after the other; the URL names it literally.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DoNotParallelize]
    [Description("Mirrors UpstreamCurlBindsToSurlOverLdapWithKerberosTests.NegotiateBind_KerberosAccount_Exits0WithTheEntryThroughTheSealedLayer")]
    public async Task WinLdapKerberos_NegotiateBind_KerberosAccount_Exits0WithTheEntryThroughTheSealedLayer()
    {
        var port = FreeLoopbackPort();
        await using var kdc = await KerberosTestKdcOnLoopback.StartAsync([$"ldap/{Dns.GetHostName()}:{port}"], TestContext.CancellationToken);
        using var accounts = await KerberosAccountsFile.WriteAsync(TestContext.CancellationToken);
        var measurementCase = Sealed(Case(
            Named(),
            WinLdapKerberos,
            "ldap",
            [
                "-m", "20", "--negotiate", "-u", $"{KerberosTestKdcOnLoopback.UserPrincipal}:{KerberosTestKdcOnLoopback.Password}",
                $"ldap://localhost:{port}/{AliceDn}?cn?base",
            ],
            ["--auth", "negotiate", "--keytab", kdc.KeytabPath, "--user-file", accounts.Path],
            ExitsZeroWriting($"DN: {AliceDn}\n\tcn: alice\n\n")));

        await JudgeAsync(measurementCase, port);
    }

    // --- UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests (OpenLDAP) ---

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Search_BaseWithSimpleBind_Exits0WithTheBaseEntry")]
    public Task OpenLdap_Search_BaseWithSimpleBind_Exits0WithTheBaseEntry() =>
        JudgeOpenLdapAsync(OpenLdapCase(Named(), ["-u", Account, Base], PlainTextAccountOptions, ExitsZeroWriting(OpenLdapBaseEntry)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Search_OneLevelWithAttributes_Exits0WithTheChildrenAndOnlyThoseAttributes")]
    public Task OpenLdap_Search_OneLevelWithAttributes_Exits0WithTheChildrenAndOnlyThoseAttributes() =>
        JudgeOpenLdapAsync(OpenLdapCase(Named(), ["-u", Account, Base + "?cn,mail?one"], PlainTextAccountOptions, ExitsZeroWriting(
            "DN: cn=alice,dc=example,dc=com\n\tcn: alice\n\n\tmail: alice@example.com\n\n\n"
            + "DN: cn=bob,dc=example,dc=com\n\tcn: bob\n\n\tmail: bob@other.example\n\n\n"
            + "DN: ou=many,dc=example,dc=com\n\n")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Search_SubtreeWithFilter_Exits0WithAliceOnly")]
    public Task OpenLdap_Search_SubtreeWithFilter_Exits0WithAliceOnly() =>
        JudgeOpenLdapAsync(OpenLdapCase(
            Named(),
            ["-u", Account, Base + "?cn?sub?(&(objectClass=person)(|(cn=al*)(sn>=K))(!(mail=*@other.example)))"],
            PlainTextAccountOptions,
            ExitsZeroWriting("DN: cn=alice,dc=example,dc=com\n\tcn: alice\n\n\n")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Search_NonAsciiValue_Exits0WithTheValueInBase64")]
    public Task OpenLdap_Search_NonAsciiValue_Exits0WithTheValueInBase64() =>
        JudgeOpenLdapAsync(OpenLdapCase(
            Named(), ["-u", Account, Base + "?description?sub?(cn=bob)"], PlainTextAccountOptions, ExitsZeroWriting("DN: cn=bob,dc=example,dc=com\n\tdescription:: Y2Fmw6k=\n\n\n")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Search_NoEntryMatches_Exits0WithNothingWritten")]
    public Task OpenLdap_Search_NoEntryMatches_Exits0WithNothingWritten() =>
        JudgeOpenLdapAsync(OpenLdapCase(Named(), ["-u", Account, Base + "??sub?(cn=nobody)"], PlainTextAccountOptions, ExitsZeroWriting(string.Empty)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Search_BaseNotInTheDirectory_Exits39NoSuchObject")]
    public Task OpenLdap_Search_BaseNotInTheDirectory_Exits39NoSuchObject() =>
        JudgeOpenLdapAsync(Failing(OpenLdapCase(
            Named(), ["-u", Account, "{surl}/dc=nowhere"], PlainTextAccountOptions, ExitsWithErrorWritingNothing(39, "search failed No such object")), "search failed No such object"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Search_PastTheSearchBound_Exits0WithTheFirstTenThousandEntries")]
    public Task OpenLdap_Search_PastTheSearchBound_Exits0WithTheFirstTenThousandEntries() =>
        JudgeOpenLdapAsync(OpenLdapCase(Named(), ["-u", Account, "{surl}/ou=many,dc=example,dc=com??one"], PlainTextAccountOptions, ExitsZeroWritingTheFirstTenThousandEntries));

    // As WinLdap_Search_InMemoryDirectory_Exits39NoSuchObject: an empty served directory stands for memory.
    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Search_InMemoryDirectory_Exits39NoSuchObject")]
    public Task OpenLdap_Search_InMemoryDirectory_Exits39NoSuchObject() =>
        JudgeOpenLdapAsync(Failing(OpenLdapCase(Named(), ["-u", Account, Base], PlainTextAccountOptions, ExitsWithError(39, "search failed No such object")) with
        {
            Files = NoFiles,
        }, "search failed No such object"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Bind_WrongPassword_Exits67LoginDenied")]
    public Task OpenLdap_Bind_WrongPassword_Exits67LoginDenied() =>
        JudgeOpenLdapAsync(Failing(OpenLdapCase(Named(), ["-u", WrongPassword, Base], PlainTextAccountOptions, ExitsWithErrorWritingNothing(67, "Login denied")), "Login denied"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Bind_NoAccounts_Exits67LoginDenied")]
    public Task OpenLdap_Bind_NoAccounts_Exits67LoginDenied() =>
        JudgeOpenLdapAsync(Failing(OpenLdapCase(Named(), ["-u", Account, Base], ["--allow-plaintext-auth"], ExitsWithError(67, "Login denied")), "Login denied"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Bind_PlainTextWithoutAllowPlaintextAuth_Exits38CannotBind")]
    public Task OpenLdap_Bind_PlainTextWithoutAllowPlaintextAuth_Exits38CannotBind() =>
        JudgeOpenLdapAsync(Failing(OpenLdapCase(Named(), ["-u", Account, Base], AccountOptions, ExitsWithError(38, "LDAP: cannot bind")), "LDAP: cannot bind"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Bind_NoUserUnderAllowAnonymous_BindsAnonymouslyAndExits0WithTheBaseEntry")]
    public Task OpenLdap_Bind_NoUserUnderAllowAnonymous_BindsAnonymouslyAndExits0WithTheBaseEntry() =>
        JudgeOpenLdapAsync(OpenLdapCase(Named(), [Base], ["--allow-anonymous"], ExitsZeroWriting(OpenLdapBaseEntry)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Bind_NoUserWithoutAllowAnonymous_Exits38CannotBind")]
    public Task OpenLdap_Bind_NoUserWithoutAllowAnonymous_Exits38CannotBind() =>
        JudgeOpenLdapAsync(Failing(OpenLdapCase(Named(), [Base], PlainTextAccountOptions, ExitsWithErrorWritingNothing(38, "LDAP: cannot bind")), "LDAP: cannot bind"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.StartTls_SslReqdWithNoCertificate_Exits1UnsupportedProtocol")]
    public Task OpenLdap_StartTls_SslReqdWithNoCertificate_Exits1UnsupportedProtocol() =>
        JudgeOpenLdapAsync(Failing(OpenLdapCase(
            Named(), ["--ssl-reqd", "-u", Account, Base], PlainTextAccountOptions, ExitsWithErrorWritingNothing(1, "Unsupported protocol")), "Unsupported protocol"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.StartTls_SslWithNoCertificate_CarriesOnInClearWithAVersion2BindAndExits0")]
    public Task OpenLdap_StartTls_SslWithNoCertificate_CarriesOnInClearWithAVersion2BindAndExits0() =>
        JudgeOpenLdapAsync(OpenLdapCase(Named(), ["--ssl", "-u", Account, Base], PlainTextAccountOptions, ExitsZeroWriting(OpenLdapBaseEntry)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.StartTls_Insecure_UpgradesAndExits0WithTheBaseEntryWithoutAllowPlaintextAuth")]
    [DataRow("--ssl-reqd")]
    [DataRow("--ssl")]
    public Task OpenLdap_StartTls_Insecure_UpgradesAndExits0WithTheBaseEntryWithoutAllowPlaintextAuth(string sslOption) =>
        JudgeOpenLdapAsync(Sealed(OpenLdapCase(
            Named(sslOption), ["-k", sslOption, "-u", Account, Base], [.. AccountOptions, "--self-signed"], ExitsZeroWriting(OpenLdapBaseEntry))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.StartTls_UntrustedCertificate_Exits64UseSslFailedNamingTheVerifyResult")]
    public Task OpenLdap_StartTls_UntrustedCertificate_Exits64UseSslFailedNamingTheVerifyResult() =>
        JudgeOpenLdapAsync(Failing(Sealed(OpenLdapCase(
            Named(), ["--ssl-reqd", "-u", Account, Base], [.. AccountOptions, "--self-signed"], ExitsWithErrorWritingNothing(64, "self-signed certificate"))), "self-signed certificate"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Ldaps_Insecure_Exits0WithTheBaseEntry")]
    public Task OpenLdap_Ldaps_Insecure_Exits0WithTheBaseEntry() =>
        JudgeOpenLdapAsync(OpenLdapCase(Named(), ["-k", "-u", Account, Base], [.. AccountOptions, "--self-signed"], ExitsZeroWriting(OpenLdapBaseEntry), "ldaps"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Ldaps_UntrustedCertificate_Exits60")]
    public Task OpenLdap_Ldaps_UntrustedCertificate_Exits60() =>
        JudgeOpenLdapAsync(Failing(OpenLdapCase(
            Named(), ["-u", Account, Base], [.. AccountOptions, "--self-signed"], ExitsWithError(60, "self-signed certificate"), "ldaps"), "self-signed certificate"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.Ldaps_CertificateTrustedThroughCacert_Exits0WithTheBaseEntry")]
    public async Task OpenLdap_Ldaps_CertificateTrustedThroughCacert_Exits0WithTheBaseEntry()
    {
        await RequireOpenLdapAsync();
        using var authority = TestCertificateAuthority.Create();

        await JudgeAsync(OpenLdapCase(
            Named(),
            ["--cacert", authority.CaCertificateFile, "-u", Account, Base],
            [.. AccountOptions, "--cert", authority.ServerCertificateFile, "--key", authority.ServerKeyFile],
            ExitsZeroWriting(OpenLdapBaseEntry),
            "ldaps"));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry")]
    [DataRow("PLAIN", false)]
    [DataRow("PLAIN", true)]
    [DataRow("LOGIN", false)]
    [DataRow("LOGIN", true)]
    public Task OpenLdap_SaslBind_PlainTextMechanismWithAllowPlaintextAuth_Exits0WithTheBaseEntry(string mechanism, bool initialResponse) =>
        JudgeOpenLdapAsync(OpenLdapCase(
            Named($"{mechanism}, {initialResponse}"),
            [.. SaslInitialResponse(initialResponse), "--login-options", $"AUTH={mechanism}", "-u", Account, Base],
            PlainTextAccountOptions,
            ExitsZeroWriting(OpenLdapBaseEntry)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_WrongPassword_Exits67LoginDenied")]
    [DataRow("PLAIN")]
    [DataRow("LOGIN")]
    [DataRow("CRAM-MD5")]
    public Task OpenLdap_SaslBind_WrongPassword_Exits67LoginDenied(string mechanism)
    {
        var measurementCase = Failing(OpenLdapCase(
            Named(mechanism), ["--login-options", $"AUTH={mechanism}", "-u", WrongPassword, Base], PlainTextAccountOptions, ExitsWithErrorWritingNothing(67, "Login denied")), "Login denied");
        return JudgeOpenLdapAsync(mechanism == "CRAM-MD5" ? Sealed(measurementCase) : measurementCase);
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_PlainOverStartTls_Exits0WithTheBaseEntryWithoutAllowPlaintextAuth")]
    public Task OpenLdap_SaslBind_PlainOverStartTls_Exits0WithTheBaseEntryWithoutAllowPlaintextAuth() =>
        JudgeOpenLdapAsync(Sealed(OpenLdapCase(
            Named(), ["-k", "--ssl-reqd", "--login-options", "AUTH=PLAIN", "-u", Account, Base], [.. AccountOptions, "--self-signed"], ExitsZeroWriting(OpenLdapBaseEntry))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_PlainNotOfferedInClearWithoutAllowPlaintextAuth_Exits67LoginDenied")]
    public Task OpenLdap_SaslBind_PlainNotOfferedInClearWithoutAllowPlaintextAuth_Exits67LoginDenied() =>
        JudgeOpenLdapAsync(OpenLdapCase(Named(), ["--login-options", "AUTH=PLAIN", "-u", Account, Base], AccountOptions, ExitsWritingNothing(67)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry")]
    [DataRow("cram-md5", "--login-options", "AUTH=CRAM-MD5")]
    [DataRow("digest-md5", "--login-options", "AUTH=DIGEST-MD5")]
    [DataRow("digest-md5", "--digest", null)]
    [DataRow("ntlm", "--login-options", "AUTH=NTLM")]
    [DataRow("ntlm", "--ntlm", null)]
    public Task OpenLdap_SaslBind_ChallengeResponseMechanismInClear_Exits0WithTheBaseEntry(string authWord, string curlOption, string? curlOptionArgument) =>
        JudgeOpenLdapAsync(Sealed(OpenLdapCase(
            Named($"{authWord}, {curlOption}, {curlOptionArgument ?? "null"}"),
            [curlOption, .. curlOptionArgument is null ? [] : (string[])[curlOptionArgument], "-u", Account, Base],
            [.. AccountOptions, "--auth", authWord],
            ExitsZeroWriting(OpenLdapBaseEntry))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_ChallengeResponseMechanismWrongPassword_Exits67LoginDenied")]
    [DataRow("digest-md5", "--digest")]
    [DataRow("ntlm", "--ntlm")]
    public Task OpenLdap_SaslBind_ChallengeResponseMechanismWrongPassword_Exits67LoginDenied(string authWord, string curlOption) =>
        JudgeOpenLdapAsync(Sealed(OpenLdapCase(
            Named($"{authWord}, {curlOption}"), [curlOption, "-u", WrongPassword, Base], [.. AccountOptions, "--auth", authWord], ExitsWritingNothing(67))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_AnyMechanism_PicksOneSurlOffersAndExits0WithTheBaseEntry")]
    public Task OpenLdap_SaslBind_AnyMechanism_PicksOneSurlOffersAndExits0WithTheBaseEntry() =>
        JudgeOpenLdapAsync(Sealed(OpenLdapCase(
            Named(), ["--login-options", "AUTH=*", "-u", Account, Base], [.. AccountOptions, "--auth", "digest-md5,cram-md5,plain"], ExitsZeroWriting(OpenLdapBaseEntry))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_BearerToken_Exits0WithTheBaseEntry")]
    [DataRow("OAUTHBEARER", false)]
    [DataRow("OAUTHBEARER", true)]
    [DataRow("XOAUTH2", false)]
    [DataRow("XOAUTH2", true)]
    public async Task OpenLdap_SaslBind_BearerToken_Exits0WithTheBaseEntry(string mechanism, bool initialResponse)
    {
        await RequireOpenLdapAsync();
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await JudgeAsync(OpenLdapCase(
            Named($"{mechanism}, {initialResponse}"),
            [
                .. SaslInitialResponse(initialResponse), "--login-options", $"AUTH={mechanism}",
                "-u", $"{AccountsFile.User}:", "--oauth2-bearer", AccountsFile.BearerToken, Base,
            ],
            ["--user-file", accounts.Path, "--allow-plaintext-auth"],
            ExitsZeroWriting(OpenLdapBaseEntry)));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_WrongBearerToken_Exits67LoginDenied")]
    public async Task OpenLdap_SaslBind_WrongBearerToken_Exits67LoginDenied()
    {
        await RequireOpenLdapAsync();
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);

        await JudgeAsync(OpenLdapCase(
            Named(),
            ["-u", $"{AccountsFile.User}:", "--oauth2-bearer", "not-the-token", Base],
            ["--user-file", accounts.Path, "--allow-plaintext-auth"],
            ExitsWritingNothing(67)));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_ExternalWithAClientCertificateOverLdaps_Exits0WithTheBaseEntry")]
    public async Task OpenLdap_SaslBind_ExternalWithAClientCertificateOverLdaps_Exits0WithTheBaseEntry()
    {
        await RequireOpenLdapAsync();
        using var authority = TestCertificateAuthority.Create();

        await JudgeAsync(OpenLdapCase(
            Named(),
            [
                "--cacert", authority.CaCertificateFile, "--cert", authority.ClientCertificateFile, "--key", authority.ClientKeyFile,
                "--login-options", "AUTH=EXTERNAL", "-u", $"{TestCertificateAuthority.ClientCommonName}:", Base,
            ],
            [
                .. AccountOptions, "--cert", authority.ServerCertificateFile, "--key", authority.ServerKeyFile,
                "--cacert", authority.CaCertificateFile,
            ],
            ExitsZeroWriting(OpenLdapBaseEntry),
            "ldaps"));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_ExternalWithoutAClientCertificate_Exits67LoginDenied")]
    public Task OpenLdap_SaslBind_ExternalWithoutAClientCertificate_Exits67LoginDenied() =>
        JudgeOpenLdapAsync(OpenLdapCase(
            Named(),
            ["-k", "--login-options", "AUTH=EXTERNAL", "-u", $"{TestCertificateAuthority.ClientCommonName}:", Base],
            [.. AccountOptions, "--self-signed"],
            ExitsWritingNothing(67),
            "ldaps"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_GssapiWithAUserNamingNoRealm_Exits67WithoutABind")]
    public Task OpenLdap_SaslBind_GssapiWithAUserNamingNoRealm_Exits67WithoutABind() =>
        JudgeOpenLdapAsync(OpenLdapCase(Named(), ["--login-options", "AUTH=GSSAPI", "-u", Account, Base], PlainTextAccountOptions, ExitsWritingNothing(67)));

    // The URL names the service host, pinned to loopback with --resolve, so the port is chosen first
    // and both runs' surl listen on it in turn; both clients read the KDC's MIT credential cache.
    [TestMethod]
    [DoNotParallelize]
    [Description("Mirrors UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_GssapiWithATicketFromTheTestKdc_Exits0WithTheBaseEntry")]
    public async Task OpenLdap_SaslBind_GssapiWithATicketFromTheTestKdc_Exits0WithTheBaseEntry()
    {
        await RequireOpenLdapAsync();
        await using var kdc = await KerberosTestKdcOnLoopback.StartWithCredentialCacheAsync([$"ldap/{KerberosHost}"], TestContext.CancellationToken);
        using var accounts = await KerberosAccountsFile.WriteAsync(TestContext.CancellationToken);
        var port = FreeLoopbackPort();

        await JudgeAsync(
            Sealed(OpenLdapCase(
                Named(),
                [
                    "--resolve", $"{KerberosHost}:{port}:127.0.0.1", "--login-options", "AUTH=GSSAPI",
                    "-u", $"{KerberosTestKdcOnLoopback.UserPrincipal}:", $"ldap://{KerberosHost}:{port}/{LdapConformanceDirectory.BaseDn}",
                ],
                ["--auth", "gssapi", "--keytab", kdc.KeytabPath, "--user-file", accounts.Path],
                ExitsZeroWriting(OpenLdapBaseEntry))),
            port,
            kdc.ClientEnvironment);
    }

    // --- The case, the mirrored assertions and the judgement ---

    private static CurlPortMeasurementCase WinLdapCase(
        (string Name, string Mirrors) test,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> surlOptions,
        Func<CurlPortMeasuredRun, string?> check,
        string scheme = "ldap") =>
        Case(test, WinLdap, scheme, ["-m", "20", .. arguments], surlOptions, check);

    private static CurlPortMeasurementCase OpenLdapCase(
        (string Name, string Mirrors) test,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> surlOptions,
        Func<CurlPortMeasuredRun, string?> check,
        string scheme = "ldap") =>
        Case(test, OpenLdap, scheme, ["-m", "20", .. arguments], surlOptions, check);

    private static CurlPortMeasurementCase Case(
        (string Name, string Mirrors) test,
        string mirroredClass,
        string scheme,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> surlOptions,
        Func<CurlPortMeasuredRun, string?> check) =>
        new(test.Name, $"{mirroredClass}.{test.Mirrors}", "ldap", ["-sS", .. arguments], scheme, surlOptions, DirectoryFiles, check);

    /// <summary>
    /// The case with the standard error line the mirrored test pins compared (ADR-0089 decision 4).
    /// </summary>
    private static CurlPortMeasurementCase Failing(CurlPortMeasurementCase measurementCase, string assertedStandardError) =>
        measurementCase with { AssertedStandardErrorFragments = [assertedStandardError] };

    /// <summary>
    /// The case without its client bytes compared: the bind seals the conversation, carries a random
    /// challenge, or StartTLS upgrades it, so the bytes are the security library's, not curl's.
    /// </summary>
    private static CurlPortMeasurementCase Sealed(CurlPortMeasurementCase measurementCase) =>
        measurementCase with { RecordsClientBytes = false };

    /// <summary>
    /// The case's name, the first column of the findings file: the test method's name, followed by its
    /// data row in brackets when it has one; and the mirrored method, the name after its first <c>_</c>.
    /// </summary>
    private static (string Name, string Mirrors) Named(string? dataRow = null, [CallerMemberName] string method = "") =>
        (dataRow is null ? method : $"{method}[{dataRow}]", method[(method.IndexOf('_', StringComparison.Ordinal) + 1)..]);

    private static string[] SaslInitialResponse(bool initialResponse) => initialResponse ? ["--sasl-ir"] : [];

    private static string Describe(CurlPortMeasuredRun run) =>
        $"exit {run.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "timeout"}, {run.StandardOutput.Length} bytes out, stderr: {run.StandardError.Trim()}";

    private static Func<CurlPortMeasuredRun, string?> Exits(int exitCode) =>
        run => run.ExitCode == exitCode ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> ExitsZeroWriting(string expected) =>
        run => run.ExitCode == 0 && Encoding.UTF8.GetString(run.StandardOutput) == expected ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> ExitsWithError(int exitCode, string standardErrorFragment) =>
        run => run.ExitCode == exitCode && run.StandardError.Contains(standardErrorFragment, StringComparison.Ordinal) ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> ExitsWithErrorWritingNothing(int exitCode, string standardErrorFragment) =>
        run => ExitsWithError(exitCode, standardErrorFragment)(run) ?? (run.StandardOutput.Length == 0 ? null : Describe(run));

    private static Func<CurlPortMeasuredRun, string?> ExitsWritingNothing(int exitCode) =>
        run => run.ExitCode == exitCode && run.StandardOutput.Length == 0 ? null : Describe(run);

    private static string? ExitsZeroWritingTheFirstTenThousandEntries(CurlPortMeasuredRun run)
    {
        var dnLines = Encoding.UTF8.GetString(run.StandardOutput)
            .Split('\n')
            .Where(line => line.StartsWith("DN: ", StringComparison.Ordinal))
            .ToList();
        return run.ExitCode == 0
            && dnLines.Count == 10000
            && dnLines[0] == "DN: cn=entry0,ou=many,dc=example,dc=com"
            && dnLines[^1] == "DN: cn=entry9999,ou=many,dc=example,dc=com"
            ? null
            : $"{Describe(run)}; {dnLines.Count} DN lines";
    }

    // A port the system just handed out and released; each run's surl binds it in turn.
    private static int FreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Inconclusive, as the mirrored OpenLDAP test is, where no upstream curl linked against OpenLDAP is
    /// pinned for the platform (on Windows and macOS today).
    /// </summary>
    private Task RequireOpenLdapAsync() => PinnedUpstreamCurl.RequireBuildLinkedAgainstAsync(TestContext, OpenLdapLibrary);

    private async Task JudgeOpenLdapAsync(CurlPortMeasurementCase measurementCase)
    {
        await RequireOpenLdapAsync();
        await JudgeAsync(measurementCase);
    }

    private async Task JudgeAsync(CurlPortMeasurementCase measurementCase, int surlPort = 0, IReadOnlyDictionary<string, string>? clientEnvironment = null)
    {
        var measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureOnPortAsync(
            TestContext, measurementCase, surlPort, clientEnvironment ?? new Dictionary<string, string>());
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
}
