namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build downloads over <c>sftp://</c> from a live, in-process
/// <c>surl --hostkey &lt;key&gt; --hostcert &lt;key&gt;-cert.pub</c>, trusting the certificate's CA
/// through a <c>@cert-authority</c> line in its <c>--knownhosts</c> file and given neither
/// <c>-k</c> nor <c>--hostpubsha256</c>, and is refused with exit 60, as measured (ADR-0068):
/// libssh2 1.11.1 lists each plain host-key name before its certificate name, so it agrees the
/// plain key, and it reads no <c>@cert-authority</c> line, so curl finds no <c>known_hosts</c>
/// entry for the host. The key and certificate are <see cref="SshTestHostCertificates"/>'s: RSA
/// on Windows, whose WinCNG build lists no Ed25519 name, and Ed25519 on the OpenSSL builds - the
/// Linux and macOS reference pins and, on Windows, the supplementary static-curl pin (ADR-0063).
/// With the plain RSA key pinned by <c>--hostpubsha256</c> instead, the download completes.
/// curl runs with <c>HOME</c> and <c>USERPROFILE</c> in a temporary directory (ADR-0051 decision 8).
/// Inconclusive where the pin is not installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlTrustsSurlsHostCertificateTests
{
    // stunnel/static-curl's Windows build of upstream curl's tag 8.21.0 on OpenSSL 4.0.1 (ADR-0063).
    private const string OpenSslWindowsBuildSha256 = "589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648";

    private const string RemoteKeyNotOk = "curl: (60) SSL peer certificate or SSH remote key was not OK";

    private static readonly Dictionary<string, byte[]> ServedFiles = new() { ["a.txt"] = "hello world\n"u8.ToArray() };
    private static readonly string Account = $"{AccountsFile.User}:{AccountsFile.Password}";

    private IsolatedCurlHome curlHome = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateCurlHome() => curlHome = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteCurlHome() => curlHome.Dispose();

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task Download_RsaHostCertificateOnTheWindowsBuild_AgreesThePlainKeyAndExits60()
    {
        await using var surl = await StartSurlAsync(SshTestHostCertificates.RsaHostKey, SshTestHostCertificates.RsaHostCertificate);
        var knownHosts = await WriteCertificateAuthorityKnownHostsAsync(surl);

        var result = await PinnedUpstreamCurlOverSsh.RunAsync(TestContext, curlHome, surl, CurlArguments(surl, knownHosts));

        AssertRefusedAfterAgreeing(surl, result, "rsa-sha2-512");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task Download_Ed25519HostCertificateOnTheOpenSslWindowsBuild_AgreesThePlainKeyAndExits60()
    {
        await using var surl = await StartSurlAsync(SshTestHostCertificates.Ed25519HostKey, SshTestHostCertificates.Ed25519HostCertificate);
        var knownHosts = await WriteCertificateAuthorityKnownHostsAsync(surl);

        var result = await PinnedUpstreamCurl.RunSupplementaryBuildWithEnvironmentAsync(
            TestContext, OpenSslWindowsBuildSha256, curlHome.Environment, ["-sS", .. CurlArguments(surl, knownHosts)]);

        AssertRefusedAfterAgreeing(surl, result, "ssh-ed25519");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task Download_Ed25519HostCertificateOnOpenSslBuilds_AgreesThePlainKeyAndExits60()
    {
        await using var surl = await StartSurlAsync(SshTestHostCertificates.Ed25519HostKey, SshTestHostCertificates.Ed25519HostCertificate);
        var knownHosts = await WriteCertificateAuthorityKnownHostsAsync(surl);

        var result = await PinnedUpstreamCurlOverSsh.RunAsync(TestContext, curlHome, surl, CurlArguments(surl, knownHosts));

        AssertRefusedAfterAgreeing(surl, result, "ssh-ed25519");
    }

    [TestMethod]
    public async Task Download_RsaHostCertificateWithThePlainKeyPinned_DownloadsTheFile()
    {
        await using var surl = await StartSurlAsync(SshTestHostCertificates.RsaHostKey, SshTestHostCertificates.RsaHostCertificate);

        var result = await PinnedUpstreamCurlOverSsh.RunAsync(
            TestContext,
            curlHome,
            surl,
            "--hostpubsha256",
            SshLogNotes.HostKeySha256(surl.Log, "ssh-rsa"),
            "-u",
            Account,
            surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(ServedFiles["a.txt"], result.StandardOutput);
    }

    private void AssertRefusedAfterAgreeing(SurlOnLoopback surl, UpstreamCurlRunResult result, string plainHostKeyAlgorithm)
    {
        Assert.AreEqual(60, result.ExitCode, result.StandardError);
        Assert.AreEqual(0, result.StandardOutput.Length);
        Assert.AreEqual(RemoteKeyNotOk, result.StandardError.Split('\n')[0].TrimEnd('\r'), result.StandardError);
        var negotiated = string.Join('\n', SshLogNotes.NegotiatedAlgorithms(surl.Log));
        TestContext.WriteLine($"surl negotiated: {negotiated}");
        StringAssert.Contains(negotiated, $"host key {plainHostKeyAlgorithm},");
    }

    private static string[] CurlArguments(SurlOnLoopback surl, string knownHosts) =>
        ["--knownhosts", knownHosts, "-u", Account, surl.UrlOf("a.txt")];

    private Task<string> WriteCertificateAuthorityKnownHostsAsync(SurlOnLoopback surl) =>
        WriteTextAsync(
            "known_hosts",
            $"@cert-authority [127.0.0.1]:{surl.BaseUrl.Port} {SshTestHostCertificates.CertificateAuthorityPublicKey}\n");

    private async Task<SurlOnLoopback> StartSurlAsync(string hostKeyText, string certificateText)
    {
        var hostKey = await WriteTextAsync("host_key", hostKeyText);
        var certificate = await WriteTextAsync("host_key-cert.pub", certificateText);
        return await SurlOnLoopback.StartAsync(
            "sftp",
            ServedFiles,
            [],
            ["-v", "--hostkey", hostKey, "--hostcert", certificate, "--user", Account],
            TestContext.CancellationToken);
    }

    private async Task<string> WriteTextAsync(string name, string text) =>
        await curlHome.WriteFileAsync(name, System.Text.Encoding.UTF8.GetBytes(text), TestContext.CancellationToken);
}
