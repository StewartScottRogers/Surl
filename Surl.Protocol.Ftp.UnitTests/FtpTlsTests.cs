using System.Net;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

/// <summary>
/// <c>AUTH TLS</c>, <c>PBSZ</c>, <c>PROT</c>, <c>CCC</c> and implicit <c>ftps://</c>
/// (ADR-0052, decision 5; ADR-0010, section 1), with <see cref="InMemoryConnection"/>'s upgrade
/// standing in for the TLS handshakes.
/// </summary>
[TestClass]
public sealed class FtpTlsTests
{
    private const string AuthAccepted = "234 AUTH accepted, start TLS\r\n";
    private const string PassiveReply = "229 Entering Extended Passive Mode (|||50100|)\r\n";
    private const string RetrieveOpening = "150 Opening data connection for a.txt (12 bytes)\r\n";
    private const string TransferComplete = "226 Transfer complete\r\n";
    private const string CannotOpenDataConnection = "425 Cannot open data connection\r\n";
    private const string SecureLogin = "PBSZ 0\r\nUSER anonymous\r\nPASS ftp@example.com\r\n";
    private const string SecureLoginReplies = "200 PBSZ=0\r\n" + AnonymousLoginReplies;

    private static readonly IPEndPoint PassiveEndPoint = new(IPAddress.Loopback, 50100);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Auth_BytesPipelinedAfterIt_AreDiscardedNeverRun()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted);
        var log = new RecordingExchangeLog();
        var control = new InMemoryConnection(Ascii("AUTH TLS\r\nUSER evil\r\nPASS x\r\n", "NOOP\r\n"));

        await Server(policy, isTlsUpgradeAvailable: true).ServeAsync(control, Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log));

        Assert.IsTrue(control.UpgradeRequested);
        Assert.AreEqual(Greeting + AuthAccepted + "200 NOOP ok\r\n", Text(control.WrittenBytes));
        Assert.IsEmpty(policy.Logins);
        CollectionAssert.AreEqual(new[] { "Discarded 19 bytes sent after AUTH" }, log.Notes.ToArray());
    }

    [TestMethod]
    [DataRow("AUTH TLS")]
    [DataRow("AUTH SSL")]
    [DataRow("auth tls")]
    public async Task Auth_WithACertificate_Answers234AndUpgradesWithNoNote(string command)
    {
        var log = new RecordingExchangeLog();
        var control = new InMemoryConnection(Ascii(command + "\r\n"));

        await Server(isTlsUpgradeAvailable: true).ServeAsync(control, Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log));

        Assert.IsTrue(control.UpgradeRequested);
        Assert.AreEqual(Greeting + AuthAccepted, Text(control.WrittenBytes));
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task Auth_FailedUpgrade_ThrowsTheHandshakeFailureAndWritesNothingAfter234()
    {
        var control = new InMemoryConnection(Ascii("AUTH TLS\r\n", "NOOP\r\n"), upgradeFails: true);

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => Server(isTlsUpgradeAvailable: true).ServeAsync(control, Context(new ManualTimeProvider(), TestContext.CancellationToken)));

        Assert.IsTrue(control.UpgradeRequested);
        Assert.AreEqual(Greeting + AuthAccepted, Text(control.WrittenBytes));
    }

    [TestMethod]
    public async Task Auth_WithNoCertificate_Answers534AndPassStaysRefusedAsPlainText()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted) { RefuseWithoutTls = true };
        var control = new InMemoryConnection(Ascii("AUTH TLS\r\nUSER tester\r\nPASS secret\r\n"));

        await Server(policy).ServeAsync(control, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.IsFalse(control.UpgradeRequested);
        Assert.AreEqual(
            Greeting + "534 TLS is not available\r\n331 Password required\r\n530 Login needs TLS first: send AUTH TLS\r\n",
            Text(control.WrittenBytes));
        Assert.IsNull(policy.Logins.Single().TlsSession);
    }

    [TestMethod]
    public async Task Pass_RefusedAsPlainTextBeforeAuth_IsAcceptedAfterIt()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted) { RefuseWithoutTls = true };
        var control = new InMemoryConnection(Ascii("USER tester\r\nPASS secret\r\nAUTH TLS\r\n", "USER tester\r\nPASS secret\r\n"));

        await Server(policy, isTlsUpgradeAvailable: true).ServeAsync(control, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual(
            Greeting + "331 Password required\r\n530 Login needs TLS first: send AUTH TLS\r\n" + AuthAccepted + "331 Password required\r\n" + LoggedIn,
            Text(control.WrittenBytes));
        Assert.IsNull(policy.Logins[0].TlsSession);
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, policy.Logins[1].TlsSession);
    }

    [TestMethod]
    [DataRow("AUTH", "501 Syntax error in arguments\r\n", DisplayName = "no mechanism")]
    [DataRow("AUTH KERBEROS_V4", "504 Security mechanism not understood\r\n", DisplayName = "another mechanism")]
    [DataRow("PBSZ", "501 Syntax error in arguments\r\n", DisplayName = "PBSZ with no size")]
    [DataRow("PROT", "501 Syntax error in arguments\r\n", DisplayName = "PROT with no level")]
    public async Task Command_Malformed_IsRefusedWithoutAnUpgrade(string command, string reply)
    {
        var control = new InMemoryConnection(Ascii(command + "\r\n"));

        await Server(isTlsUpgradeAvailable: true).ServeAsync(control, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.IsFalse(control.UpgradeRequested);
        Assert.AreEqual(Greeting + reply, Text(control.WrittenBytes));
    }

    [TestMethod]
    [DataRow("AUTH TLS", "503 Already using TLS\r\n")]
    [DataRow("PBSZ 0", "200 PBSZ=0\r\n")]
    [DataRow("PBSZ 16384", "200 PBSZ=0\r\n")]
    [DataRow("PROT P", "503 Send PBSZ first\r\n")]
    public async Task Command_OnAConnectionAlreadyUnderTls_IsAnswered(string command, string reply)
    {
        var control = TlsControl(command + "\r\n");

        await Server(isTlsUpgradeAvailable: true).ServeAsync(control, Context(new ManualTimeProvider(), TestContext.CancellationToken, scheme: "ftps"));

        Assert.IsFalse(control.UpgradeRequested);
        Assert.AreEqual(Greeting + reply, Text(control.WrittenBytes));
    }

    [TestMethod]
    [DataRow("PROT C", "200 Protection level set to C\r\n")]
    [DataRow("PROT p", "200 Protection level set to P\r\n")]
    [DataRow("PROT S", "536 Protection level not supported\r\n")]
    [DataRow("PROT E", "536 Protection level not supported\r\n")]
    [DataRow("PROT X", "504 Protection level not understood\r\n")]
    public async Task Prot_AfterPbsz_AnswersTheLevel(string command, string reply)
    {
        var control = TlsControl("PBSZ 0\r\n" + command + "\r\n");

        await Server().ServeAsync(control, Context(new ManualTimeProvider(), TestContext.CancellationToken, scheme: "ftps"));

        Assert.AreEqual(Greeting + "200 PBSZ=0\r\n" + reply, Text(control.WrittenBytes));
    }

    [TestMethod]
    public async Task Ccc_AfterLogin_IsRefusedSoTheControlConnectionStaysEncrypted()
    {
        var written = await ServeLoggedInAsync("CCC\r\nNOOP\r\n", TestContext.CancellationToken);

        Assert.AreEqual("534 Request denied for policy reasons\r\n200 NOOP ok\r\n", written);
    }

    [TestMethod]
    public async Task Ccc_BeforeLogin_Answers530()
    {
        var written = await ServeAsync("CCC\r\n", TestContext.CancellationToken);

        Assert.AreEqual(Greeting + "530 Please log in with USER and PASS\r\n", written);
    }

    [TestMethod]
    public async Task Feat_WithACertificate_AlsoListsAuthTlsPbszAndProt()
    {
        var control = new InMemoryConnection(Ascii("FEAT\r\n"));

        await Server(isTlsUpgradeAvailable: true).ServeAsync(control, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual(
            Greeting + "211-Features:\r\n EPRT\r\n EPSV\r\n MDTM\r\n MLST type*;size*;modify*;\r\n PASV\r\n REST STREAM\r\n SIZE\r\n TVFS\r\n UTF8\r\n AUTH TLS\r\n PBSZ\r\n PROT\r\n211 End\r\n",
            Text(control.WrittenBytes));
    }

    [TestMethod]
    [DataRow("ftp", "PROT P\r\n", true, DisplayName = "ftp://, PROT P: TLS")]
    [DataRow("ftp", "PROT C\r\n", false, DisplayName = "ftp://, PROT C: plaintext")]
    [DataRow("ftp", "", false, DisplayName = "ftp://, no PROT: plaintext")]
    [DataRow("ftps", "", true, DisplayName = "ftps://, no PROT: TLS")]
    [DataRow("ftps", "PROT C\r\n", false, DisplayName = "ftps://, PROT C: plaintext")]
    [DataRow("ftps", "PROT C\r\nPROT P\r\n", true, DisplayName = "ftps://, PROT C then P: TLS")]
    public async Task Retr_UpgradesTheDataConnectionOnlyUnderProtP(string scheme, string protection, bool upgrades)
    {
        var dataConnection = new InMemoryConnection([]);
        var control = TlsControl(SecureLogin + protection + "EPSV\r\nRETR a.txt\r\n");

        await Server().ServeAsync(control, Context(new ManualTimeProvider(), TestContext.CancellationToken, dataConnections: Passive(dataConnection), scheme: scheme));

        StringAssert.EndsWith(Text(control.WrittenBytes), PassiveReply + RetrieveOpening + TransferComplete);
        Assert.AreEqual(upgrades, dataConnection.UpgradeRequested);
        Assert.AreEqual(FileText, Text(dataConnection.WrittenBytes));
    }

    [TestMethod]
    public async Task Retr_DataHandshakeFails_Answers425After150AndResetsTheDataConnection()
    {
        var log = new RecordingExchangeLog();
        var dataConnection = new InMemoryConnection([], upgradeFails: true);
        var control = TlsControl(SecureLogin + "EPSV\r\nRETR a.txt\r\n");

        await Server().ServeAsync(control, Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log, dataConnections: Passive(dataConnection), scheme: "ftps"));

        Assert.AreEqual(Greeting + SecureLoginReplies + PassiveReply + RetrieveOpening + CannotOpenDataConnection, Text(control.WrittenBytes));
        Assert.IsTrue(dataConnection.Aborted);
        Assert.IsTrue(dataConnection.Disposed);
        Assert.IsEmpty(dataConnection.WrittenBytes);
        CollectionAssert.AreEqual(new[] { "The TLS handshake on the data connection failed (The TLS handshake failed.); answered 425." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task Retr_DataConnectionResetDuringTheHandshake_Answers425()
    {
        var dataConnection = new InMemoryConnection([]);
        dataConnection.Abort();
        var control = TlsControl(SecureLogin + "EPSV\r\nRETR a.txt\r\n");

        await Server().ServeAsync(control, Context(new ManualTimeProvider(), TestContext.CancellationToken, dataConnections: Passive(dataConnection), scheme: "ftps"));

        Assert.AreEqual(Greeting + SecureLoginReplies + PassiveReply + RetrieveOpening + CannotOpenDataConnection, Text(control.WrittenBytes));
    }

    [TestMethod]
    public async Task Retr_DataHandshakeThrowsAnythingElse_EndsTheExchangeWithIt()
    {
        var dataConnection = new InMemoryConnection([]);
        await dataConnection.DisposeAsync();
        var control = TlsControl(SecureLogin + "EPSV\r\nRETR a.txt\r\n");

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => Server().ServeAsync(
            control, Context(new ManualTimeProvider(), TestContext.CancellationToken, dataConnections: Passive(dataConnection), scheme: "ftps")));
    }

    [TestMethod]
    public async Task Stor_UnderProtP_ReadsTheUploadOverTls()
    {
        var fileSystem = StandardFileSystem();
        var dataConnection = new InMemoryConnection(Ascii("uploaded body\n"));
        var control = TlsControl(SecureLogin + "EPSV\r\nSTOR b.txt\r\n");

        await Server(contentStore: FtpUploadTests.Store(fileSystem)).ServeAsync(
            control, Context(new ManualTimeProvider(), TestContext.CancellationToken, dataConnections: Passive(dataConnection), scheme: "ftps"));

        StringAssert.EndsWith(Text(control.WrittenBytes), PassiveReply + "150 Opening data connection for b.txt\r\n" + TransferComplete);
        Assert.IsTrue(dataConnection.UpgradeRequested);
        Assert.AreEqual("uploaded body\n", FtpUploadTests.ReadFile(fileSystem, "b.txt"));
    }

    [TestMethod]
    public async Task Stor_DataHandshakeFails_Answers425After150AndWritesNothing()
    {
        var fileSystem = StandardFileSystem();
        var dataConnection = new InMemoryConnection(Ascii("uploaded body\n"), upgradeFails: true);
        var control = TlsControl(SecureLogin + "EPSV\r\nSTOR b.txt\r\n");

        await Server(contentStore: FtpUploadTests.Store(fileSystem)).ServeAsync(
            control, Context(new ManualTimeProvider(), TestContext.CancellationToken, dataConnections: Passive(dataConnection), scheme: "ftps"));

        Assert.AreEqual(
            Greeting + SecureLoginReplies + PassiveReply + "150 Opening data connection for b.txt\r\n" + CannotOpenDataConnection,
            Text(control.WrittenBytes));
        Assert.IsTrue(dataConnection.Aborted);
        Assert.IsTrue(dataConnection.Disposed);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, "b.txt")));
    }

    // A control connection already under TLS, as the engine hands over an ftps:// exchange.
    private static InMemoryConnection TlsControl(string request) =>
        new(Ascii(request), initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

    private static InMemoryDataConnections Passive(IConnection dataConnection) =>
        new InMemoryDataConnections().ScriptPassiveListener(PassiveEndPoint, dataConnection);
}
