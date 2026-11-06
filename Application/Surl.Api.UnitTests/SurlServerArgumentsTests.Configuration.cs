using Surl.Cli;
using Surl.Output;

namespace Surl.Api;

// BL-616: the account, --auth, loosening, TLS, host key and limit properties (ADR-0099, decisions 4 and 6).
public sealed partial class SurlServerArgumentsTests
{
    private const string HttpListenUrl = "http://127.0.0.1:0";

    [TestMethod]
    public void Parse_Accounts_ComposesWhatRepeatedUserComposes()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.Accounts.Add(new SurlAccount("alice", "secret"));
        options.Accounts.Add(new SurlAccount("bob", "pass:word"));

        var actual = AssertComposesAsSurl(options, HttpListenUrl, "--user", "alice:secret", "-u", "bob:pass:word");

        CollectionAssert.AreEqual(
            new[] { new CommandLineAccount("alice", "secret"), new CommandLineAccount("bob", "pass:word") }, actual.Accounts.ToArray());
    }

    [TestMethod]
    public void Parse_UserFileAuthenticationMethodsAndTheLoosenings_ComposeWhatEachOptionComposes()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.UserFile = "users.txt";
        options.AuthenticationMethods = ["basic", "DIGEST"];
        options.AllowAnonymous = true;
        options.AllowPlaintextAuthentication = true;
        options.AllowUploads = true;

        var actual = AssertComposesAsSurl(
            options, HttpListenUrl, "--user-file", "users.txt", "--auth", "basic,DIGEST", "--allow-anonymous",
            "--allow-plaintext-auth", "--allow-uploads");

        Assert.AreEqual("users.txt", actual.UserFile);
        Assert.IsTrue(actual.AllowAnonymous && actual.AllowPlaintextAuthentication && actual.AllowUploads);
        Assert.HasCount(2, actual.GivenAuthenticationMethods!);
    }

    [TestMethod]
    public void Parse_CertificateKeyPassphraseAndCaCertificate_ComposeWhatEachOptionComposes()
    {
        var options = ApiTestSeams.Options("https://127.0.0.1:0");
        options.CertificateFile = "cert.pem";
        options.KeyFile = "key.pem";
        options.KeyPassphrase = "phrase";
        options.CaCertificateFile = "ca.pem";

        var actual = AssertComposesAsSurl(
            options, "https://127.0.0.1:0", "--cert", "cert.pem", "--key", "key.pem", "--pass", "phrase", "--cacert", "ca.pem");

        Assert.AreEqual("cert.pem", actual.CertificateFile);
        Assert.AreEqual("key.pem", actual.KeyFile);
        Assert.AreEqual("phrase", actual.KeyPassphrase);
        Assert.AreEqual("ca.pem", actual.CaCertificateFile);
    }

    [TestMethod]
    public void Parse_SelfSigned_ComposesWhatSelfSignedComposes()
    {
        var options = ApiTestSeams.Options("https://127.0.0.1:0");
        options.SelfSigned = true;

        var actual = AssertComposesAsSurl(options, "https://127.0.0.1:0", "--self-signed");

        Assert.IsTrue(actual.SelfSigned);
    }

    [TestMethod]
    public void Parse_HostKeyFiles_ComposeWhatRepeatedHostKeyComposes()
    {
        var options = ApiTestSeams.Options("sftp://127.0.0.1:0");
        options.HostKeyFiles.Add("ed25519.key");
        options.HostKeyFiles.Add("rsa.key");

        var actual = AssertComposesAsSurl(options, "sftp://127.0.0.1:0", "--hostkey", "ed25519.key", "--hostkey", "rsa.key");

        CollectionAssert.AreEqual(new[] { "ed25519.key", "rsa.key" }, actual.HostKeyFiles.ToArray());
    }

    [TestMethod]
    public void Parse_ThrowawayHostKey_ComposesWhatThrowawayHostKeyComposes()
    {
        var options = ApiTestSeams.Options("sftp://127.0.0.1:0");
        options.ThrowawayHostKey = true;

        var actual = AssertComposesAsSurl(options, "sftp://127.0.0.1:0", "--throwaway-hostkey");

        Assert.IsTrue(actual.ThrowawayHostKey);
    }

    [TestMethod]
    public void Parse_EveryLimit_ComposesWhatEachLimitOptionComposes()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.MaxConnections = 7;
        options.MaxConnectionsPerAddress = 3;
        options.IdleTimeout = TimeSpan.FromMilliseconds(1500);
        options.MaxTime = TimeSpan.FromSeconds(30);
        options.MaxFileSize = 2048;

        var actual = AssertComposesAsSurl(
            options, HttpListenUrl, "--max-connections", "7", "--max-connections-per-address", "3", "--idle-timeout", "1.5",
            "-m", "30", "--max-filesize", "2k");

        Assert.AreEqual(7, actual.MaxConnections);
        Assert.AreEqual(3, actual.MaxConnectionsPerAddress);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1500), actual.IdleTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(30), actual.MaxTime);
        Assert.AreEqual(2048, actual.Limits.MaxUploadBytes);
    }

    [TestMethod]
    public void Parse_ADurationOfOneTick_IsKeptTickForTick()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.MaxTime = TimeSpan.FromTicks(1);

        var actual = AssertComposesAsSurl(options, HttpListenUrl, "--max-time", "0.0000001");

        Assert.AreEqual(TimeSpan.FromTicks(1), actual.MaxTime);
    }

    [TestMethod]
    public void Parse_ZeroAndInfiniteDurations_AreNoLimitAsZeroIs()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.IdleTimeout = TimeSpan.Zero;
        options.MaxTime = Timeout.InfiniteTimeSpan;

        var actual = AssertComposesAsSurl(options, HttpListenUrl, "--idle-timeout", "0", "--max-time", "0");

        Assert.AreEqual(Timeout.InfiniteTimeSpan, actual.IdleTimeout);
        Assert.AreEqual(Timeout.InfiniteTimeSpan, actual.MaxTime);
    }

    [TestMethod]
    public void Parse_AListenUrlAndAnAccountAlone_LeavesAnonymousPlaintextAndUploadsOffAndSurlsLimits()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.Accounts.Add(new SurlAccount("alice", "secret"));
        var surlDefaults = CommandLineParser.Parse([HttpListenUrl]).CommandLine!;

        var actual = SurlServerArguments.Parse(options, []);

        Assert.IsFalse(actual.AllowAnonymous);
        Assert.IsFalse(actual.AllowPlaintextAuthentication);
        Assert.IsFalse(actual.AllowUploads);
        Assert.IsFalse(actual.SelfSigned || actual.ThrowawayHostKey);
        Assert.IsNull(actual.GivenAuthenticationMethods);
        Assert.AreEqual(surlDefaults.MaxConnections, actual.MaxConnections);
        Assert.AreEqual(surlDefaults.MaxConnectionsPerAddress, actual.MaxConnectionsPerAddress);
        Assert.AreEqual(surlDefaults.IdleTimeout, actual.IdleTimeout);
        Assert.AreEqual(surlDefaults.MaxTime, actual.MaxTime);
        Assert.AreEqual(surlDefaults.Limits, actual.Limits);
    }

    [TestMethod]
    public void Compose_EveryConfigurationProperty_IsEachOptionAfterTheContentOptionsAndBeforeTheLevel()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.AllowUploads = true;
        options.Accounts.Add(new SurlAccount("alice", "secret"));
        options.UserFile = "users.txt";
        options.AuthenticationMethods = ["basic"];
        options.AllowAnonymous = true;
        options.AllowPlaintextAuthentication = true;
        options.CertificateFile = "cert.pem";
        options.KeyFile = "key.pem";
        options.KeyPassphrase = "phrase";
        options.SelfSigned = true;
        options.CaCertificateFile = "ca.pem";
        options.HostKeyFiles.Add("host.key");
        options.ThrowawayHostKey = true;
        options.MaxConnections = 1;
        options.MaxConnectionsPerAddress = 2;
        options.IdleTimeout = TimeSpan.FromSeconds(3);
        options.MaxTime = TimeSpan.FromSeconds(4);
        options.MaxFileSize = 5;
        options.LogLevel = LogLevel.Verbose;

        var arguments = SurlServerArguments.Compose(options);

        CollectionAssert.AreEqual(
            new[]
            {
                HttpListenUrl, "--allow-uploads", "--user", "alice:secret", "--user-file", "users.txt", "--auth", "basic",
                "--allow-anonymous", "--allow-plaintext-auth", "--cert", "cert.pem", "--key", "key.pem", "--pass", "phrase",
                "--self-signed", "--cacert", "ca.pem", "--hostkey", "host.key", "--throwaway-hostkey", "--max-connections", "1",
                "--max-connections-per-address", "2", "--idle-timeout", "3", "--max-time", "4", "--max-filesize", "5",
                "--log-level", "verbose",
            },
            arguments.ToArray());
    }

    [TestMethod]
    public void Parse_ANegativeMaxConnections_IsRefusedAsSurlRefusesIt()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.MaxConnections = -1;
        var expected = CommandLineParser.Parse([HttpListenUrl, "--max-connections", "-1"]).Failure!;

        var exception = Assert.ThrowsExactly<SurlServerStartException>(() => SurlServerArguments.Parse(options, []));

        Assert.AreEqual(expected.ExitCode, exception.ExitCode);
        Assert.AreEqual(expected.Message, exception.Lines[0]);
    }

    [TestMethod]
    public void Parse_AnAccountWithAnEmptyPassword_IsRefusedAsSurlRefusesIt()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.Accounts.Add(new SurlAccount("alice", ""));
        var expected = CommandLineParser.Parse([HttpListenUrl, "--user", "alice:"]).Failure!;

        var exception = Assert.ThrowsExactly<SurlServerStartException>(() => SurlServerArguments.Parse(options, []));

        Assert.AreEqual(expected.ExitCode, exception.ExitCode);
        Assert.AreEqual(expected.Message, exception.Lines[0]);
    }

    [TestMethod]
    [DataRow(null, "secret")]
    [DataRow("alice", null)]
    [DataRow("al:ice", "secret")]
    public void ThrowIfMisused_AnAccountWithANullOrSplitUserNameOrANullPassword_ThrowsArgument(string? userName, string? password)
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.Accounts.Add(new SurlAccount(userName!, password!));

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }

    [TestMethod]
    public void ThrowIfMisused_ANullAccount_ThrowsArgument()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.Accounts.Add(null!);

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }

    [TestMethod]
    public void ThrowIfMisused_ANullHostKeyFile_ThrowsArgument()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.HostKeyFiles.Add(null!);

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }

    [TestMethod]
    public void ThrowIfMisused_ANullAuthenticationMethod_ThrowsArgument()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.AuthenticationMethods = ["basic", null!];

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }

    [TestMethod]
    public void ThrowIfMisused_ANegativeIdleTimeout_ThrowsArgument()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.IdleTimeout = TimeSpan.FromSeconds(-1);

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }

    [TestMethod]
    public void ThrowIfMisused_ANegativeMaxTime_ThrowsArgument()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.MaxTime = TimeSpan.FromSeconds(-1);

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }

    [TestMethod]
    public void ThrowIfMisused_EveryConfigurationPropertyWellFormed_IsAccepted()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.Accounts.Add(new SurlAccount("alice", "secret"));
        options.HostKeyFiles.Add("host.key");
        options.AuthenticationMethods = ["basic"];
        options.IdleTimeout = Timeout.InfiniteTimeSpan;
        options.MaxTime = TimeSpan.FromSeconds(1);

        SurlServerArguments.ThrowIfMisused(options);

        Assert.HasCount(1, options.Accounts);
    }

    [TestMethod]
    public void ToString_AnAccount_NamesTheUserAndNotThePassword()
    {
        var account = new SurlAccount("alice", "secret");

        var text = account.ToString();

        Assert.AreEqual("SurlAccount { UserName = alice }", text);
    }

    // Parses the options and surl's own words and asserts they compose equal command lines, each
    // list member compared item by item first; returns the options' command line.
    private static SurlCommandLine AssertComposesAsSurl(SurlServerOptions options, params string[] surlWords)
    {
        var expected = CommandLineParser.Parse(surlWords).CommandLine!;

        var actual = SurlServerArguments.Parse(options, []);

        CollectionAssert.AreEqual(expected.ListenUrls.ToArray(), actual.ListenUrls.ToArray());
        CollectionAssert.AreEqual(expected.Accounts.ToArray(), actual.Accounts.ToArray());
        CollectionAssert.AreEqual(expected.HostKeyFiles.ToArray(), actual.HostKeyFiles.ToArray());
        CollectionAssert.AreEqual(expected.GivenAuthenticationMethods?.ToArray(), actual.GivenAuthenticationMethods?.ToArray());
        Assert.AreEqual(
            expected with
            {
                ListenUrls = actual.ListenUrls,
                Accounts = actual.Accounts,
                HostKeyFiles = actual.HostKeyFiles,
                GivenAuthenticationMethods = actual.GivenAuthenticationMethods,
            },
            actual);
        return actual;
    }
}
