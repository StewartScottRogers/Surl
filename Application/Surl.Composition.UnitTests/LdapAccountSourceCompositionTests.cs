using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Authentication;
using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// <see cref="LdapAccountSourceComposition"/> and the <c>--user-ldap</c> source composed beside the
/// <c>--user</c> accounts (ADR-0149 decisions 6, 7 and 9; BL-673).
/// </summary>
[TestClass]
public sealed class LdapAccountSourceCompositionTests
{
    private const string Directory = "ldaps://dir.example.com/dc=example,dc=com";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Compose_NoUserLdap_ComposesNoSource()
    {
        var (source, exitCode, failure) = LdapAccountSourceComposition.Compose(new SurlCommandLine(), Unreadable, TimeProvider.System, null, null);

        Assert.IsNull(source);
        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public void Compose_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LdapAccountSourceComposition.Compose(null!, Unreadable, TimeProvider.System, null, null));
        Assert.ThrowsExactly<ArgumentNullException>(() => LdapAccountSourceComposition.Compose(new SurlCommandLine(), null!, TimeProvider.System, null, null));
        Assert.ThrowsExactly<ArgumentNullException>(() => LdapAccountSourceComposition.Compose(new SurlCommandLine(), Unreadable, null!, null, null));
        Assert.ThrowsExactly<ArgumentNullException>(() => LdapAccountSourceComposition.WriteWarning(null!, TextWriter.Null));
        Assert.ThrowsExactly<ArgumentNullException>(() => LdapAccountSourceComposition.WriteWarning(new SurlCommandLine(), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => LdapAccountSourceComposition.WriteStartLine(null!, TextWriter.Null));
        Assert.ThrowsExactly<ArgumentNullException>(() => LdapAccountSourceComposition.WriteStartLine(new SurlCommandLine(), null!));
    }

    [TestMethod]
    public void Compose_AUrlNamingADirectory_ComposesTheSourceWithoutContactingIt()
    {
        var connector = new CountingConnector();

        var (source, exitCode, failure) = Compose(Source(Directory), connector);

        Assert.IsNotNull(source, failure);
        Assert.AreEqual(SurlExitCode.Ok, exitCode);
        Assert.AreEqual(0, connector.Connections);
    }

    [TestMethod]
    [DataRow("ldap://u:hunter2@h/dc=x", "(2) --user-ldap: the URL holds a user name or password; use bind-dn and bind-password-file", DisplayName = "User information")]
    [DataRow("ldap:///dc=x", "(2) --user-ldap: the URL names no host", DisplayName = "No host")]
    [DataRow("ldap://h:0/dc=x", "(2) --user-ldap: the URL's port is not 1 to 65535", DisplayName = "Port 0")]
    [DataRow("ldap://h/", "(2) --user-ldap: the URL's base DN is missing or malformed", DisplayName = "No base DN")]
    [DataRow("ldap://h/dc=x?uid,cn", "(2) --user-ldap: the URL names more than one attribute", DisplayName = "Two attributes")]
    [DataRow("ldap://h/dc=x?_x", "(2) --user-ldap: the URL's attribute is not an attribute description", DisplayName = "Bad attribute")]
    [DataRow("ldap://h/dc=x?uid?deep", "(2) --user-ldap: the URL's scope is not base, one or sub", DisplayName = "Bad scope")]
    [DataRow("ldap://h/dc=x?uid?sub?(cn", "(2) --user-ldap: the URL's filter is malformed", DisplayName = "Bad filter")]
    [DataRow("ldap://h/dc=x?uid?sub?(cn=a)?e-x", "(2) --user-ldap: the URL's extensions are not supported", DisplayName = "Extension")]
    [DataRow("http://h/dc=x", "(2) --user-ldap: the URL is not ldap:// or ldaps://", DisplayName = "Not an LDAP URL")]
    public void Compose_AMalformedUrl_IsRefused2WithoutEchoingIt(string url, string message)
    {
        var (source, exitCode, failure) = Compose(Source(url), null);

        Assert.IsNull(source);
        Assert.AreEqual(SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual(message, failure);
        Assert.DoesNotContain("hunter2", failure!);
    }

    [TestMethod]
    public void Compose_PlaintextWithoutAllowPlaintextAuth_IsRefused2()
    {
        var (source, exitCode, failure) = Compose(Source("ldap://h/dc=x", isPlaintext: true), null);

        Assert.IsNull(source);
        Assert.AreEqual(SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual("(2) --user-ldap plaintext needs --allow-plaintext-auth", failure);
    }

    [TestMethod]
    public void Compose_PlaintextWithAllowPlaintextAuth_ComposesTheSource()
    {
        var commandLine = new SurlCommandLine { UserLdap = Source("ldap://h/dc=x", isPlaintext: true), AllowPlaintextAuthentication = true };

        var (source, _, failure) = LdapAccountSourceComposition.Compose(commandLine, Unreadable, TimeProvider.System, new CountingConnector(), null);

        Assert.IsNotNull(source, failure);
    }

    [TestMethod]
    [DataRow("secret\n", true, DisplayName = "LF")]
    [DataRow("secret\r\nmore lines\r\n", true, DisplayName = "CR LF and more lines")]
    [DataRow("secret", true, DisplayName = "No line end")]
    public void Compose_BindPasswordFile_UsesItsFirstLine(string content, bool composed)
    {
        var (source, _, failure) = Compose(Source(Directory, bindDn: "cn=svc", bindPasswordFile: "pw"), new CountingConnector(), ("pw", Encoding.UTF8.GetBytes(content)));

        Assert.AreEqual(composed, source is not null, failure);
    }

    [TestMethod]
    public void Compose_BindPasswordFileThatCannotBeRead_Is37WithoutTheDn()
    {
        var (source, exitCode, failure) = Compose(Source(Directory, bindDn: "cn=svc", bindPasswordFile: "pw.txt"), null);

        Assert.IsNull(source);
        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exitCode);
        Assert.AreEqual("(37) Could not read bind password file pw.txt", failure);
    }

    [TestMethod]
    public void Compose_BindPasswordFileOver64KiB_Is37()
    {
        var (_, exitCode, failure) = Compose(
            Source(Directory, bindDn: "cn=svc", bindPasswordFile: "pw"), null, ("pw", new byte[LdapAccountSourceComposition.MaxBindPasswordFileBytes + 1]));

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exitCode);
        Assert.AreEqual("(37) Could not read bind password file pw", failure);
    }

    [TestMethod]
    [DataRow("", DisplayName = "Empty file")]
    [DataRow("\nsecond", DisplayName = "Empty first line")]
    [DataRow("\r\nsecond", DisplayName = "First line is only a CR")]
    public void Compose_BindPasswordFileWithNoPassword_Is2(string content)
    {
        var (_, exitCode, failure) = Compose(Source(Directory, bindDn: "cn=svc", bindPasswordFile: "pw"), null, ("pw", Encoding.UTF8.GetBytes(content)));

        Assert.AreEqual(SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual("(2) Bind password file pw: the password is empty or not UTF-8", failure);
    }

    [TestMethod]
    public void Compose_BindPasswordFileThatIsNotUtf8_Is2()
    {
        var (_, exitCode, failure) = Compose(Source(Directory, bindDn: "cn=svc", bindPasswordFile: "pw"), null, ("pw", [0xFF, 0xFE, (byte)'\n']));

        Assert.AreEqual(SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual("(2) Bind password file pw: the password is empty or not UTF-8", failure);
    }

    [TestMethod]
    public void Compose_CaFileThatCannotBeRead_Is77()
    {
        var (source, exitCode, failure) = Compose(Source(Directory, caFile: "ca.pem"), null);

        Assert.IsNull(source);
        Assert.AreEqual(SurlExitCode.CaCertificateBadFile, exitCode);
        Assert.AreEqual("(77) Could not read CA file ca.pem", failure);
    }

    [TestMethod]
    public void Compose_CaFileAccessDenied_Is77()
    {
        var commandLine = new SurlCommandLine { UserLdap = Source(Directory, caFile: "ca.pem") };

        var (_, exitCode, failure) = LdapAccountSourceComposition.Compose(
            commandLine, _ => throw new UnauthorizedAccessException(), TimeProvider.System, null, null);

        Assert.AreEqual(SurlExitCode.CaCertificateBadFile, exitCode);
        Assert.AreEqual("(77) Could not read CA file ca.pem", failure);
    }

    [TestMethod]
    public void Compose_CaFileHoldingNoCertificate_Is77()
    {
        var (_, exitCode, failure) = Compose(Source(Directory, caFile: "ca.pem"), null, ("ca.pem", Encoding.ASCII.GetBytes("not a certificate")));

        Assert.AreEqual(SurlExitCode.CaCertificateBadFile, exitCode);
        Assert.AreEqual("(77) CA file ca.pem holds no certificate", failure);
    }

    [TestMethod]
    public void Compose_CaFileHoldingACertificate_ComposesTheSource()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=dir.example.com", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var (source, _, failure) = Compose(
            Source(Directory, caFile: "ca.pem"), new CountingConnector(), ("ca.pem", Encoding.ASCII.GetBytes(certificate.ExportCertificatePem())));

        Assert.IsNotNull(source, failure);
    }

    [TestMethod]
    public void Compose_WithoutAConnector_BuildsTheTlsClientConnector()
    {
        var (source, _, failure) = Compose(Source(Directory), null);

        Assert.IsNotNull(source, failure);
    }

    [TestMethod]
    public async Task Authentication_UserAccountsAreJudgedByTheBook_AndOtherNamesGoToTheDirectory()
    {
        var connector = new CountingConnector();
        var notes = new List<string>();
        var commandLine = new SurlCommandLine
        {
            Accounts = [new CommandLineAccount("alice", "secret")],
            UserLdap = Source(Directory),
            AllowPlaintextAuthentication = true,
        };
        var (policy, _, _, exitCode, failure) = AuthenticationComposition.Compose(
            commandLine, Unreadable, new SteppingTimeProvider { Step = TimeSpan.FromSeconds(2) }, ldapConnector: connector, ldapNote: notes.Add);
        Assert.IsNotNull(policy, failure);
        Assert.AreEqual(SurlExitCode.Ok, exitCode);

        var local = await policy.CheckPasswordLoginAsync(Login("alice", "secret"), TestContext.CancellationToken);
        var wrongLocal = await policy.CheckPasswordLoginAsync(Login("alice", "wrong"), TestContext.CancellationToken);

        Assert.AreEqual(PasswordLoginVerdict.Accepted, local);
        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, wrongLocal);
        Assert.AreEqual(0, connector.Connections);

        var directory = await policy.CheckPasswordLoginAsync(Login("bob", "pw"), TestContext.CancellationToken);

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, directory);
        Assert.AreEqual(1, connector.Connections);
        CollectionAssert.AreEqual(new[] { "Login refused: password bob (directory: could not connect)" }, notes);
    }

    [TestMethod]
    public async Task Authentication_ProtocolsKey_KeepsTheDirectoryOffOtherSchemes()
    {
        var connector = new CountingConnector();
        var commandLine = new SurlCommandLine
        {
            UserLdap = Source(Directory, protocols: ["imap"]),
            AllowPlaintextAuthentication = true,
        };
        var (policy, _, _, _, failure) = AuthenticationComposition.Compose(
            commandLine, Unreadable, new SteppingTimeProvider { Step = TimeSpan.FromSeconds(2) }, ldapConnector: connector);
        Assert.IsNotNull(policy, failure);

        var http = await policy.CheckPasswordLoginAsync(Login("bob", "pw"), TestContext.CancellationToken);
        var imap = await policy.CheckPasswordLoginAsync(Login("bob", "pw", "imap"), TestContext.CancellationToken);

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, http);
        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, imap);
        Assert.AreEqual(1, connector.Connections);
    }

    [TestMethod]
    public void Authentication_ABadUserLdap_FailsTheCompositionWithItsExitCode()
    {
        var commandLine = new SurlCommandLine { UserLdap = Source("ldap:///dc=x") };

        var (policy, names, _, exitCode, failure) = AuthenticationComposition.Compose(commandLine, Unreadable, TimeProvider.System);

        Assert.IsNull(policy);
        Assert.IsEmpty(names);
        Assert.AreEqual(SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual("(2) --user-ldap: the URL names no host", failure);
    }

    [TestMethod]
    public void WriteLooseningWarnings_Plaintext_WritesItsWarningAfterTheAllowPlaintextAuthOne()
    {
        var commandLine = new SurlCommandLine
        {
            UserLdap = Source("ldap://dir.example.com:3890/dc=x", isPlaintext: true),
            AllowPlaintextAuthentication = true,
            LogLevel = Surl.Output.LogLevel.Info,
        };
        using var log = new StringWriter();

        AuthenticationComposition.WriteLooseningWarnings(commandLine, log);

        Assert.AreEqual(
            "surl: warning: --allow-plaintext-auth: passwords and tokens are accepted over unencrypted connections" + Environment.NewLine
            + "surl: warning: --user-ldap: passwords are sent to dir.example.com:3890 without TLS" + Environment.NewLine,
            log.ToString());
    }

    [TestMethod]
    public void WriteWarning_NotPlaintextOrQuiet_WritesNothing()
    {
        using var log = new StringWriter();

        LdapAccountSourceComposition.WriteWarning(new SurlCommandLine { UserLdap = Source(Directory), LogLevel = Surl.Output.LogLevel.Info }, log);
        LdapAccountSourceComposition.WriteWarning(
            new SurlCommandLine { UserLdap = Source("ldap://h/dc=x", isPlaintext: true), LogLevel = Surl.Output.LogLevel.Error }, log);
        LdapAccountSourceComposition.WriteWarning(new SurlCommandLine { LogLevel = Surl.Output.LogLevel.Info }, log);
        LdapAccountSourceComposition.WriteWarning(
            new SurlCommandLine { UserLdap = Source("ldap:///dc=x", isPlaintext: true), LogLevel = Surl.Output.LogLevel.Info }, log);

        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public void WriteStartLine_Verbose_NamesTheDirectoryByHostAndPortOnly()
    {
        using var log = new StringWriter();

        LdapAccountSourceComposition.WriteStartLine(new SurlCommandLine { UserLdap = Source(Directory, bindDn: "cn=svc"), LogLevel = Surl.Output.LogLevel.Verbose }, log);
        LdapAccountSourceComposition.WriteStartLine(new SurlCommandLine { UserLdap = Source("ldap://h:3890/dc=x"), LogLevel = Surl.Output.LogLevel.Verbose }, log);

        Assert.AreEqual(
            "Account source: LDAP directory ldaps://dir.example.com:636" + Environment.NewLine
            + "Account source: LDAP directory ldap://h:3890" + Environment.NewLine,
            log.ToString());
    }

    [TestMethod]
    public void WriteStartLine_BelowVerboseOrWithoutTheOption_WritesNothing()
    {
        using var log = new StringWriter();

        LdapAccountSourceComposition.WriteStartLine(new SurlCommandLine { UserLdap = Source(Directory), LogLevel = Surl.Output.LogLevel.Info }, log);
        LdapAccountSourceComposition.WriteStartLine(new SurlCommandLine { LogLevel = Surl.Output.LogLevel.Verbose }, log);
        LdapAccountSourceComposition.WriteStartLine(new SurlCommandLine { UserLdap = Source("ldap:///dc=x"), LogLevel = Surl.Output.LogLevel.Verbose }, log);

        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public async Task ServeAsync_UserLdapWithAGroupMemberNamingNoAccount_WritesTheNotesAndTheWarning()
    {
        var connector = new CountingConnector();
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse(
                "-v", "--allow-plaintext-auth", "--user-ldap", "ldap://127.0.0.1:3890/dc=x;plaintext", "--group", "ops=carol", "http://127.0.0.1:0/"),
            new StartTestSeams().Seams with { LdapDirectoryConnector = connector },
            TestContext.CancellationToken);
        await using var serving = start.Serving ?? throw new AssertFailedException(string.Join(" ", start.Failure!.Lines));
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        var text = log.ToString();
        Assert.Contains("surl: warning: --user-ldap: passwords are sent to 127.0.0.1:3890 without TLS", text);
        Assert.Contains("Account source: LDAP directory ldap://127.0.0.1:3890", text);
        Assert.Contains("Member carol is not a configured account; taken as a directory account", text);
    }

    [TestMethod]
    public async Task ComposeAsync_AGroupMemberNamingNoAccountWithoutUserLdap_IsStillRefused()
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--group", "ops=carol", "http://127.0.0.1:0/"), new StartTestSeams().Seams, TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { "(2) option --group: member carol names no account or group" }, start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public async Task ComposeAsync_ABadUserLdap_IsRefusedBeforeAnyListenerBinds()
    {
        var start = await SurlServerComposition.ComposeAsync(
            new SurlCommandLine
            {
                UserLdap = Source("ldap:///dc=x"),
                ListenUrls = [new ListenUrl("http", "127.0.0.1", 0)],
            },
            new StartTestSeams().Seams,
            TestContext.CancellationToken);

        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { "(2) --user-ldap: the URL names no host" }, start.Failure.Lines.ToArray());
    }

    private static ConfiguredLdapAccountSource Source(
        string url,
        string? bindDn = null,
        string? bindPasswordFile = null,
        string? caFile = null,
        bool isPlaintext = false,
        IReadOnlyList<string>? protocols = null) =>
        new(url, bindDn, bindPasswordFile, caFile, TimeoutSeconds: 5, protocols, isPlaintext);

    private static byte[] Unreadable(string path) => throw new FileNotFoundException(path);

    private static PasswordLogin Login(string user, string password, string scheme = "http") =>
        new(scheme, user, Encoding.UTF8.GetBytes(password), null);

    private static (LdapAccountSource? Source, SurlExitCode ExitCode, string? FailureMessage) Compose(
        ConfiguredLdapAccountSource configured, ILdapDirectoryConnector? connector, params (string Path, byte[] Content)[] files)
    {
        var commandLine = new SurlCommandLine { UserLdap = configured };

        return LdapAccountSourceComposition.Compose(
            commandLine,
            path => files.Where(file => file.Path == path).Select(file => file.Content).FirstOrDefault() ?? throw new FileNotFoundException(path),
            TimeProvider.System,
            connector,
            null);
    }

    private sealed class CountingConnector : ILdapDirectoryConnector
    {
        public int Connections { get; private set; }

        public ValueTask<ILdapDirectoryConnection> ConnectAsync(LdapDirectoryEndpoint endpoint, CancellationToken cancellationToken)
        {
            Connections++;
            throw new IOException("no directory in this test");
        }
    }
}
