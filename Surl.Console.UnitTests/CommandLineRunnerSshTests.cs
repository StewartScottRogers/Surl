using System.Text;
using System.Text.RegularExpressions;
using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// The SSH server's composition (ADR-0051 decision 13, BL-171): <c>scp</c> and <c>sftp</c> listen
/// URLs served by it, the <c>--hostkey</c>, <c>--hostcert</c> and <c>--authorized-keys</c> files read before any
/// listener binds with decisions 4 and 6's refusals, and the <c>--throwaway-hostkey</c> warning and
/// host-key notes of decisions 8 and 11.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerSshTests
{
    private const string Sftp = "sftp://127.0.0.1:0/";

    private const string Scp = "scp://127.0.0.1:0/";

    private const string Http = "http://127.0.0.1:0/";

    private const string HostKey = "host.key";

    private const string HostCertificate = "host-cert.pub";

    private const string AliceKeys = "alice.keys";

    private static readonly string NewLine = Environment.NewLine;

    public TestContext TestContext { get; set; } = null!;

    // Registration.

    [TestMethod]
    public async Task RunAsync_ScpAndSftpListenUrls_StartsTheirListenersAndTheSshServerAnswersAConnection()
    {
        var connection = new FakeConnection("SSH-1.5-old\r\n"u8.ToArray());
        var factory = new FakeListenerFactory { Connection = connection };
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = CreateRunner(factory, Files((HostKey, TestSshKeyFiles.Rsa2048)))
            .RunAsync(["--hostkey", HostKey, Scp, Sftp], output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        CollectionAssert.AreEqual(
            new[] { new ListenUrl("scp", "127.0.0.1", 0), new ListenUrl("sftp", "127.0.0.1", 0) }, factory.StartedListenUrls);
        Assert.AreEqual(
            $"Listening on scp://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine
            + $"Listening on sftp://127.0.0.1:{FakeListenerFactory.BoundPort}/" + NewLine,
            output.ToString());
        StringAssert.StartsWith(Encoding.ASCII.GetString(connection.WrittenBytes), "SSH-2.0-surl\r\n");
    }

    [TestMethod]
    public async Task RunAsync_Version_ListsScpAndSftp()
    {
        using var output = new StringWriter();

        await CreateRunner(new FakeListenerFactory(), Files()).RunAsync(["--version"], output, TextWriter.Null, TestContext.CancellationToken);

        StringAssert.Contains(output.ToString(), " scp sftp ");
    }

    // An scp or sftp listen URL with no host key (ADR-0051 decision 4).

    [TestMethod]
    [DataRow(Sftp, "sftp://127.0.0.1:0/")]
    [DataRow("scp://[::1]/", "scp://[::1]:22/")]
    public async Task RunAsync_SshListenUrlWithoutAHostKey_WritesNeedsAHostKeyAndReturnsFailedInitBeforeAnyListenerBinds(
        string listenUrl, string written)
    {
        var run = await RunRefusedAsync(Files(), Http, listenUrl, Sftp);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual(
            $"surl: (2) {written} needs a host key: give --hostkey <file>, or --throwaway-hostkey for a throwaway one" + NewLine,
            run.Error);
        Assert.IsFalse(run.FactoryCreated);
    }

    [TestMethod]
    public async Task RunAsync_SecureListenUrlWithoutACertificateAndSshWithoutAHostKey_NamesTheCertificateFirst()
    {
        var run = await RunRefusedAsync(Files(), Sftp, "https://127.0.0.1:0/");

        Assert.AreEqual(SurlExitCode.CertificateProblem, run.ExitCode);
    }

    // The --hostkey refusals (ADR-0051 decision 4).

    [TestMethod]
    [DataRow("missing", SurlExitCode.CouldNotReadFile, "(37) Could not read host key host.key", DisplayName = "Missing")]
    [DataRow("denied", SurlExitCode.CouldNotReadFile, "(37) Could not read host key host.key", DisplayName = "Unreadable")]
    [DataRow("not a key", SurlExitCode.FailedInit, "(2) Host key host.key: not a private key surl can read", DisplayName = "Not a key")]
    [DataRow("encrypted", SurlExitCode.FailedInit, "(2) Host key host.key: the key is encrypted; give --pass", DisplayName = "Encrypted, no --pass")]
    [DataRow("encrypted, wrong pass", SurlExitCode.FailedInit, "(2) Host key host.key: --pass does not decrypt the key", DisplayName = "Wrong --pass")]
    [DataRow("encrypted openssh", SurlExitCode.FailedInit, "(2) Host key host.key: the key is encrypted; give --pass", DisplayName = "Encrypted OpenSSH, no --pass")]
    [DataRow("rsa 1024", SurlExitCode.FailedInit, "(2) Host key host.key: RSA keys of 1024 bits need --allow-weak-ssh-algorithms", DisplayName = "Weak RSA")]
    [DataRow("x25519", SurlExitCode.FailedInit, "(2) Host key host.key: key type 1.3.101.110 is not supported", DisplayName = "Another key type")]
    public async Task RunAsync_HostKeyFileSurlCannotUse_WritesItsRefusalAndReturnsItsExitCodeBeforeAnyListenerBinds(
        string file, SurlExitCode exitCode, string message)
    {
        string[] pass = file == "encrypted, wrong pass" ? ["--pass", "wrong horse"] : [];

        var run = await RunRefusedAsync(ReadHostKeyAs(file), [.. pass, "--hostkey", HostKey, Sftp]);

        Assert.AreEqual(exitCode, run.ExitCode);
        Assert.AreEqual("surl: " + message + NewLine, run.Error);
        Assert.IsFalse(run.FactoryCreated);
    }

    [TestMethod]
    public async Task RunAsync_HostKeyFileWithoutAnSshListenUrl_IsStillReadAndRefused()
    {
        var run = await RunRefusedAsync(Files((HostKey, TestSshKeyFiles.NotAKey)), "--hostkey", HostKey, Http);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual("surl: (2) Host key host.key: not a private key surl can read" + NewLine, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_TwoHostKeysOfOneType_NamesTheFileThatGaveTheFirst()
    {
        var run = await RunRefusedAsync(
            Files(("first.key", TestSshKeyFiles.Rsa2048), ("ecdsa.key", TestSshKeyFiles.EcdsaP256), ("second.key", TestSshKeyFiles.Rsa2048Pkcs1)),
            "--hostkey", "first.key", "--hostkey", "ecdsa.key", "--hostkey", "second.key", Sftp);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual("surl: (2) Host key second.key: a ssh-rsa host key is already given by first.key" + NewLine, run.Error);
        Assert.IsFalse(run.FactoryCreated);
    }

    // --hostcert (ADR-0051 decision 4, BL-222).

    [TestMethod]
    public async Task RunAsync_HostCertificateOfAHostKey_IsOfferedBeforeItsKeyInTheServersKexInit()
    {
        var connection = new FakeConnection("SSH-2.0-libssh2_1.11.1\r\n"u8.ToArray());
        var factory = new FakeListenerFactory { Connection = connection };
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = CreateRunner(factory, Files((HostKey, TestSshCertificateFiles.Ed25519HostKey), (HostCertificate, TestSshCertificateFiles.Ed25519HostCertificate)))
            .RunAsync(["--hostkey", HostKey, "--hostcert", HostCertificate, Sftp], output, error, stop.Token);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        Assert.DoesNotContain("Host certificate", error.ToString());
        CollectionAssert.AreEqual(new[] { new ListenUrl("sftp", "127.0.0.1", 0) }, factory.StartedListenUrls);
        StringAssert.Contains(
            Encoding.Latin1.GetString(connection.WrittenBytes),
            "\0\0\0\x2cssh-ed25519-cert-v01@openssh.com,ssh-ed25519\0");
    }

    [TestMethod]
    [DataRow("missing", SurlExitCode.CouldNotReadFile, "(37) Could not read host certificate host-cert.pub", DisplayName = "Missing")]
    [DataRow("denied", SurlExitCode.CouldNotReadFile, "(37) Could not read host certificate host-cert.pub", DisplayName = "Unreadable")]
    [DataRow("not a certificate", SurlExitCode.FailedInit, "(2) Host certificate host-cert.pub: not an OpenSSH host certificate", DisplayName = "Not a certificate")]
    [DataRow("user certificate", SurlExitCode.FailedInit, "(2) Host certificate host-cert.pub: not an OpenSSH host certificate", DisplayName = "A user certificate")]
    [DataRow("another key's", SurlExitCode.FailedInit, "(2) Host certificate host-cert.pub: certifies no --hostkey key", DisplayName = "Another key's")]
    public async Task RunAsync_HostCertificateFileSurlCannotUse_WritesItsRefusalAndReturnsItsExitCodeBeforeAnyListenerBinds(
        string file, SurlExitCode exitCode, string message)
    {
        var run = await RunRefusedAsync(ReadHostCertificateAs(file), "--hostkey", HostKey, "--hostcert", HostCertificate, Sftp);

        Assert.AreEqual(exitCode, run.ExitCode);
        Assert.AreEqual("surl: " + message + NewLine, run.Error);
        Assert.IsFalse(run.FactoryCreated);
    }

    [TestMethod]
    public async Task RunAsync_HostCertificateWithoutAnyHostKey_IsStillReadAndCertifiesNoHostKey()
    {
        var run = await RunRefusedAsync(Files((HostCertificate, TestSshCertificateFiles.Ed25519HostCertificate)), "--hostcert", HostCertificate, Http);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual("surl: (2) Host certificate host-cert.pub: certifies no --hostkey key" + NewLine, run.Error);
        Assert.IsFalse(run.FactoryCreated);
    }

    [TestMethod]
    public async Task RunAsync_TwoHostCertificatesOfOneType_NamesTheFileThatGaveTheFirst()
    {
        var run = await RunRefusedAsync(
            Files(
                (HostKey, TestSshCertificateFiles.Ed25519HostKey),
                ("first-cert.pub", TestSshCertificateFiles.Ed25519HostCertificate),
                ("second-cert.pub", TestSshCertificateFiles.Ed25519HostCertificate)),
            "--hostkey", HostKey, "--hostcert", "first-cert.pub", "--hostcert", "second-cert.pub", Sftp);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual(
            "surl: (2) Host certificate second-cert.pub: a ssh-ed25519-cert-v01@openssh.com host certificate is already given by first-cert.pub" + NewLine,
            run.Error);
        Assert.IsFalse(run.FactoryCreated);
    }

    [TestMethod]
    public async Task RunAsync_EncryptedHostKeyWithItsPass_Serves()
    {
        var run = await ServeUntilListeningAsync(
            Files((HostKey, TestSshKeyFiles.EncryptedRsa2048)), "--pass", TestSshKeyFiles.Passphrase, "--hostkey", HostKey, Sftp);

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode, run.Error);
        Assert.AreEqual(string.Empty, run.Error);
    }

    // The --authorized-keys refusals (ADR-0051 decision 6).

    [TestMethod]
    [DataRow("ssh-ed25519\n", "line 1: expected <key type> <key>", DisplayName = "No key")]
    [DataRow("# keys\n\nfrom=\"10.0.0.1\" ssh-ed25519 AAAA\n", "line 3: key options are not supported", DisplayName = "Key options")]
    [DataRow("sk-ssh-ed25519@openssh.com AAAA\n", "line 1: key type sk-ssh-ed25519@openssh.com is not supported", DisplayName = "Unknown key type")]
    [DataRow("ssh-ed25519 !!!!\n", "line 1: the key is malformed", DisplayName = "Not base64")]
    [DataRow("ssh-ed25519 AAAA \xFF\n", "line 1: not UTF-8", DisplayName = "Not UTF-8")]
    public async Task RunAsync_AuthorizedKeysLineThatIsNotAKey_WritesTheLineAndReturnsFailedInitBeforeAnyListenerBinds(string content, string refusal)
    {
        var bytes = content.Select(character => (byte)character).ToArray();

        var run = await RunRefusedAsync(Files((AliceKeys, bytes)), "--authorized-keys", "alice:" + AliceKeys, "--throwaway-hostkey", Sftp);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual($"surl: (2) Authorized keys {AliceKeys}, {refusal}" + NewLine, run.Error);
        Assert.IsFalse(run.FactoryCreated);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Missing")]
    [DataRow(true, DisplayName = "Unreadable")]
    public async Task RunAsync_AuthorizedKeysFileThatCannotBeRead_WritesCouldNotReadAndReturnsCouldNotReadFile(bool denied)
    {
        Func<string, byte[]> read = denied ? _ => throw new UnauthorizedAccessException("denied") : Files();

        var run = await RunRefusedAsync(read, "--authorized-keys", "alice:" + AliceKeys, "--throwaway-hostkey", Sftp);

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, run.ExitCode);
        Assert.AreEqual($"surl: (37) Could not read authorized keys {AliceKeys}" + NewLine, run.Error);
        Assert.IsFalse(run.FactoryCreated);
    }

    [TestMethod]
    public async Task Compose_AuthorizedKeys_ThePolicyFindsEachUsersKeyAcceptable()
    {
        var file = Encoding.ASCII.GetBytes(TestSshKeyFiles.Ed25519AuthorizedKeyLine + "\n");
        var commandLine = CommandLineParser.Parse(["--authorized-keys", "alice:" + AliceKeys, Sftp]).CommandLine!;
        var blob = Convert.FromBase64String(TestSshKeyFiles.Ed25519AuthorizedKeyLine.Split(' ')[1]);

        var (policy, _, _, _, _) = AuthenticationComposition.Compose(commandLine, Files((AliceKeys, file)), TimeProvider.System);
        var alice = await policy!.CheckSshPublicKeyLoginAsync(
            new SshPublicKeyLogin("alice", "ssh-ed25519", blob, SshPublicKeyProof.None), TestContext.CancellationToken);
        var bob = await policy.CheckSshPublicKeyLoginAsync(
            new SshPublicKeyLogin("bob", "ssh-ed25519", blob, SshPublicKeyProof.None), TestContext.CancellationToken);

        Assert.AreEqual(SshLoginOutcome.KeyAcceptable, alice.Outcome);
        Assert.AreEqual(SshLoginOutcome.Refused, bob.Outcome);
    }

    // The --throwaway-hostkey warning and the host-key notes (ADR-0051 decisions 8 and 11).

    [TestMethod]
    public async Task RunAsync_VerboseThrowawayHostKey_WritesTheWarningThenTheKeysNoteWithTheSameHash()
    {
        var run = await ServeUntilListeningAsync(Files(), "-v", "--throwaway-hostkey", Sftp);

        var match = Regex.Match(
            run.Error,
            "^surl: warning: --throwaway-hostkey: serving a throwaway SSH host key \\(--hostpubsha256 ([A-Za-z0-9+/]{43}=)\\); "
            + "clients must pin it or skip the check \\(curl -k\\)" + NewLine
            + "\\* Serving SSH host key ssh-rsa, --hostpubsha256 ([A-Za-z0-9+/]{43}=) --hostpubmd5 [0-9a-f]{32}" + NewLine + "$");
        Assert.IsTrue(match.Success, run.Error);
        Assert.AreEqual(match.Groups[1].Value, match.Groups[2].Value);
    }

    [TestMethod]
    public async Task RunAsync_ThrowawayHostKeyAtTheInfoLevel_WritesTheWarningAlone()
    {
        var run = await ServeUntilListeningAsync(Files(), "--throwaway-hostkey", Scp);

        Assert.MatchesRegex("^surl: warning: --throwaway-hostkey: [^\r\n]*" + NewLine + "$", run.Error);
    }

    [TestMethod]
    public async Task RunAsync_SilentThrowawayHostKey_WritesNothing()
    {
        var run = await ServeUntilListeningAsync(Files(), "-s", "--throwaway-hostkey", Scp);

        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_ThrowawayHostKeyWithoutAnSshListenUrl_MakesNoKeyAndWritesNothing()
    {
        var run = await ServeUntilListeningAsync(Files(), "-v", "--throwaway-hostkey", Http);

        Assert.AreEqual(string.Empty, run.Error);
    }

    // --allow-weak-ssh-algorithms: the offer and the warning (ADR-0051 decisions 2 and 11, BL-250).

    [TestMethod]
    [DataRow(true, DisplayName = "With --allow-weak-ssh-algorithms")]
    [DataRow(false, DisplayName = "Without it")]
    public async Task RunAsync_AllowWeakSshAlgorithms_TheServersKexInitNamesTheWeakAlgorithmsOnlyWithIt(bool allowWeak)
    {
        var connection = new FakeConnection("SSH-2.0-libssh2_1.11.1\r\n"u8.ToArray());
        var factory = new FakeListenerFactory { Connection = connection };
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        string[] weak = allowWeak ? ["--allow-weak-ssh-algorithms"] : [];

        var running = CreateRunner(factory, Files((HostKey, TestSshKeyFiles.Rsa2048)))
            .RunAsync([.. weak, "--hostkey", HostKey, Sftp], output, error, stop.Token);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        CollectionAssert.AreEqual(new[] { new ListenUrl("sftp", "127.0.0.1", 0) }, factory.StartedListenUrls);
        var written = Encoding.Latin1.GetString(connection.WrittenBytes);
        StringAssert.Contains(written, "curve25519-sha256");
        foreach (var weakAlgorithm in new[] { "diffie-hellman-group14-sha1", "aes128-cbc", "3des-cbc", "arcfour", "hmac-sha1", "hmac-md5" })
        {
            Assert.AreEqual(allowWeak, written.Contains(weakAlgorithm, StringComparison.Ordinal), weakAlgorithm);
        }
    }

    // --ssh-ciphers and --ssh-macs: the narrowed offer and the refusals (ADR-0066, BL-262).

    [TestMethod]
    [DataRow(true, DisplayName = "With --ssh-ciphers and --ssh-macs")]
    [DataRow(false, DisplayName = "Without them")]
    public async Task RunAsync_SshCiphersAndSshMacs_TheServersKexInitListsExactlyTheNamesGivenInBothDirections(bool narrowed)
    {
        var connection = new FakeConnection("SSH-2.0-libssh2_1.11.1\r\n"u8.ToArray());
        var factory = new FakeListenerFactory { Connection = connection };
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        string[] narrowing = narrowed ? ["--ssh-ciphers", "blowfish-cbc", "--ssh-macs", "hmac-ripemd160"] : [];

        var running = CreateRunner(factory, Files((HostKey, TestSshKeyFiles.Rsa2048)))
            .RunAsync(["-s", "--allow-weak-ssh-algorithms", .. narrowing, "--hostkey", HostKey, Sftp], output, error, stop.Token);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;

        Assert.AreEqual(SurlExitCode.Ok, exitCode, error.ToString());
        var written = Encoding.Latin1.GetString(connection.WrittenBytes);
        const string CipherAlone = "\0\0\0\x0c" + "blowfish-cbc" + "\0\0\0\x0c" + "blowfish-cbc";
        const string MacAlone = "\0\0\0\x0e" + "hmac-ripemd160" + "\0\0\0\x0e" + "hmac-ripemd160";
        Assert.AreEqual(narrowed, written.Contains(CipherAlone + MacAlone, StringComparison.Ordinal));
        Assert.AreEqual(!narrowed, written.Contains("chacha20-poly1305@openssh.com", StringComparison.Ordinal));
        Assert.AreEqual(!narrowed, written.Contains("hmac-sha2-256-etm@openssh.com", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("--ssh-ciphers", "aes256-ctr,no-such-cipher", "(2) --ssh-ciphers: surl does not offer the SSH cipher no-such-cipher", DisplayName = "Unknown cipher")]
    [DataRow("--ssh-macs", "hmac-sha2-256,no-such-mac", "(2) --ssh-macs: surl does not offer the SSH MAC no-such-mac", DisplayName = "Unknown MAC")]
    [DataRow("--ssh-ciphers", "aes256-ctr,blowfish-cbc", "(2) --ssh-ciphers: blowfish-cbc needs --allow-weak-ssh-algorithms", DisplayName = "Weak cipher")]
    [DataRow("--ssh-macs", "hmac-ripemd160", "(2) --ssh-macs: hmac-ripemd160 needs --allow-weak-ssh-algorithms", DisplayName = "Weak MAC")]
    [DataRow("--ssh-ciphers", "BLOWFISH-CBC", "(2) --ssh-ciphers: surl does not offer the SSH cipher BLOWFISH-CBC", DisplayName = "Wrong case")]
    public async Task RunAsync_SshAlgorithmNameSurlCannotOffer_IsRefusedBeforeAnyFileIsRead(string option, string names, string message)
    {
        var run = await RunRefusedAsync(Files(), option, names, "--throwaway-hostkey", Sftp);

        Assert.AreEqual(SurlExitCode.FailedInit, run.ExitCode);
        Assert.AreEqual("surl: " + message + NewLine, run.Error);
        Assert.IsFalse(run.FactoryCreated);
    }

    [TestMethod]
    public void AiHelpSshTopic_NamesEveryCipherAndMacSurlCanOffer()
    {
        var everyOffered = Surl.Protocol.Ssh.SshAlgorithmOffer.Default([], aesGcmIsSupported: true, allowWeakAlgorithms: true);
        var topic = AiHelpText.Answer("ssh").Output;

        foreach (var name in everyOffered.Cipher.Concat(everyOffered.Mac))
        {
            StringAssert.Contains(topic, $"`{name}`", name);
        }
    }

    [TestMethod]
    public async Task RunAsync_WeakSshCipherAndMacWithAllowWeakSshAlgorithms_AreServed()
    {
        var run = await ServeUntilListeningAsync(
            Files(), "-s", "--allow-weak-ssh-algorithms", "--ssh-ciphers", "cast128-cbc,aes128-ctr", "--ssh-macs", "hmac-ripemd160@openssh.com", "--throwaway-hostkey", Sftp);

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_AllowWeakSshAlgorithmsWithAShortRsaHostKey_ServesIt()
    {
        var run = await ServeUntilListeningAsync(Files((HostKey, TestSshKeyFiles.Rsa1024)), "--allow-weak-ssh-algorithms", "--hostkey", HostKey, Sftp);

        Assert.AreEqual(SurlExitCode.Ok, run.ExitCode, run.Error);
    }

    [TestMethod]
    [DataRow(Sftp, DisplayName = "An SSH listen URL")]
    [DataRow(Http, DisplayName = "No SSH listen URL")]
    public async Task RunAsync_AllowWeakSshAlgorithmsAtTheInfoLevel_WritesTheWarningWheneverItIsGiven(string listenUrl)
    {
        var run = await ServeUntilListeningAsync(Files((HostKey, TestSshKeyFiles.Rsa2048)), "--allow-weak-ssh-algorithms", "--hostkey", HostKey, listenUrl);

        Assert.AreEqual(
            "surl: warning: --allow-weak-ssh-algorithms: SHA-1, MD5, CBC, RC4, 3DES and 1024-bit Diffie-Hellman SSH algorithms are offered" + NewLine,
            run.Error);
    }

    [TestMethod]
    public async Task RunAsync_VerboseAllowWeakSshAlgorithmsWithAThrowawayHostKey_WritesItsWarningAfterTheThrowawayOneAndBeforeTheNote()
    {
        var run = await ServeUntilListeningAsync(Files(), "-v", "--throwaway-hostkey", "--allow-weak-ssh-algorithms", Sftp);

        Assert.MatchesRegex(
            "^surl: warning: --throwaway-hostkey: [^\r\n]*" + NewLine
            + "surl: warning: " + Regex.Escape(SshHostKeyComposition.WeakAlgorithmsWarning) + NewLine
            + "\\* Serving SSH host key ssh-rsa, [^\r\n]*" + NewLine + "$",
            run.Error);
    }

    [TestMethod]
    public async Task RunAsync_SilentAllowWeakSshAlgorithms_WritesNothing()
    {
        var run = await ServeUntilListeningAsync(Files(), "-s", "--allow-weak-ssh-algorithms", "--throwaway-hostkey", Sftp);

        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public async Task RunAsync_VerboseHostKeys_WritesOneNotePerKeyInCommandLineOrder()
    {
        var run = await ServeUntilListeningAsync(
            Files(("rsa.key", TestSshKeyFiles.Rsa2048), ("ecdsa.key", TestSshKeyFiles.EcdsaP256)),
            "-v", "--hostkey", "rsa.key", "--hostkey", "ecdsa.key", Sftp);

        Assert.MatchesRegex(
            "^\\* Serving SSH host key ssh-rsa, --hostpubsha256 [A-Za-z0-9+/]{43}= --hostpubmd5 [0-9a-f]{32}" + NewLine
            + "\\* Serving SSH host key ecdsa-sha2-nistp256, --hostpubsha256 [A-Za-z0-9+/]{43}= --hostpubmd5 [0-9a-f]{32}" + NewLine + "$",
            run.Error);
    }

    [TestMethod]
    public async Task RunAsync_VerboseHostKeyWithoutAnSshListenUrl_WritesNoNote()
    {
        var run = await ServeUntilListeningAsync(Files((HostKey, TestSshKeyFiles.Rsa2048)), "-v", "--hostkey", HostKey, Http);

        Assert.AreEqual(string.Empty, run.Error);
    }

    [TestMethod]
    public void FormatHostKeyNote_AKey_NamesItsBlobsSha256InBase64AndMd5InLowerCaseHex()
    {
        var (composition, _, _) = SshHostKeyComposition.Compose(
            CommandLineParser.Parse(["--hostkey", HostKey, Sftp]).CommandLine!, Files((HostKey, TestSshKeyFiles.Rsa2048)));
        var hostKey = composition!.HostKeys.Keys[0];
        var blob = hostKey.PublicKeyBlob.Span;

        var note = SshHostKeyComposition.FormatHostKeyNote(hostKey);

        Assert.AreEqual(
            "* Serving SSH host key ssh-rsa, --hostpubsha256 "
            + Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(blob))
            + " --hostpubmd5 "
            + Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData(blob)),
            note);
    }

    private static Func<string, byte[]> Files(params (string Path, byte[] Content)[] files) =>
        path => files.FirstOrDefault(file => file.Path == path).Content ?? throw new FileNotFoundException("missing", path);

    private static Func<string, byte[]> ReadHostKeyAs(string file) => file switch
    {
        "missing" => Files(),
        "denied" => _ => throw new UnauthorizedAccessException("denied"),
        "not a key" => Files((HostKey, TestSshKeyFiles.NotAKey)),
        "encrypted" or "encrypted, wrong pass" => Files((HostKey, TestSshKeyFiles.EncryptedRsa2048)),
        "encrypted openssh" => Files((HostKey, TestSshKeyFiles.EncryptedOpenSsh)),
        "rsa 1024" => Files((HostKey, TestSshKeyFiles.Rsa1024)),
        _ => Files((HostKey, TestSshKeyFiles.X25519)),
    };

    private static Func<string, byte[]> ReadHostCertificateAs(string file) => file switch
    {
        "missing" => Files((HostKey, TestSshCertificateFiles.Ed25519HostKey)),
        "denied" => path => path == HostKey ? TestSshCertificateFiles.Ed25519HostKey : throw new UnauthorizedAccessException("denied"),
        "not a certificate" => Files((HostKey, TestSshCertificateFiles.Ed25519HostKey), (HostCertificate, TestSshKeyFiles.NotAKey)),
        "user certificate" => Files((HostKey, TestSshCertificateFiles.Ed25519HostKey), (HostCertificate, TestSshCertificateFiles.Ed25519UserCertificate)),
        _ => Files((HostKey, TestSshCertificateFiles.Ed25519HostKey), (HostCertificate, TestSshCertificateFiles.OtherEcdsaP384HostCertificate)),
    };

    private static CommandLineRunner CreateRunner(FakeListenerFactory factory, Func<string, byte[]> readStartFile, Action? onFactoryCreated = null) =>
        new(
            _ =>
            {
                onFactoryCreated?.Invoke();
                return factory;
            },
            _ => true,
            _ => DataDirectoryLockOutcome.NoLock,
            TimeProvider.System,
            readStartFile: readStartFile);

    private async Task<Run> RunRefusedAsync(Func<string, byte[]> readStartFile, params string[] args)
    {
        var factoryCreated = false;
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CreateRunner(new FakeListenerFactory(), readStartFile, () => factoryCreated = true)
            .RunAsync(args, output, error, TestContext.CancellationToken);
        return new Run(exitCode, error.ToString(), factoryCreated);
    }

    private async Task<Run> ServeUntilListeningAsync(Func<string, byte[]> readStartFile, params string[] args)
    {
        var factory = new FakeListenerFactory();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var running = CreateRunner(factory, readStartFile).RunAsync(args, output, error, stop.Token);
        await Task.WhenAny(factory.AcceptStarted.Task, running).WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        var exitCode = await running;
        return new Run(exitCode, error.ToString(), FactoryCreated: true);
    }

    private sealed record Run(SurlExitCode ExitCode, string Error, bool FactoryCreated);
}
