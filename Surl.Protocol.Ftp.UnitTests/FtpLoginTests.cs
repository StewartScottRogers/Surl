using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

[TestClass]
public sealed class FtpLoginTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Login_Accepted_Answers230AndNotesTheAcceptedLogin()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted);
        var log = new RecordingExchangeLog();

        var written = await ServeAsync("USER tester\r\nPASS secret\r\nPWD\r\n", TestContext.CancellationToken, policy, log);

        Assert.AreEqual(Greeting + "331 Password required\r\n" + LoggedIn + "257 \"/\" is the current directory\r\n", written);
        CollectionAssert.AreEqual(new[] { "Login accepted: ftp tester" }, log.Notes.ToArray());
        var login = policy.Logins.Single();
        Assert.AreEqual("secret", Encoding.ASCII.GetString(login.Password!.Value.Span));
    }

    [TestMethod]
    public async Task Login_AcceptedUnchecked_Answers230WithNoNote()
    {
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(AnonymousLogin, TestContext.CancellationToken, log: log);

        Assert.AreEqual(Greeting + AnonymousLoginReplies, written);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task Login_RefusedCredentials_Answers530NotesTheRefusalAndStaysLoggedOut()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.RefusedCredentials);
        var log = new RecordingExchangeLog();

        var written = await ServeAsync("USER tester\r\nPASS wrong\r\nPWD\r\n", TestContext.CancellationToken, policy, log);

        Assert.AreEqual(
            Greeting + "331 Password required\r\n530 Login incorrect\r\n530 Please log in with USER and PASS\r\n",
            written);
        CollectionAssert.AreEqual(new[] { "Login refused: ftp tester" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task Login_RefusedAnonymous_Answers530WithNoNote()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.RefusedAnonymous);
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(AnonymousLogin, TestContext.CancellationToken, policy, log);

        Assert.AreEqual(Greeting + "331 Password required\r\n530 Login incorrect\r\n", written);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(99)]
    public async Task Login_VerdictThisServerDoesNotKnow_IsRefused(int verdict)
    {
        var policy = new UnitTestRecordingAuthenticationPolicy((PasswordLoginVerdict)verdict);

        var written = await ServeAsync(AnonymousLogin + "PWD\r\n", TestContext.CancellationToken, policy);

        Assert.AreEqual(
            Greeting + "331 Password required\r\n530 Login incorrect\r\n530 Please log in with USER and PASS\r\n",
            written);
    }

    [TestMethod]
    public async Task Login_OverPlaintextWithoutAllowPlaintextAuth_Answers530NeedsTlsWithNoNote()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted) { RefuseWithoutTls = true };
        var log = new RecordingExchangeLog();

        var written = await ServeAsync("USER tester\r\nPASS secret\r\n", TestContext.CancellationToken, policy, log);

        Assert.AreEqual(Greeting + "331 Password required\r\n530 Login needs TLS first: send AUTH TLS\r\n", written);
        Assert.IsEmpty(log.Notes);
        Assert.IsNull(policy.Logins.Single().TlsSession);
    }

    [TestMethod]
    public async Task Login_OverTls_PassesTheConnectionsTlsSession()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted) { RefuseWithoutTls = true };
        var session = InMemoryConnection.DefaultUpgradeTlsSession;
        var connection = new InMemoryConnection(Ascii("USER tester\r\nPASS secret\r\n"), initialTlsSession: session);

        await Server(policy).ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual(Greeting + "331 Password required\r\n" + LoggedIn, Text(connection.WrittenBytes));
        Assert.AreSame(session, policy.Logins.Single().TlsSession);
    }

    [TestMethod]
    public async Task Pass_WithNoPassword_ChecksAnEmptyPassword()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted);

        await ServeAsync("USER tester\r\nPASS\r\n", TestContext.CancellationToken, policy);

        Assert.AreEqual(0, policy.Logins.Single().Password!.Value.Length);
    }

    [TestMethod]
    public async Task Pass_PasswordBytes_ReachThePolicyAsSent()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted);
        byte[] line = [.. "PASS "u8, 0xFF, 0x80, (byte)'x', (byte)'\r', (byte)'\n'];
        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes("USER tester\r\n"), line]);

        await Server(policy).ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        CollectionAssert.AreEqual(new byte[] { 0xFF, 0x80, (byte)'x' }, policy.Logins.Single().Password!.Value.ToArray());
    }

    [TestMethod]
    public async Task User_SecondBeforePass_ReplacesTheName()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted);

        await ServeAsync("USER first\r\nUSER second\r\nPASS secret\r\n", TestContext.CancellationToken, policy);

        Assert.AreEqual("second", policy.Logins.Single().UserName);
    }

    [TestMethod]
    public async Task User_WithNoName_Answers501()
    {
        var written = await ServeAsync("USER\r\n", TestContext.CancellationToken);

        Assert.AreEqual(Greeting + "501 Syntax error in arguments\r\n", written);
    }

    [TestMethod]
    public async Task Pass_WithoutUser_Answers503WithoutAskingThePolicy()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted);

        var written = await ServeAsync("PASS secret\r\n", TestContext.CancellationToken, policy);

        Assert.AreEqual(Greeting + "503 Send USER first\r\n", written);
        Assert.IsEmpty(policy.Logins);
    }

    [TestMethod]
    public async Task Pass_AfterARefusal_NeedsUserAgain()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.RefusedCredentials);

        var written = await ServeAsync("USER tester\r\nPASS wrong\r\nPASS again\r\n", TestContext.CancellationToken, policy);

        Assert.EndsWith("530 Login incorrect\r\n503 Send USER first\r\n", written);
        Assert.HasCount(1, policy.Logins);
    }

    [TestMethod]
    [DataRow("USER other")]
    [DataRow("PASS other")]
    public async Task UserOrPass_AfterLogin_Answers503(string command)
    {
        var written = await ServeLoggedInAsync(command + "\r\n", TestContext.CancellationToken);

        Assert.AreEqual("503 Already logged in\r\n", written);
    }

    [TestMethod]
    [DataRow("PWD")]
    [DataRow("CWD dir")]
    [DataRow("CDUP")]
    [DataRow("TYPE I")]
    [DataRow("MODE S")]
    [DataRow("STRU F")]
    [DataRow("ALLO 10")]
    [DataRow("ACCT x")]
    [DataRow("RETR a.txt")]
    [DataRow("FROB")]
    public async Task Command_BeforeLogin_Answers530(string command)
    {
        var written = await ServeAsync(command + "\r\n", TestContext.CancellationToken);

        Assert.AreEqual(Greeting + "530 Please log in with USER and PASS\r\n", written);
    }

    [TestMethod]
    [DataRow("SYST", "215 UNIX Type: L8\r\n")]
    [DataRow("NOOP", "200 NOOP ok\r\n")]
    [DataRow("OPTS UTF8 ON", "200 UTF8 set to on\r\n")]
    [DataRow("AUTH TLS", "534 TLS is not available\r\n")]
    [DataRow("PBSZ 0", "503 Send AUTH first\r\n")]
    [DataRow("PROT P", "503 Send PBSZ first\r\n")]
    [DataRow("QUIT", "221 Goodbye\r\n")]
    public async Task Command_AnsweredBeforeLogin_IsAnswered(string command, string reply)
    {
        var written = await ServeAsync(command + "\r\n", TestContext.CancellationToken);

        Assert.AreEqual(Greeting + reply, written);
    }
}
