using System.Text;
using Surl.LineProtocol;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smtp.SmtpTestExchange;

namespace Surl.Protocol.Smtp;

/// <summary>
/// The SMTP server half of an upstream test case (ADR-0080 decision 2), driven over an in-memory
/// connection with inline cases.
/// </summary>
[TestClass]
public sealed class SmtpUpstreamTestCaseServerTests
{
    private const string Envelope = "EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\n";

    private const string EnvelopeReplies =
        "250 surl Hello\r\n250 2.1.0 Sender OK\r\n250 2.1.5 Recipient OK\r\n354 End data with <CR><LF>.<CR><LF>\r\n";

    private static readonly Dictionary<string, byte[]> NoParts = [];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullReplies_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SmtpUpstreamTestCaseServer(null!, new AnonymousAuthenticationPolicy()));
    }

    [TestMethod]
    public void Constructor_NullAuthenticationPolicy_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SmtpUpstreamTestCaseServer(new MailUpstreamTestCaseReplies(NoParts, []), null!));
    }

    [TestMethod]
    public void Schemes_IsSmtp()
    {
        var server = new SmtpUpstreamTestCaseServer(new MailUpstreamTestCaseReplies(NoParts, []), new AnonymousAuthenticationPolicy());

        CollectionAssert.AreEqual(new[] { "smtp" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ReplyWelcome_ReplacesTheGreeting()
    {
        var replies = Case("REPLY welcome 220 upstream test server");

        var written = await ServeAsync(replies, "QUIT\r\n");

        Assert.AreEqual("220 upstream test server\r\n221 2.0.0 Bye\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_NoReplyWelcome_GreetsAsTheServerDoes()
    {
        var written = await ServeAsync(Case(), "QUIT\r\n");

        Assert.AreEqual(Greeting + "221 2.0.0 Bye\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_ReplyForACommand_AnswersTheCaseTextAndDoesNotRunIt()
    {
        var replies = Case("REPLY QUIT 250 not leaving");

        var written = await ServeAsync(replies, "QUIT\r\nNOOP\r\n");

        Assert.AreEqual(Greeting + "250 not leaving\r\n250 2.0.0 OK\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_CountLimitedReply_AnswersTheCaseTextThenTheServersOwn()
    {
        var replies = Case("REPLY NOOP 250 scripted", "COUNT NOOP 1");

        var written = await ServeAsync(replies, "NOOP\r\nNOOP\r\n");

        Assert.AreEqual(Greeting + "250 scripted\r\n250 2.0.0 OK\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_ReplyLf_EndsTheAnswerWithABareLf()
    {
        var replies = Case("REPLYLF NOOP 250 lf only");

        var written = await ServeAsync(replies, "NOOP\r\n");

        Assert.AreEqual(Greeting + "250 lf only\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_EhloWithCapaAndAuth_ListsOnlyTheCasesExtensions()
    {
        var replies = Case("CAPA SIZE \"X FOO\"", "AUTH PLAIN LOGIN");

        var written = await ServeAsync(replies, "EHLO c\r\n");

        Assert.AreEqual(Greeting + "250-surl Hello\r\n250-SIZE\r\n250-X FOO\r\n250 AUTH PLAIN LOGIN\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_EhloWithNoCapaOrAuth_ListsOnlyTheHelloLine()
    {
        var written = await ServeAsync(Case(), "EHLO c\r\n");

        Assert.AreEqual(Greeting + "250 surl Hello\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_HeloInACase_AnswersAsTheServerDoes()
    {
        var written = await ServeAsync(Case("CAPA SIZE"), "HELO c\r\n");

        Assert.AreEqual(Greeting + "250 surl Hello\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_MailWithoutALogin_IsAccepted()
    {
        var policy = new UnitTestRefusingPolicy();
        var replies = Case();
        var connection = new InMemoryConnection(Ascii("EHLO c\r\nMAIL FROM:<a@x>\r\n"));

        await new SmtpProtocolServer(policy, NoSaslMechanisms(), AnonymousStore(new ManualTimeProvider()), isTlsUpgradeAvailable: false, replies)
            .ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual(Greeting + "250 surl Hello\r\n250 2.1.0 Sender OK\r\n", Utf8(connection.WrittenBytes));
        Assert.IsEmpty(policy.Logins);
    }

    [TestMethod]
    public async Task ServeAsync_DataBody_IsRecordedAsSentAndNotDelivered()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var replies = Case();
        var connection = new InMemoryConnection(Ascii(Envelope + "Subject: hi\r\n..dot\r\n.\r\nQUIT\r\n"));

        await new SmtpProtocolServer(new AnonymousAuthenticationPolicy(), NoSaslMechanisms(), store, isTlsUpgradeAvailable: false, replies)
            .ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(Greeting + EnvelopeReplies + "250 2.0.0 Message accepted\r\n221 2.0.0 Bye\r\n", Utf8(connection.WrittenBytes));
        Assert.AreEqual("Subject: hi\r\n..dot\r\n.\r\n", Utf8(replies.UploadedBytes));
        Assert.IsEmpty(Inbox(store, string.Empty));
    }

    [TestMethod]
    public async Task ServeAsync_DataAfterRepliedMailAndRcpt_RecordsTheUpload()
    {
        var replies = Case("REPLY MAIL 250 Ok", "REPLY RCPT 250 Ok");
        var request = "EHLO c\r\nMAIL FROM:<a@x> RET=HDRS\r\nRCPT TO:<b@y> NOTIFY=SUCCESS\r\nDATA\r\nSubject: hi\r\n\r\nbody\r\n.\r\nQUIT\r\n";

        var written = await ServeAsync(replies, request);

        Assert.AreEqual(Greeting + "250 surl Hello\r\n250 Ok\r\n250 Ok\r\n354 End data with <CR><LF>.<CR><LF>\r\n250 2.0.0 Message accepted\r\n221 2.0.0 Bye\r\n", written);
        Assert.AreEqual("Subject: hi\r\n\r\nbody\r\n.\r\n", Utf8(replies.UploadedBytes));
    }

    [TestMethod]
    public async Task ServeAsync_RepliedRcptWithoutAMail_StillCountsAsARecipient()
    {
        var replies = Case("REPLY RCPT 250 Ok");

        var written = await ServeAsync(replies, "EHLO c\r\nRCPT TO:<b@y>\r\nDATA\r\nx\r\n.\r\n");

        Assert.Contains("354 ", written);
        Assert.AreEqual("x\r\n.\r\n", Utf8(replies.UploadedBytes));
    }

    [TestMethod]
    public async Task ServeAsync_DataWithAnArgument_Answers501()
    {
        var written = await ServeAsync(Case(), "DATA x\r\n");

        Assert.AreEqual(Greeting + "501 5.5.4 Syntax: DATA takes no argument\r\n", written);
    }

    [TestMethod]
    [DataRow("EHLO c\r\nMAIL FROM:<a@x>\r\nDATA\r\n", DisplayName = "after MAIL, before RCPT")]
    [DataRow("EHLO c\r\nDATA\r\n", DisplayName = "before MAIL")]
    public async Task ServeAsync_DataBeforeRcpt_Answers503(string request)
    {
        var written = await ServeAsync(Case(), request);

        Assert.EndsWith("503 5.5.1 Send RCPT first\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_DataBodyCutOff_RecordsNothing()
    {
        var replies = Case();

        var written = await ServeAsync(replies, Envelope + "Subject: hi\r\n");

        Assert.AreEqual(Greeting + EnvelopeReplies, written);
        Assert.IsEmpty(replies.UploadedBytes);
    }

    [TestMethod]
    public async Task ServeAsync_DataBodyPastTheUploadLimit_Answers552AndRecordsNothing()
    {
        var replies = Case();
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 20 };

        var written = await ServeAsync(replies, Envelope + new string('x', 100) + "\r\n.\r\nNOOP\r\n", limits);

        Assert.AreEqual(Greeting + EnvelopeReplies + "552 5.3.4 Message exceeds the size limit\r\n", written);
        Assert.IsEmpty(replies.UploadedBytes);
    }

    [TestMethod]
    [DataRow("VRFY", DisplayName = "VRFY")]
    [DataRow("EXPN", DisplayName = "EXPN")]
    public async Task ServeAsync_VrfyOrExpnWithCaseData_AnswersTheCaseData(string verb)
    {
        var replies = Case(new Dictionary<string, byte[]> { ["data"] = Encoding.ASCII.GetBytes("250 <recipient@example.com>\r\n") });

        var written = await ServeAsync(replies, $"EHLO c\r\n{verb} recipient\r\n");

        Assert.AreEqual(Greeting + "250 surl Hello\r\n250 <recipient@example.com>\r\n", written);
    }

    [TestMethod]
    [DataRow("VRFY x\r\n", "252 2.1.5 Cannot verify the user, but will accept the message\r\n", DisplayName = "VRFY with no data, before EHLO")]
    [DataRow("EXPN x\r\n", "252 2.1.5 Cannot expand the list, but will accept the message\r\n", DisplayName = "EXPN with no data, before EHLO")]
    [DataRow("VRFY\r\n", "501 5.5.4 Syntax: VRFY <address>\r\n", DisplayName = "VRFY with no argument")]
    [DataRow("EXPN\r\n", "501 5.5.4 Syntax: EXPN <list>\r\n", DisplayName = "EXPN with no argument")]
    public async Task ServeAsync_VrfyOrExpnWithoutCaseData_AnswersAsTheServerDoes(string line, string reply)
    {
        var written = await ServeAsync(Case(), line);

        Assert.AreEqual(Greeting + reply, written);
    }

    [TestMethod]
    public async Task ServeAsync_EveryCommandLine_IsRecordedWithCrlf()
    {
        var replies = Case("REPLY NOOP 250 scripted");

        await ServeAsync(replies, "EHLO c\r\nNOOP\r\nXYZZY\r\nQUIT\r\n");

        Assert.AreEqual("EHLO c\r\nNOOP\r\nXYZZY\r\nQUIT\r\n", Utf8(replies.ReceivedBytes));
    }

    [TestMethod]
    public async Task ServeAsync_NoCase_AnswersEhloWithItsOwnExtensionsAndRefusesMailBeforeALogin()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Ascii("EHLO c\r\nMAIL FROM:<a@x>\r\n"));

        await new SmtpProtocolServer(new UnitTestRefusingPolicy(), NoSaslMechanisms(), AnonymousStore(clock))
            .ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(Greeting + EhloReply + "530 5.7.0 Authentication required\r\n", Utf8(connection.WrittenBytes));
    }

    private static MailUpstreamTestCaseReplies Case(params string[] serverCommands) => new(NoParts, serverCommands);

    private static MailUpstreamTestCaseReplies Case(Dictionary<string, byte[]> parts) => new(parts, []);

    private async Task<string> ServeAsync(MailUpstreamTestCaseReplies replies, string request, ExchangeLimits? limits = null)
    {
        var connection = new InMemoryConnection(Ascii(request));

        await new SmtpUpstreamTestCaseServer(replies, new AnonymousAuthenticationPolicy())
            .ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken, limits));

        return Utf8(connection.WrittenBytes);
    }
}
