namespace Surl.Conformance;

/// <summary>
/// Records the <c>SSH_MSG_KEXINIT</c> the pinned upstream curl build sends for <c>sftp://</c>,
/// the lists surl's offer (ADR-0051 decision 2) is negotiated against, and pins each platform's
/// in its own test: the Windows build's WinCNG lists ADR-0051 measured, and the Linux and macOS
/// builds' OpenSSL lists (BL-172), which the supplementary OpenSSL Windows build also offers
/// (ADR-0063). A failing run writes the lists it recorded to the test's log. Inconclusive where no
/// pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlOffersSshAlgorithmsTests
{
    private const string WinCngKex =
        "diffie-hellman-group-exchange-sha256,diffie-hellman-group16-sha512,diffie-hellman-group18-sha512,"
        + "diffie-hellman-group14-sha256,diffie-hellman-group14-sha1,diffie-hellman-group1-sha1,"
        + "diffie-hellman-group-exchange-sha1,ext-info-c,kex-strict-c-v00@openssh.com";

    private const string WinCngHostKey =
        "rsa-sha2-512,rsa-sha2-256,rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com,"
        + "ssh-rsa,ssh-rsa-cert-v01@openssh.com";

    private const string WinCngCipher =
        "chacha20-poly1305@openssh.com,aes256-ctr,aes192-ctr,aes128-ctr,aes256-cbc,rijndael-cbc@lysator.liu.se,"
        + "aes192-cbc,aes128-cbc,arcfour128,arcfour,3des-cbc";

    private const string WinCngMac =
        "hmac-sha2-256,hmac-sha2-256-etm@openssh.com,hmac-sha2-512,hmac-sha2-512-etm@openssh.com,hmac-sha1,"
        + "hmac-sha1-etm@openssh.com,hmac-sha1-96,hmac-md5,hmac-md5-96";

    private const string OpenSslKex =
        "curve25519-sha256,curve25519-sha256@libssh.org,ecdh-sha2-nistp256,ecdh-sha2-nistp384,ecdh-sha2-nistp521,"
        + WinCngKex;

    private const string OpenSslHostKey =
        "ecdsa-sha2-nistp256,ecdsa-sha2-nistp384,ecdsa-sha2-nistp521,ecdsa-sha2-nistp256-cert-v01@openssh.com,"
        + "ecdsa-sha2-nistp384-cert-v01@openssh.com,ecdsa-sha2-nistp521-cert-v01@openssh.com,ssh-ed25519,"
        + "ssh-ed25519-cert-v01@openssh.com," + WinCngHostKey;

    private const string OpenSslCipher =
        "chacha20-poly1305@openssh.com,aes256-gcm@openssh.com,aes128-gcm@openssh.com,aes256-ctr,aes192-ctr,aes128-ctr,"
        + "aes256-cbc,rijndael-cbc@lysator.liu.se,aes192-cbc,aes128-cbc,blowfish-cbc,arcfour128,arcfour,cast128-cbc,3des-cbc";

    private const string OpenSslMac = WinCngMac + ",hmac-ripemd160,hmac-ripemd160@openssh.com";

    // stunnel/static-curls Windows build of upstream curls tag 8.21.0 on OpenSSL 4.0.1: the one
    // pinned Windows build that offers the OpenSSL-only SSH algorithms (ADR-0063).
    private const string OpenSslWindowsBuildSha256 = "589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task KexInit_WindowsBuild_ListsTheWinCngAlgorithms()
    {
        var (identification, nameLists) = await RecordKexInitAsync();

        Assert.AreEqual("SSH-2.0-libssh2_1.11.1", identification);
        CollectionAssert.AreEqual(
            (string[])[WinCngKex, WinCngHostKey, WinCngCipher, WinCngCipher, WinCngMac, WinCngMac, "none", "none", string.Empty, string.Empty],
            nameLists.ToArray(),
            string.Join('\n', nameLists));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task KexInit_LinuxAndMacOSBuilds_ListTheOpenSslAlgorithms()
    {
        var (identification, nameLists) = await RecordKexInitAsync();

        Assert.AreEqual("SSH-2.0-libssh2_1.11.1", identification);
        CollectionAssert.AreEqual(
            (string[])[OpenSslKex, OpenSslHostKey, OpenSslCipher, OpenSslCipher, OpenSslMac, OpenSslMac, "none", "none", string.Empty, string.Empty],
            nameLists.ToArray(),
            string.Join('\n', nameLists));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms()
    {
        var (identification, nameLists) = await RecordKexInitAsync(
            (environment, arguments) => PinnedUpstreamCurl.RunSupplementaryBuildWithEnvironmentAsync(
                TestContext, OpenSslWindowsBuildSha256, environment, arguments));

        Assert.AreEqual("SSH-2.0-libssh2_1.11.1", identification);
        CollectionAssert.AreEqual(
            (string[])[OpenSslKex, OpenSslHostKey, OpenSslCipher, OpenSslCipher, OpenSslMac, OpenSslMac, "none", "none", string.Empty, string.Empty],
            nameLists.ToArray(),
            string.Join('\n', nameLists));
    }

    [TestMethod]
    public async Task KexInit_CompressedSsh_ListsZlibBeforeNone()
    {
        var (_, nameLists) = await RecordKexInitAsync("--compressed-ssh");

        Assert.AreEqual("zlib,zlib@openssh.com,none", nameLists[6], string.Join('\n', nameLists));
        Assert.AreEqual("zlib,zlib@openssh.com,none", nameLists[7], string.Join('\n', nameLists));
    }

    private Task<(string Identification, IReadOnlyList<string> NameLists)> RecordKexInitAsync(params string[] options) =>
        RecordKexInitAsync(
            (environment, arguments) => PinnedUpstreamCurl.RunWithEnvironmentAsync(TestContext, environment, arguments),
            options);

    private async Task<(string Identification, IReadOnlyList<string> NameLists)> RecordKexInitAsync(
        Func<IReadOnlyDictionary<string, string>, string[], Task<UpstreamCurlRunResult>> runCurl, params string[] options)
    {
        using var curlHome = new IsolatedCurlHome();
        using var recorder = ClientKexInitRecorder.Start();
        var recording = recorder.RecordAsync(TestContext.CancellationToken);

        var curl = runCurl(curlHome.Environment, ["-sS", "-k", .. options, $"sftp://127.0.0.1:{recorder.Port}/x"]);
        await Task.WhenAny(recording, curl);
        if (!recording.IsCompleted)
        {
            await curl;
            Assert.Fail("curl exited before it sent its KEXINIT.");
        }

        var recorded = await recording;
        recorder.Dispose();
        await curl;
        TestContext.WriteLine($"{UpstreamCurlLocator.CurrentPlatform}'s build: {recorded.Identification}\n{string.Join('\n', recorded.NameLists)}");
        return recorded;
    }
}
