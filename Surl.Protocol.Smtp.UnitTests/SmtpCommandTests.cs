using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smtp.SmtpTestExchange;

namespace Surl.Protocol.Smtp;

/// <summary>
/// ADR-0053 decision 1's reply table and sequencing, command by command.
/// </summary>
[TestClass]
public sealed class SmtpCommandTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("EHLO\r\n", "501 Syntax: EHLO <domain>\r\n", DisplayName = "EHLO with no argument")]
    [DataRow("EHLO \r\n", "501 Syntax: EHLO <domain>\r\n", DisplayName = "EHLO with nothing after its space")]
    [DataRow("EHLO a\tb\r\n", "501 Syntax: EHLO <domain>\r\n", DisplayName = "EHLO with a control character")]
    [DataRow("EHLO a\u007Fb\r\n", "501 Syntax: EHLO <domain>\r\n", DisplayName = "EHLO with DEL")]
    [DataRow("HELO\r\n", "501 Syntax: HELO <domain>\r\n", DisplayName = "HELO with no argument")]
    [DataRow("HELO c\r\n", "250 surl Hello\r\n", DisplayName = "HELO")]
    [DataRow("ehlo c\r\n", EhloReply, DisplayName = "ehlo in lower case")]
    [DataRow("MAIL FROM:<a@x>\r\n", "503 5.5.1 Send EHLO or HELO first\r\n", DisplayName = "MAIL before EHLO")]
    [DataRow("RCPT TO:<b@y>\r\n", "503 5.5.1 Send MAIL first\r\n", DisplayName = "RCPT before MAIL")]
    [DataRow("DATA\r\n", "503 5.5.1 Send RCPT first\r\n", DisplayName = "DATA before MAIL")]
    [DataRow("DATA x\r\n", "501 5.5.4 Syntax: DATA takes no argument\r\n", DisplayName = "DATA with an argument")]
    [DataRow("RSET\r\n", "250 2.0.0 Reset\r\n", DisplayName = "RSET")]
    [DataRow("RSET x\r\n", "501 5.5.4 Syntax: RSET takes no argument\r\n", DisplayName = "RSET with an argument")]
    [DataRow("NOOP anything\r\n", "250 2.0.0 OK\r\n", DisplayName = "NOOP with an argument")]
    [DataRow("VRFY\r\n", "501 5.5.4 Syntax: VRFY <address>\r\n", DisplayName = "VRFY with no argument")]
    [DataRow("VRFY tester\r\n", "252 2.1.5 Cannot verify the user, but will accept the message\r\n", DisplayName = "VRFY before EHLO")]
    [DataRow("EXPN\r\n", "501 5.5.4 Syntax: EXPN <list>\r\n", DisplayName = "EXPN with no argument")]
    [DataRow("HELP me\r\n", "214 2.0.0 Commands: EHLO HELO STARTTLS AUTH MAIL RCPT DATA RSET NOOP VRFY EXPN HELP QUIT\r\n", DisplayName = "HELP with an argument")]
    [DataRow("QUIT now\r\n", "501 5.5.4 Syntax: QUIT takes no argument\r\n", DisplayName = "QUIT with an argument")]
    [DataRow("STARTTLS\r\n", "454 4.7.0 TLS not available\r\n", DisplayName = "STARTTLS with no certificate")]
    [DataRow("STARTTLS x\r\n", "501 5.5.4 Syntax: STARTTLS takes no argument\r\n", DisplayName = "STARTTLS with an argument")]
    [DataRow("AUTH PLAIN\r\n", "502 5.5.1 Command not implemented\r\n", DisplayName = "AUTH, until BL-200")]
    [DataRow("BDAT 10 LAST\r\n", "502 5.5.1 Command not implemented\r\n", DisplayName = "BDAT")]
    [DataRow("VERB\r\n", "502 5.5.1 Command not implemented\r\n", DisplayName = "VERB")]
    [DataRow("XYZZY\r\n", "500 5.5.2 Command not recognized\r\n", DisplayName = "an unknown command")]
    [DataRow("\r\n", "500 5.5.2 Command not recognized\r\n", DisplayName = "an empty line")]
    [DataRow(" NOOP\r\n", "500 5.5.2 Command not recognized\r\n", DisplayName = "a line starting with a space")]
    [DataRow("NO\nOP\r\n", "500 5.5.2 Command not recognized\r\n", DisplayName = "a bare LF")]
    [DataRow("NOOP x\ry\r\n", "500 5.5.2 Command not recognized\r\n", DisplayName = "a bare CR after the command")]
    [DataRow("NO\u0001OP\r\n", "500 5.5.2 Command not recognized\r\n", DisplayName = "a control character in the command")]
    public async Task ServeAsync_OneCommand_AnswersAsTheAdrSays(string line, string reply)
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AnonymousStore(clock), line + "QUIT\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual(Greeting + reply + "221 2.0.0 Bye\r\n", Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_NonAsciiByteInTheCommand_Answers500()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection([new byte[] { (byte)'N', 0xC3, 0xA9, (byte)'\r', (byte)'\n' }]);

        await Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(Greeting + "500 5.5.2 Command not recognized\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsOnATlsConnection_Answers503()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Ascii("STARTTLS\r\n"), initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        await Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(Greeting + "503 5.5.1 Already using TLS\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_SecondMail_Answers503()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AnonymousStore(clock), "EHLO c\r\nMAIL FROM:<a@x>\r\nMAIL FROM:<a@x>\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("250 2.1.0 Sender OK\r\n503 5.5.1 Sender already given\r\n", RepliesAfterHello(connection));
    }

    [TestMethod]
    [DataRow("RSET")]
    [DataRow("EHLO c")]
    [DataRow("HELO c")]
    public async Task ServeAsync_CommandThatEndsTheTransaction_MakesRcptNeedMailAgain(string command)
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AnonymousStore(clock), $"EHLO c\r\nMAIL FROM:<a@x>\r\n{command}\r\nRCPT TO:<b@y>\r\n", clock, TestContext.CancellationToken);

        StringAssert.EndsWith(RepliesAfterHello(connection), "503 5.5.1 Send MAIL first\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_DataAfterMailWithNoRecipient_Answers503()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AnonymousStore(clock), "EHLO c\r\nMAIL FROM:<a@x>\r\nDATA\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("250 2.1.0 Sender OK\r\n503 5.5.1 Send RCPT first\r\n", RepliesAfterHello(connection));
    }

    [TestMethod]
    [DataRow("MAIL", DisplayName = "no argument")]
    [DataRow("MAIL TO:<a@x>", DisplayName = "the wrong keyword")]
    [DataRow("MAIL FROM:a@x", DisplayName = "no brackets")]
    [DataRow("MAIL FROM:<a@x", DisplayName = "no closing bracket")]
    [DataRow("MAIL FROM:<Bob <a@x>", DisplayName = "a bracket inside the path")]
    [DataRow("MAIL FROM:<a@x>SIZE=1", DisplayName = "no space before a parameter")]
    [DataRow("MAIL FROM:<a..b@x>", DisplayName = "an invalid local part")]
    [DataRow("MAIL FROM:<a@>", DisplayName = "an empty domain")]
    [DataRow("MAIL FROM:  <a@x>", DisplayName = "two spaces after the colon")]
    public async Task ServeAsync_MailWithAnInvalidPath_Answers501(string command)
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AnonymousStore(clock), $"EHLO c\r\n{command}\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("501 5.1.7 Invalid sender address\r\n", RepliesAfterHello(connection));
    }

    [TestMethod]
    public async Task ServeAsync_MailWithAPathThatIsNotUtf8_Answers501()
    {
        var clock = new ManualTimeProvider();
        byte[] request = [.. "EHLO c\r\nMAIL FROM:<"u8, 0xE4, .. "@x>\r\n"u8];
        var connection = new InMemoryConnection([request]);

        await Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual("501 5.1.7 Invalid sender address\r\n", RepliesAfterHello(connection));
    }

    [TestMethod]
    [DataRow("MAIL FROM:<>", DisplayName = "the null reverse-path")]
    [DataRow("mail from: <a@x>", DisplayName = "lower case, one space after the colon")]
    [DataRow("MAIL FROM:<@r1,@r2:a@x> SIZE=53 BODY=8BITMIME SMTPUTF8 AUTH=<a@x>", DisplayName = "a source route and every parameter")]
    [DataRow("MAIL FROM:<\"a b\"@x>", DisplayName = "a quoted local part")]
    [DataRow("MAIL FROM:<bob>", DisplayName = "a local part with no domain")]
    public async Task ServeAsync_MailWithAValidPath_Answers250(string command)
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AnonymousStore(clock), $"EHLO c\r\n{command}\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("250 2.1.0 Sender OK\r\n", RepliesAfterHello(connection));
    }

    [TestMethod]
    [DataRow("MAIL FROM:<a@x> SIZE=104857601", "552 5.3.4 Message size exceeds the size limit", DisplayName = "SIZE past --max-filesize")]
    [DataRow("MAIL FROM:<a@x> FOO=1", "555 5.5.4 Unsupported parameter", DisplayName = "an unknown parameter")]
    [DataRow("MAIL FROM:<a@x> BODY=BINARYMIME", "501 5.5.4 Invalid BODY parameter", DisplayName = "BODY=BINARYMIME")]
    public async Task ServeAsync_MailWithARefusedParameter_AnswersItsRefusalAndStartsNoTransaction(string command, string reply)
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AnonymousStore(clock), $"EHLO c\r\n{command}\r\nRCPT TO:<b@y>\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual(reply + "\r\n503 5.5.1 Send MAIL first\r\n", RepliesAfterHello(connection));
    }

    [TestMethod]
    public async Task ServeAsync_MailWithAHugeSizeAndNoLimit_Answers250()
    {
        var clock = new ManualTimeProvider();
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 0 };

        var connection = await ServeAsync(AnonymousStore(clock), "EHLO c\r\nMAIL FROM:<a@x> SIZE=99999999999999999999\r\n", clock, TestContext.CancellationToken, limits);

        StringAssert.Contains(Utf8(connection.WrittenBytes), "250-SIZE 0\r\n");
        StringAssert.EndsWith(Utf8(connection.WrittenBytes), "250 2.1.0 Sender OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_MailWhenThePolicyRefusesAnonymousLogins_Answers530AndAsksOnce()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var policy = new UnitTestRefusingPolicy();
        var connection = new InMemoryConnection(Ascii("EHLO c\r\nMAIL FROM:<a@x>\r\nMAIL FROM:<a@x>\r\n"));

        await new SmtpProtocolServer(policy, AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));

        Assert.AreEqual("530 5.7.0 Authentication required\r\n530 5.7.0 Authentication required\r\n", RepliesAfterHello(connection));
        Assert.HasCount(1, policy.Logins);
        Assert.AreEqual(new PasswordLogin("smtp", null, null, null), policy.Logins[0]);
        Assert.AreEqual("MAIL refused: log in with AUTH first, or give --allow-anonymous", log.Notes[0]);
    }

    [TestMethod]
    [DataRow("RCPT TO:<>", "501 5.1.3 Invalid recipient address", DisplayName = "the null path")]
    [DataRow("RCPT", "501 5.1.3 Invalid recipient address", DisplayName = "no argument")]
    [DataRow("RCPT TO:<Bob <b@y>", "501 5.1.3 Invalid recipient address", DisplayName = "a bracket inside the path")]
    [DataRow("RCPT TO:<b@y> NOTIFY=NEVER", "555 5.5.4 Unsupported parameter", DisplayName = "a parameter")]
    [DataRow("RCPT TO:<postmaster>", "250 2.1.5 Recipient OK", DisplayName = "postmaster")]
    [DataRow("RCPT TO:<bob>", "250 2.1.5 Recipient OK", DisplayName = "a local part with no domain")]
    public async Task ServeAsync_Rcpt_AnswersAsTheAdrSays(string command, string reply)
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AnonymousStore(clock), $"EHLO c\r\nMAIL FROM:<a@x>\r\n{command}\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("250 2.1.0 Sender OK\r\n" + reply + "\r\n", RepliesAfterHello(connection));
    }

    [TestMethod]
    public async Task ServeAsync_101stRecipient_Answers452AndKeepsTheFirst100()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var recipients = string.Concat(Enumerable.Range(0, 101).Select(index => $"RCPT TO:<r{index}@y>\r\n"));

        var connection = await ServeAsync(store, $"EHLO c\r\nMAIL FROM:<a@x>\r\n{recipients}DATA\r\nhi\r\n.\r\n", clock, TestContext.CancellationToken);

        var replies = RepliesAfterHello(connection);
        StringAssert.Contains(replies, "250 2.1.5 Recipient OK\r\n452 4.5.3 Too many recipients\r\n354 ");
        Assert.HasCount(100, Inbox(store, string.Empty));
    }

    [TestMethod]
    public async Task ServeAsync_AfterQuit_ReadsNothingMore()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AnonymousStore(clock), "QUIT\r\nNOOP\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual(Greeting + "221 2.0.0 Bye\r\n", Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_PeerClosesWithoutQuit_EndsTheSession()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(AnonymousStore(clock), "NOOP\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual(Greeting + "250 2.0.0 OK\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public void Schemes_IsSmtp()
    {
        CollectionAssert.AreEqual(new[] { "smtp" }, Server(AnonymousStore(new ManualTimeProvider())).Schemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var store = AnonymousStore(new ManualTimeProvider());

        Assert.ThrowsExactly<ArgumentNullException>(() => new SmtpProtocolServer(null!, store));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SmtpProtocolServer(new AnonymousAuthenticationPolicy(), null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        var clock = new ManualTimeProvider();
        var server = Server(AnonymousStore(clock));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(clock, TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_Writes421AndCompletesWrites(ConnectionRefusal refusal)
    {
        var connection = new InMemoryConnection([]);

        await Server(AnonymousStore(new ManualTimeProvider())).WriteRefusalAsync(connection, refusal, TestContext.CancellationToken);

        Assert.AreEqual("421 4.3.2 surl Too many connections, closing\r\n", Encoding.ASCII.GetString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullConnection_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await Server(AnonymousStore(new ManualTimeProvider())).WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken));
    }

    private sealed class UnitTestRefusingPolicy : IAuthenticationPolicy
    {
        public List<PasswordLogin> Logins { get; } = [];

        public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken)
        {
            Logins.Add(login);
            return ValueTask.FromResult(PasswordLoginVerdict.RefusedAnonymous);
        }

        public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => throw new NotSupportedException();
    }
}
