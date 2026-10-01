using System.Text;
using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smtp.ScriptedMailAuthenticationPolicy;
using static Surl.Protocol.Smtp.SmtpTestExchange;

namespace Surl.Protocol.Smtp;

/// <summary>
/// <c>AUTH</c> (RFC 4954) as ADR-0049 section 7 and ADR-0053 decisions 1 to 3 decide: the
/// <c>EHLO</c> line, the framing the server owns, the replies for each way a login ends, when
/// <c>AUTH</c> is allowed, and what a login unlocks. The mechanisms themselves are the policy's,
/// here a scripted double.
/// </summary>
[TestClass]
public sealed class SmtpAuthTests
{
    private const string EhloReplyWithPlainAuth =
        "250-surl Hello\r\n250-SIZE 104857600\r\n250-8BITMIME\r\n250-SMTPUTF8\r\n250-PIPELINING\r\n250-ENHANCEDSTATUSCODES\r\n250 AUTH PLAIN\r\n";

    private const string PlainResponse = "AHVzZXIAc2VjcmV0";

    private const string Succeeded = "235 2.7.0 Authentication successful\r\n";

    private const string MailRequest = "MAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\nhello\r\n.\r\n";

    private const string MailReplies = "250 2.1.0 Sender OK\r\n250 2.1.5 Recipient OK\r\n354 End data with <CR><LF>.<CR><LF>\r\n250 2.0.0 Message accepted\r\n";

    private static readonly CheckedLogin AcceptedUser = new("PLAIN", "user", true);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_Ehlo_AdvertisesThePolicysMechanismsForThePlaintextConnectionLast()
    {
        var mailPolicy = new ScriptedMailAuthenticationPolicy(["CRAM-MD5", "NTLM"]);

        var (connection, _) = await ServeAsync(mailPolicy, "EHLO c\r\n");

        StringAssert.EndsWith(Utf8(connection.WrittenBytes), "250-ENHANCEDSTATUSCODES\r\n250 AUTH CRAM-MD5 NTLM\r\n");
        CollectionAssert.AreEqual(new TlsSession?[] { null }, mailPolicy.OffersAskedFor);
    }

    // ADR-0049 section 2: a plain-text mechanism is offered only over TLS, so the offer grows
    // after STARTTLS and the server asks for it afresh.
    [TestMethod]
    public async Task ServeAsync_EhloAfterStartTls_AdvertisesTheTlsOfferAfterStartTlsBefore()
    {
        var mailPolicy = new ScriptedMailAuthenticationPolicy(["CRAM-MD5"], ["CRAM-MD5", "PLAIN", "LOGIN"]);
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Ascii("EHLO c\r\nSTARTTLS\r\n", "EHLO c\r\n"));

        await new SmtpProtocolServer(new AnonymousAuthenticationPolicy(), mailPolicy, AnonymousStore(clock), isTlsUpgradeAvailable: true)
            .ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        var written = Utf8(connection.WrittenBytes);
        StringAssert.Contains(written, "250-STARTTLS\r\n250 AUTH CRAM-MD5\r\n220 2.0.0 Ready to start TLS\r\n");
        StringAssert.EndsWith(written, "250-ENHANCEDSTATUSCODES\r\n250 AUTH CRAM-MD5 PLAIN LOGIN\r\n");
        CollectionAssert.AreEqual(new[] { null, InMemoryConnection.DefaultUpgradeTlsSession }, mailPolicy.OffersAskedFor);
    }

    [TestMethod]
    public async Task ServeAsync_EhloWithNoMechanismOffered_LeavesTheAuthLineOut()
    {
        var (connection, _) = await ServeAsync(NoSaslMechanisms(), "EHLO c\r\n");

        Assert.AreEqual(Greeting + EhloReply, Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_AuthWithoutInitialResponse_Sends334RelaysTheResponseAndLogsIn()
    {
        var mailPolicy = PlainPolicy(Challenge([]), Ended(SaslLoginOutcome.Accepted, AcceptedUser));

        var (connection, log) = await ServeAsync(mailPolicy, $"EHLO c\r\nAUTH plain\r\n{PlainResponse}\r\n");

        Assert.AreEqual("334 \r\n" + Succeeded, Replies(connection));
        var start = mailPolicy.Starts.Single();
        Assert.AreEqual(new SaslExchangeStart("smtp", "plain", null, null), start);
        Assert.AreEqual("\0user\0secret", Encoding.ASCII.GetString(mailPolicy.Responses.Single()));
        CollectionAssert.AreEqual(new[] { "Login accepted: PLAIN user" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_AuthWithInitialResponse_HandsItToThePolicyDecoded()
    {
        var mailPolicy = PlainPolicy(Ended(SaslLoginOutcome.Accepted, AcceptedUser));

        var (connection, _) = await ServeAsync(mailPolicy, $"EHLO c\r\nAUTH PLAIN {PlainResponse}\r\n");

        Assert.AreEqual(Succeeded, Replies(connection));
        Assert.AreEqual("\0user\0secret", Encoding.ASCII.GetString(mailPolicy.Starts.Single().InitialResponse!.Value.Span));
        Assert.IsEmpty(mailPolicy.Responses);
    }

    [TestMethod]
    public async Task ServeAsync_AuthWithEqualsSign_HandsThePolicyAnEmptyInitialResponse()
    {
        var mailPolicy = PlainPolicy(Ended(SaslLoginOutcome.AcceptedUnchecked));

        var (connection, log) = await ServeAsync(mailPolicy, "EHLO c\r\nAUTH EXTERNAL =\r\n");

        Assert.AreEqual(Succeeded, Replies(connection));
        Assert.IsTrue(mailPolicy.Starts.Single().InitialResponse is { IsEmpty: true });
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_AuthWithATrailingSpace_SendsNoInitialResponse()
    {
        var mailPolicy = PlainPolicy(Ended(SaslLoginOutcome.AcceptedUnchecked));

        await ServeAsync(mailPolicy, "EHLO c\r\nAUTH PLAIN \r\n");

        Assert.IsNull(mailPolicy.Starts.Single().InitialResponse);
    }

    [TestMethod]
    public async Task ServeAsync_TwoChallenges_SendsEachInBase64AndAnEmptyLineAsAnEmptyResponse()
    {
        var mailPolicy = PlainPolicy(Challenge("Username:"u8.ToArray()), Challenge("rspauth=1"u8.ToArray()), Ended(SaslLoginOutcome.Accepted, AcceptedUser));

        var (connection, _) = await ServeAsync(mailPolicy, "EHLO c\r\nAUTH LOGIN\r\ndXNlcg==\r\n\r\n");

        Assert.AreEqual("334 VXNlcm5hbWU6\r\n334 cnNwYXV0aD0x\r\n" + Succeeded, Replies(connection));
        Assert.AreEqual("user", Encoding.ASCII.GetString(mailPolicy.Responses[0]));
        Assert.IsEmpty(mailPolicy.Responses[1]);
    }

    [TestMethod]
    public async Task ServeAsync_RefusedLogin_Answers535WritesTheNoteFirstAndMailStaysRefused()
    {
        var mailPolicy = PlainPolicy(Ended(SaslLoginOutcome.RefusedCredentials, new CheckedLogin("PLAIN", "user", false)));

        var (connection, log) = await ServeAsync(mailPolicy, $"EHLO c\r\nAUTH PLAIN {PlainResponse}\r\nMAIL FROM:<a@x>\r\n");

        Assert.AreEqual("535 5.7.8 Authentication credentials invalid\r\n530 5.7.0 Authentication required\r\n", Replies(connection));
        Assert.AreEqual("Login refused: PLAIN user", log.Notes[0]);
    }

    // ADR-0057 decision 4: a refused GSSAPI ticket's reason follows the login note in the
    // verbose log, and nothing else of the ticket does.
    [TestMethod]
    public async Task ServeAsync_RefusedGssapiTicket_NotesTheKerberosReasonAfterTheLogin()
    {
        var mailPolicy = PlainPolicy(
            Ended(SaslLoginOutcome.RefusedCredentials, new CheckedLogin("GSSAPI", null, false)) with { RefusalNote = "Kerberos: ticket expired" });

        var (connection, log) = await ServeAsync(mailPolicy, "EHLO c\r\nAUTH GSSAPI YQ==\r\n");

        Assert.AreEqual("535 5.7.8 Authentication credentials invalid\r\n", Replies(connection));
        CollectionAssert.AreEqual(new[] { "Login refused: GSSAPI", "Kerberos: ticket expired" }, log.Notes.ToArray());
    }

    // ADR-0049 section 1: a plain-text mechanism started without TLS is refused before any
    // credential is read, so no 334 goes out and nothing more is read for it.
    [TestMethod]
    public async Task ServeAsync_PlainTextMechanismRefusedWithoutTls_Answers538WithoutAContinuation()
    {
        var mailPolicy = new ScriptedMailAuthenticationPolicy(["CRAM-MD5"], null, Ended(SaslLoginOutcome.RefusedPlaintext));

        var (connection, log) = await ServeAsync(mailPolicy, "EHLO c\r\nAUTH LOGIN\r\nNOOP\r\n");

        StringAssert.Contains(Utf8(connection.WrittenBytes), "250 AUTH CRAM-MD5\r\n538 5.7.11 Encryption required for requested authentication mechanism\r\n250 2.0.0 OK\r\n");
        Assert.IsEmpty(mailPolicy.Responses);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_UnknownMechanism_Answers504()
    {
        var mailPolicy = PlainPolicy(Ended(SaslLoginOutcome.RefusedMechanism));

        var (connection, _) = await ServeAsync(mailPolicy, "EHLO c\r\nAUTH SCRAM-SHA-256\r\n");

        Assert.AreEqual("504 5.5.4 Unrecognized authentication type\r\n", Replies(connection));
        Assert.AreEqual("SCRAM-SHA-256", mailPolicy.Starts.Single().Mechanism);
    }

    [TestMethod]
    public async Task ServeAsync_ClientCancelsWithAsterisk_Answers501AndStaysLoggedOut()
    {
        var mailPolicy = PlainPolicy(Challenge([]));

        var (connection, _) = await ServeAsync(mailPolicy, "EHLO c\r\nAUTH PLAIN\r\n*\r\nMAIL FROM:<a@x>\r\n");

        Assert.AreEqual("334 \r\n501 5.7.0 Authentication cancelled\r\n530 5.7.0 Authentication required\r\n", Replies(connection));
        Assert.IsEmpty(mailPolicy.Responses);
    }

    [TestMethod]
    [DataRow("!!!!", DisplayName = "not base64")]
    [DataRow("AHVz ZXIA", DisplayName = "a space inside")]
    public async Task ServeAsync_ResponseNotBase64_Answers501AndTheSessionGoesOn(string response)
    {
        var mailPolicy = PlainPolicy(Challenge([]));

        var (connection, _) = await ServeAsync(mailPolicy, $"EHLO c\r\nAUTH PLAIN\r\n{response}\r\nNOOP\r\n");

        Assert.AreEqual("334 \r\n501 5.5.2 Cannot decode response\r\n250 2.0.0 OK\r\n", Replies(connection));
        Assert.IsEmpty(mailPolicy.Responses);
    }

    [TestMethod]
    [DataRow("AUTH PLAIN !!!!", DisplayName = "not base64")]
    [DataRow("AUTH PLAIN *", DisplayName = "an asterisk")]
    public async Task ServeAsync_InitialResponseNotBase64_Answers501WithoutStartingAnExchange(string command)
    {
        var mailPolicy = PlainPolicy();

        var (connection, _) = await ServeAsync(mailPolicy, $"EHLO c\r\n{command}\r\n");

        Assert.AreEqual("501 5.5.2 Cannot decode response\r\n", Replies(connection));
        Assert.IsEmpty(mailPolicy.Starts);
    }

    [TestMethod]
    public async Task ServeAsync_AuthTwice_Answers503TheSecondTime()
    {
        var mailPolicy = PlainPolicy(Ended(SaslLoginOutcome.Accepted, AcceptedUser));

        var (connection, _) = await ServeAsync(mailPolicy, $"EHLO c\r\nAUTH PLAIN {PlainResponse}\r\nAUTH PLAIN {PlainResponse}\r\n");

        Assert.AreEqual(Succeeded + "503 5.5.1 Already authenticated\r\n", Replies(connection));
        Assert.HasCount(1, mailPolicy.Starts);
    }

    [TestMethod]
    public async Task ServeAsync_AuthAfterMail_Answers503()
    {
        var mailPolicy = PlainPolicy();
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Ascii($"EHLO c\r\nMAIL FROM:<a@x>\r\nAUTH PLAIN {PlainResponse}\r\n"));

        await new SmtpProtocolServer(new AnonymousAuthenticationPolicy(), mailPolicy, AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual("250 2.1.0 Sender OK\r\n503 5.5.1 AUTH not permitted during a mail transaction\r\n", Replies(connection));
        Assert.IsEmpty(mailPolicy.Starts);
    }

    [TestMethod]
    [DataRow("AUTH PLAIN\r\n", DisplayName = "before any hello")]
    [DataRow("HELO c\r\nAUTH PLAIN\r\n", DisplayName = "after HELO")]
    public async Task ServeAsync_AuthWithoutEhlo_Answers503(string request)
    {
        var mailPolicy = PlainPolicy();

        var (connection, _) = await ServeAsync(mailPolicy, request);

        StringAssert.EndsWith(Utf8(connection.WrittenBytes), "503 5.5.1 Send EHLO first\r\n");
        Assert.IsEmpty(mailPolicy.Starts);
    }

    [TestMethod]
    public async Task ServeAsync_AuthWithNoArgument_Answers501()
    {
        var (connection, _) = await ServeAsync(PlainPolicy(), "EHLO c\r\nAUTH\r\n");

        Assert.AreEqual("501 5.5.4 Syntax: AUTH <mechanism> [<initial response>]\r\n", Replies(connection));
    }

    // ADR-0053 decision 3: without --allow-anonymous, MAIL is 530 until a login, and then goes
    // through; the login stays across RSET and the Received field says ESMTPA.
    [TestMethod]
    public async Task ServeAsync_MailRefusedBeforeLogin_IsAcceptedAfterItAndStoredAsEsmtpa()
    {
        var mailPolicy = PlainPolicy(Ended(SaslLoginOutcome.Accepted, AcceptedUser));
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);

        var (connection, _) = await ServeAsync(mailPolicy, $"EHLO c\r\nMAIL FROM:<a@x>\r\nAUTH PLAIN {PlainResponse}\r\nRSET\r\n{MailRequest}", store, clock);

        Assert.AreEqual("530 5.7.0 Authentication required\r\n" + Succeeded + "250 2.0.0 Reset\r\n" + MailReplies, Replies(connection));
        CollectionAssert.AreEqual(new[] { TraceFields("a@x", protocol: "ESMTPA") + "hello\r\n" }, Inbox(store, string.Empty).ToList());
    }

    [TestMethod]
    public async Task ServeAsync_LoginOnImplicitTls_IsStoredAsEsmtpsa()
    {
        var mailPolicy = PlainPolicy(Ended(SaslLoginOutcome.Accepted, AcceptedUser));
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var connection = new InMemoryConnection(Ascii($"EHLO c\r\nAUTH PLAIN {PlainResponse}\r\n{MailRequest}"), initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        await new SmtpProtocolServer(new UnitTestRefusingPolicy(), mailPolicy, store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, mailPolicy.Starts.Single().TlsSession);
        CollectionAssert.AreEqual(new[] { TraceFields("a@x", protocol: "ESMTPSA") + "hello\r\n" }, Inbox(store, string.Empty).ToList());
    }

    [TestMethod]
    public async Task ServeAsync_StartTlsAfterALogin_LogsTheSessionOut()
    {
        var mailPolicy = PlainPolicy(Ended(SaslLoginOutcome.Accepted, AcceptedUser));
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Ascii($"EHLO c\r\nAUTH PLAIN {PlainResponse}\r\nSTARTTLS\r\n", "EHLO c\r\nMAIL FROM:<a@x>\r\n"));

        await new SmtpProtocolServer(new UnitTestRefusingPolicy(), mailPolicy, AnonymousStore(clock), isTlsUpgradeAvailable: true)
            .ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        StringAssert.EndsWith(Utf8(connection.WrittenBytes), "250 AUTH PLAIN\r\n530 5.7.0 Authentication required\r\n");
    }

    // ADR-0049 section 6: the note travels on the step that decided the credentials, even a
    // challenge (the bearer mechanisms' error challenge), and is written before the 334.
    [TestMethod]
    public async Task ServeAsync_ChallengeCarryingANote_WritesTheNoteAndThenTheFinalRefusal()
    {
        var mailPolicy = PlainPolicy(
            Challenge("{\"status\":\"invalid_token\"}"u8.ToArray(), new CheckedLogin("OAUTHBEARER", CheckedLogin.BearerTokenUser, false)),
            Ended(SaslLoginOutcome.RefusedCredentials));

        var (connection, log) = await ServeAsync(mailPolicy, "EHLO c\r\nAUTH OAUTHBEARER bixhPXVzZXIsAWF1dGg9QmVhcmVyIHRvawEB\r\nAQ==\r\n");

        Assert.AreEqual("334 eyJzdGF0dXMiOiJpbnZhbGlkX3Rva2VuIn0=\r\n535 5.7.8 Authentication credentials invalid\r\n", Replies(connection));
        CollectionAssert.AreEqual(new[] { "Login refused: OAUTHBEARER bearer token" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ResponseLineTooLong_Answers500AndCloses()
    {
        var limits = ExchangeLimits.Default with { MaxLineBytes = 64 };

        var (connection, _) = await ServeAsync(PlainPolicy(Challenge([])), "EHLO c\r\nAUTH PLAIN\r\n" + new string('A', 100) + "\r\nNOOP\r\n", limits: limits);

        Assert.AreEqual("334 \r\n500 5.5.6 Command line too long\r\n", Replies(connection));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_ResponseNotCompleteWithinTheHeadTimeout_Answers421AndCloses()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Ascii("EHLO c\r\nAUTH PLAIN\r\nAHV"), peerHalfClosesWhenExhausted: false);

        var serving = new SmtpProtocolServer(new AnonymousAuthenticationPolicy(), PlainPolicy(Challenge([])), AnonymousStore(clock))
            .ServeAsync(connection, Context(clock, TestContext.CancellationToken));
        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        Assert.AreEqual("334 \r\n421 4.4.2 surl Timeout waiting for a command, closing\r\n", Replies(connection));
    }

    [TestMethod]
    public async Task ServeAsync_PeerClosesDuringTheExchange_EndsTheSessionWithoutAReply()
    {
        var (connection, _) = await ServeAsync(PlainPolicy(Challenge([])), "EHLO c\r\nAUTH PLAIN\r\n");

        Assert.AreEqual("334 \r\n", Replies(connection));
        Assert.IsFalse(connection.WritesCompleted);
    }

    private static ScriptedMailAuthenticationPolicy PlainPolicy(params SaslLoginStep[] steps) => new(["PLAIN"], null, steps);

    // The replies after the greeting and the EHLO capabilities, whichever mechanisms they list.
    private static string Replies(InMemoryConnection connection)
    {
        var written = Utf8(connection.WrittenBytes);
        var afterEhlo = written.IndexOf("250 ENHANCEDSTATUSCODES\r\n", StringComparison.Ordinal) is var plain and >= 0
            ? plain + "250 ENHANCEDSTATUSCODES\r\n".Length
            : written.IndexOf("\r\n", written.IndexOf("250 AUTH ", StringComparison.Ordinal), StringComparison.Ordinal) + 2;
        return written[afterEhlo..];
    }

    private async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(
        ScriptedMailAuthenticationPolicy mailPolicy,
        string request,
        MailboxStore? store = null,
        ManualTimeProvider? clock = null,
        ExchangeLimits? limits = null)
    {
        clock ??= new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(Ascii(request));
        await new SmtpProtocolServer(new UnitTestRefusingPolicy(), mailPolicy, store ?? AnonymousStore(clock))
            .ServeAsync(connection, Context(clock, TestContext.CancellationToken, limits, log));
        return (connection, log);
    }
}
