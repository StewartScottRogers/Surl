namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build logs in to a live, in-process <c>surl sftp://</c> and
/// <c>scp://</c> the ways ADR-0051 and BL-172 list - a password, a public key, a refused login,
/// each way curl checks the host key, <c>--compressed-ssh</c>, and one run per host-key type
/// with only that key served - and each run writes the algorithms surl's
/// <c>SSH negotiated</c> note says it agreed to the test's log. curl runs with <c>HOME</c> and
/// <c>USERPROFILE</c> in a temporary directory (ADR-0051 decision 8). Inconclusive where no
/// pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlLogsInToSurlOverSshTests
{
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
    [DataRow("sftp")]
    [DataRow("scp")]
    public async Task Login_Password_DownloadsTheFile(string scheme)
    {
        await using var surl = await StartSurlAsync(scheme, "--throwaway-hostkey", "--user", Account);

        var result = await RunCurlAsync(surl, "--hostpubsha256", Sha256(surl), "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
        StringAssert.Contains(surl.Log, $"SSH login request: password for {AccountsFile.User}");
        WriteNegotiatedAlgorithms(surl);
    }

    [TestMethod]
    [DataRow("sftp")]
    [DataRow("scp")]
    public async Task Login_PublicKey_DownloadsTheFile(string scheme)
    {
        var (privateKey, publicKey) = SshTestKeys.RsaUserKeyPair();
        var keyFile = await WriteTextAsync("id_test", privateKey);
        var publicKeyFile = await WriteTextAsync("id_test.pub", publicKey + "\n");
        var authorizedKeys = await WriteTextAsync("authorized_keys", publicKey + "\n");
        await using var surl = await StartSurlAsync(
            scheme, "--throwaway-hostkey", "--authorized-keys", $"{AccountsFile.User}:{authorizedKeys}");

        var result = await RunCurlAsync(
            surl,
            "--hostpubsha256",
            Sha256(surl),
            "-u",
            $"{AccountsFile.User}:",
            "--key",
            keyFile,
            "--pubkey",
            publicKeyFile,
            surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
        StringAssert.Contains(surl.Log, $"SSH login request: publickey for {AccountsFile.User}, key ssh-rsa");
        WriteNegotiatedAlgorithms(surl);
    }

    [TestMethod]
    [DataRow("sftp")]
    [DataRow("scp")]
    public async Task Login_WrongPassword_Exits67LoginDenied(string scheme)
    {
        await using var surl = await StartSurlAsync(scheme, "--throwaway-hostkey", "--user", Account);

        var result = await RunCurlAsync(
            surl, "--hostpubsha256", Sha256(surl), "-u", $"{AccountsFile.User}:wrong", surl.UrlOf("a.txt"));

        Assert.AreEqual(67, result.ExitCode, result.StandardError);
        Assert.AreEqual("curl: (67) Login denied", result.StandardError.Trim());
        Assert.AreEqual(0, result.StandardOutput.Length);
    }

    [TestMethod]
    public async Task HostKey_PinnedByMd5_DownloadsTheFile()
    {
        await using var surl = await StartSurlAsync("sftp", "--throwaway-hostkey", "--user", Account);

        var result = await RunCurlAsync(
            surl, "--hostpubmd5", SshLogNotes.HostKeyMd5(surl.Log, "ssh-rsa"), "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
    }

    [TestMethod]
    public async Task HostKey_CheckSkipped_DownloadsTheFile()
    {
        await using var surl = await StartSurlAsync("sftp", "--throwaway-hostkey", "--user", Account);

        var result = await RunCurlAsync(surl, "-k", "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
    }

    [TestMethod]
    public async Task HostKey_OtherSha256Pinned_Exits60()
    {
        await using var surl = await StartSurlAsync("sftp", "--throwaway-hostkey", "--user", Account);

        const string otherSha256 = "47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=";

        var result = await RunCurlAsync(surl, "--hostpubsha256", otherSha256, "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(60, result.ExitCode, result.StandardError);
        StringAssert.StartsWith(
            result.StandardError,
            $"curl: (60) Denied establishing ssh session: mismatch SHA256 fingerprint. Remote {Sha256(surl)} is not equal to {otherSha256}");
    }

    [TestMethod]
    public async Task HostKey_NoPinAndNoKnownHostsFile_Exits2()
    {
        await using var surl = await StartSurlAsync("sftp", "--throwaway-hostkey", "--user", Account);

        var result = await RunCurlAsync(surl, "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(2, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task HostKey_NotInKnownHosts_Exits60()
    {
        await using var surl = await StartSurlAsync("sftp", "--throwaway-hostkey", "--user", Account);
        var knownHosts = await WriteTextAsync("known_hosts", string.Empty);

        var result = await RunCurlAsync(surl, "--knownhosts", knownHosts, "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(60, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    public async Task HostKey_DifferentKeyInKnownHosts_Exits60()
    {
        await using var surl = await StartSurlAsync("sftp", "--throwaway-hostkey", "--user", Account);
        var knownHosts = await WriteTextAsync(
            "known_hosts", $"[127.0.0.1]:{surl.BaseUrl.Port} {SshTestKeys.OtherRsaPublicKeyLine()}\n");

        var result = await RunCurlAsync(surl, "--knownhosts", knownHosts, "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(60, result.ExitCode, result.StandardError);
    }

    [TestMethod]
    [DataRow("sftp")]
    [DataRow("scp")]
    public async Task Login_CompressedSsh_NegotiatesZlibAndDownloadsTheFile(string scheme)
    {
        await using var surl = await StartSurlAsync(scheme, "--throwaway-hostkey", "--user", Account);

        var result = await RunCurlAsync(
            surl, "--compressed-ssh", "--hostpubsha256", Sha256(surl), "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
        StringAssert.Contains(WriteNegotiatedAlgorithms(surl), "compression zlib");
    }

    [TestMethod]
    public async Task HostKey_OnlyRsa_NegotiatesRsaAndDownloadsTheFile()
    {
        await using var surl = await StartWithOnlyHostKeyPemAsync(SshTestKeys.RsaHostKeyPem());

        var result = await RunCurlAsync(surl, "--hostpubsha256", Sha256(surl, "ssh-rsa"), "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
        StringAssert.Contains(WriteNegotiatedAlgorithms(surl), "host key rsa-sha2-");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("ecdsa-sha2-nistp256")]
    [DataRow("ssh-ed25519")]
    public async Task HostKey_OnlyEllipticCurveOnOpenSslBuilds_NegotiatesItAndDownloadsTheFile(string keyType)
    {
        await using var surl = await StartWithOnlyHostKeyAsync(keyType);

        var result = await RunCurlAsync(surl, "--hostpubsha256", Sha256(surl, keyType), "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(HelloWorld, result.StandardOutput);
        StringAssert.Contains(WriteNegotiatedAlgorithms(surl), $"host key {keyType},");
    }

    // The Windows build's WinCNG backend lists only RSA host-key algorithms (ADR-0051, Context),
    // so a surl holding only an elliptic-curve key has none in common with it.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("ecdsa-sha2-nistp256")]
    [DataRow("ssh-ed25519")]
    public async Task HostKey_OnlyEllipticCurveOnTheWindowsBuild_Exits2NoCommonHostKeyAlgorithm(string keyType)
    {
        await using var surl = await StartWithOnlyHostKeyAsync(keyType);

        var result = await RunCurlAsync(surl, "--hostpubsha256", Sha256(surl, keyType), "-u", Account, surl.UrlOf("a.txt"));

        Assert.AreEqual(2, result.ExitCode, result.StandardError);
        Assert.AreEqual(
            "curl: (2) Failure establishing ssh session: -5, Unable to exchange encryption keys", result.StandardError.Trim());
        await WaitForLogAsync(surl, "SSH no common host key algorithm; client offered rsa-sha2-512,");
    }

    private async Task WaitForLogAsync(SurlOnLoopback surl, string text)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while (!surl.Log.Contains(text, StringComparison.Ordinal))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), deadline.Token);
        }
    }

    private async Task<SurlOnLoopback> StartWithOnlyHostKeyAsync(string keyType)
    {
        var pem = keyType == "ssh-ed25519" ? SshTestKeys.Ed25519HostKeyPem() : SshTestKeys.EcdsaHostKeyPem();
        return await StartWithOnlyHostKeyPemAsync(pem);
    }

    private async Task<SurlOnLoopback> StartWithOnlyHostKeyPemAsync(string pem)
    {
        var hostKey = await WriteTextAsync("host_key.pem", pem);
        return await StartSurlAsync("sftp", "--hostkey", hostKey, "--user", Account);
    }

    private static string Sha256(SurlOnLoopback surl, string keyType = "ssh-rsa") => SshLogNotes.HostKeySha256(surl.Log, keyType);

    private string WriteNegotiatedAlgorithms(SurlOnLoopback surl)
    {
        var notes = string.Join('\n', SshLogNotes.NegotiatedAlgorithms(surl.Log));
        TestContext.WriteLine($"surl negotiated with {UpstreamCurlLocator.CurrentPlatform}'s build: {notes}");
        return notes;
    }

    private async Task<string> WriteTextAsync(string name, string text) =>
        await curlHome.WriteFileAsync(name, System.Text.Encoding.UTF8.GetBytes(text), TestContext.CancellationToken);

    private Task<SurlOnLoopback> StartSurlAsync(string scheme, params string[] options) =>
        SurlOnLoopback.StartAsync(scheme, ServedFiles, [], ["-v", .. options], TestContext.CancellationToken);

    private Task<UpstreamCurlRunResult> RunCurlAsync(SurlOnLoopback surl, params string[] arguments) =>
        PinnedUpstreamCurlOverSsh.RunAsync(TestContext, curlHome, surl, arguments);
}
