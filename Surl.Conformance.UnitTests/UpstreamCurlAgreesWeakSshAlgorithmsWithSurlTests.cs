namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build agrees each of the four OpenSSL-only SSH algorithms ADR-0061 has
/// surl offer behind <c>--allow-weak-ssh-algorithms</c> - <c>blowfish-cbc</c>, <c>cast128-cbc</c>,
/// <c>hmac-ripemd160</c> and <c>hmac-ripemd160@openssh.com</c> - with a live, in-process
/// <c>surl sftp://</c> and downloads a file over it. libssh2 takes the first name in its own list
/// that surl offers, so surl narrows its offer to the one name with <c>--ssh-ciphers</c> or
/// <c>--ssh-macs</c> (ADR-0066); a MAC case also narrows the cipher to <c>aes128-ctr</c>, because
/// an AEAD cipher would leave the MAC unused. On Windows the build is the supplementary OpenSSL
/// static-curl pin, the only Windows pin offering the four (ADR-0063); on Linux and macOS it is
/// the reference pin, the same curl, libssh2 and OpenSSL. The cipher cases run curl with
/// <c>OPENSSL_CONF</c> naming a file that activates OpenSSL's <c>default</c> and <c>legacy</c>
/// providers, since OpenSSL 4 keeps Blowfish and CAST-128 in <c>legacy</c>, which libssh2 never
/// loads (ADR-0063 decision 3); the MAC cases run without it. curl runs with <c>HOME</c> and
/// <c>USERPROFILE</c> in a temporary directory (ADR-0051 decision 8). Inconclusive where the pin
/// is not installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests
{
    // stunnel/static-curl's Windows build of upstream curl's tag 8.21.0 on OpenSSL 4.0.1 (ADR-0063).
    private const string OpenSslWindowsBuildSha256 = "589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648";

    private const string LegacyProviderConfiguration =
        "openssl_conf = openssl_init\n"
        + "[openssl_init]\nproviders = provider_sect\n"
        + "[provider_sect]\ndefault = default_sect\nlegacy = legacy_sect\n"
        + "[default_sect]\nactivate = 1\n"
        + "[legacy_sect]\nactivate = 1\n";

    private static readonly byte[] HelloWorld = "hello world\n"u8.ToArray();
    private static readonly Dictionary<string, byte[]> ServedFiles = new() { ["a.txt"] = HelloWorld };
    private static readonly string Account = $"{AccountsFile.User}:{AccountsFile.Password}";

    private IsolatedCurlHome curlHome = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateCurlHome() => curlHome = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteCurlHome() => curlHome.Dispose();

    [TestMethod]
    [DataRow("blowfish-cbc")]
    [DataRow("cast128-cbc")]
    public async Task Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile(string cipher)
    {
        await using var surl = await StartSurlAsync("--ssh-ciphers", cipher);
        var configuration = await curlHome.WriteFileAsync(
            "openssl.cnf", System.Text.Encoding.UTF8.GetBytes(LegacyProviderConfiguration), TestContext.CancellationToken);
        var environment = new Dictionary<string, string>(curlHome.Environment) { ["OPENSSL_CONF"] = configuration };

        var result = await RunCurlAsync(surl, environment);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
        StringAssert.Contains(NegotiatedAlgorithms(surl), $"cipher {cipher}/{cipher},");
    }

    [TestMethod]
    [DataRow("hmac-ripemd160")]
    [DataRow("hmac-ripemd160@openssh.com")]
    public async Task Download_MacOnlySurlOffers_AgreesItAndWritesTheFile(string mac)
    {
        await using var surl = await StartSurlAsync("--ssh-ciphers", "aes128-ctr", "--ssh-macs", mac);

        var result = await RunCurlAsync(surl, curlHome.Environment);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
        StringAssert.Contains(NegotiatedAlgorithms(surl), $"MAC {mac}/{mac},");
    }

    private string NegotiatedAlgorithms(SurlOnLoopback surl)
    {
        var notes = string.Join('\n', SshLogNotes.NegotiatedAlgorithms(surl.Log));
        TestContext.WriteLine($"surl negotiated with {UpstreamCurlLocator.CurrentPlatform}'s OpenSSL build: {notes}");
        return notes;
    }

    private Task<SurlOnLoopback> StartSurlAsync(params string[] options) =>
        SurlOnLoopback.StartAsync(
            "sftp",
            ServedFiles,
            [],
            ["-v", "--throwaway-hostkey", "--user", Account, "--allow-weak-ssh-algorithms", .. options],
            TestContext.CancellationToken);

    private async Task<UpstreamCurlRunResult> RunCurlAsync(SurlOnLoopback surl, IReadOnlyDictionary<string, string> environment)
    {
        string[] arguments =
            ["-sS", "--hostpubsha256", SshLogNotes.HostKeySha256(surl.Log, "ssh-rsa"), "-u", Account, surl.UrlOf("a.txt")];
        var result = OperatingSystem.IsWindows()
            ? await PinnedUpstreamCurl.RunSupplementaryBuildWithEnvironmentAsync(
                TestContext, OpenSslWindowsBuildSha256, environment, arguments)
            : await PinnedUpstreamCurl.RunWithEnvironmentAsync(TestContext, environment, arguments);
        if (result.ExitCode != 0)
        {
            TestContext.WriteLine($"curl's stderr: {result.StandardError}");
            TestContext.WriteLine($"surl's log:\n{surl.Log}");
        }

        return result;
    }
}
