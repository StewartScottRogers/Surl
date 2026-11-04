using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 decision 10's <c>AUTHENTICATE</c>: ADR-0049's SASL exchange, framed by the server
/// and decided by the policy, answered in ADR-0049 section 7's IMAP words.
/// </summary>
[TestClass]
public sealed class ImapAuthenticateTests
{
    private static readonly CheckedLogin AcceptedLogin = new("PLAIN", "u", true);
    private static readonly CheckedLogin RefusedLogin = new("PLAIN", "u", false);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_AuthenticateAccepted_WritesTheNoteAndLogsTheAccountIn()
    {
        var log = new RecordingExchangeLog();
        var policy = Policy(ScriptedLoginPolicy.Challenge([]), ScriptedLoginPolicy.Ended(SaslLoginOutcome.Accepted, AcceptedLogin));

        var (responses, store) = await ServeAsync(policy, "a AUTHENTICATE plain\r\nAHUAcA==\r\nb SELECT INBOX\r\n", log);

        Assert.StartsWith("+ \r\na OK AUTHENTICATE completed\r\n* FLAGS", responses);
        Assert.AreEqual(AcceptedLogin.Note, log.Notes.Single());
        var start = policy.Starts.Single();
        Assert.AreEqual("PLAIN", start.Mechanism);
        Assert.IsNull(start.InitialResponse);
        Assert.AreEqual("imap", start.Scheme);
        Assert.IsNull(start.TlsSession);
        Assert.AreEqual("\0u\0p", Encoding.ASCII.GetString(policy.Responses.Single()));
        Assert.IsNotNull(store);
    }

    [TestMethod]
    public async Task ServeAsync_AuthenticateWithAnInitialResponse_HandsItToTheExchange()
    {
        var policy = Policy(ScriptedLoginPolicy.Ended(SaslLoginOutcome.AcceptedUnchecked));

        var (responses, _) = await ServeAsync(policy, "a AUTHENTICATE PLAIN AHUAcA==\r\nb AUTHENTICATE PLAIN =\r\n");

        Assert.AreEqual("a OK AUTHENTICATE completed\r\nb BAD Already authenticated\r\n", responses);
        Assert.AreEqual("\0u\0p", Encoding.ASCII.GetString(policy.Starts.Single().InitialResponse!.Value.Span));
    }

    [TestMethod]
    public async Task ServeAsync_AuthenticateWithAnEmptyInitialResponse_HandsAnEmptyOneToTheExchange()
    {
        var policy = Policy(ScriptedLoginPolicy.Ended(SaslLoginOutcome.Accepted));

        var (responses, _) = await ServeAsync(policy, "a AUTHENTICATE EXTERNAL =\r\n");

        Assert.AreEqual("a OK AUTHENTICATE completed\r\n", responses);
        var initialResponse = policy.Starts.Single().InitialResponse;
        Assert.IsTrue(initialResponse.HasValue);
        Assert.IsTrue(initialResponse.Value.IsEmpty);
    }

    [TestMethod]
    public async Task ServeAsync_AuthenticateRefused_WritesTheNoteAndStaysLoggedOut()
    {
        var log = new RecordingExchangeLog();
        var policy = Policy(ScriptedLoginPolicy.Ended(SaslLoginOutcome.RefusedCredentials, RefusedLogin));

        var (responses, _) = await ServeAsync(policy, "a AUTHENTICATE PLAIN AHUAeA==\r\nb SELECT INBOX\r\n", log);

        Assert.AreEqual("a NO [AUTHENTICATIONFAILED] Authentication failed\r\nb NO [AUTHENTICATIONFAILED] Authentication required\r\n", responses);
        Assert.AreEqual(RefusedLogin.Note, log.Notes[0]);
    }

    // ADR-0057 decision 4: a refused GSSAPI ticket's reason follows the login note in the
    // verbose log, and nothing else of the ticket does.
    [TestMethod]
    public async Task ServeAsync_RefusedGssapiTicket_NotesTheKerberosReasonAfterTheLogin()
    {
        var log = new RecordingExchangeLog();
        var policy = Policy(
            ScriptedLoginPolicy.Ended(SaslLoginOutcome.RefusedCredentials, new CheckedLogin("GSSAPI", null, false)) with { RefusalNote = "Kerberos: ticket expired" });

        var (responses, _) = await ServeAsync(policy, "a AUTHENTICATE GSSAPI YQ==\r\n", log);

        Assert.AreEqual("a NO [AUTHENTICATIONFAILED] Authentication failed\r\n", responses);
        CollectionAssert.AreEqual(new[] { "Login refused: GSSAPI", "Kerberos: ticket expired" }, log.Notes.ToArray());
    }

    [TestMethod]
    [DataRow(SaslLoginOutcome.RefusedPlaintext, "a NO [PRIVACYREQUIRED] Encryption required")]
    [DataRow(SaslLoginOutcome.RefusedMechanism, "a NO Unsupported authentication mechanism")]
    public async Task ServeAsync_AuthenticateRefusedWithoutACheck_AnswersInImapWords(SaslLoginOutcome outcome, string expected)
    {
        var log = new RecordingExchangeLog();
        var policy = Policy(ScriptedLoginPolicy.Ended(outcome));

        var (responses, _) = await ServeAsync(policy, "a AUTHENTICATE X-UNKNOWN\r\n", log);

        Assert.AreEqual(expected + "\r\n", responses);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_ChallengeCarryingANote_WritesItBeforeTheChallenge()
    {
        var log = new RecordingExchangeLog();
        var policy = Policy(
            ScriptedLoginPolicy.Challenge("{\"status\":\"invalid_token\"}"u8.ToArray(), RefusedLogin),
            ScriptedLoginPolicy.Ended(SaslLoginOutcome.RefusedCredentials));

        var (responses, _) = await ServeAsync(policy, "a AUTHENTICATE OAUTHBEARER bixhPXUsAQE=\r\nAQ==\r\n", log);

        Assert.AreEqual("+ eyJzdGF0dXMiOiJpbnZhbGlkX3Rva2VuIn0=\r\na NO [AUTHENTICATIONFAILED] Authentication failed\r\n", responses);
        Assert.AreEqual(RefusedLogin.Note, log.Notes.Single());
        Assert.AreEqual("\u0001", Encoding.ASCII.GetString(policy.Responses.Single()));
    }

    [TestMethod]
    public async Task ServeAsync_ClientCancelsWithAStar_AnswersCancelledAndGoesOn()
    {
        var policy = Policy(ScriptedLoginPolicy.Challenge("Username:"u8.ToArray()));

        var (responses, _) = await ServeAsync(policy, "a AUTHENTICATE LOGIN\r\n*\r\nb NOOP\r\n");

        Assert.AreEqual("+ VXNlcm5hbWU6\r\na BAD Authentication cancelled\r\nb OK NOOP completed\r\n", responses);
        Assert.IsEmpty(policy.Responses);
    }

    [TestMethod]
    public async Task ServeAsync_ContinuationThatIsNotBase64_AnswersCannotDecodeAndGoesOn()
    {
        var policy = Policy(ScriptedLoginPolicy.Challenge([]));

        var (responses, _) = await ServeAsync(policy, "a AUTHENTICATE PLAIN\r\nnot base64!\r\nb NOOP\r\n");

        Assert.AreEqual("+ \r\na BAD Cannot decode response\r\nb OK NOOP completed\r\n", responses);
    }

    [TestMethod]
    [DataRow("a AUTHENTICATE PLAIN !!!!\r\n")]
    [DataRow("a AUTHENTICATE PLAIN *\r\n")]
    public async Task ServeAsync_InitialResponseThatCannotBeDecoded_AnswersCannotDecodeWithoutAnExchange(string request)
    {
        var policy = Policy();

        var (responses, _) = await ServeAsync(policy, request);

        Assert.AreEqual("a BAD Cannot decode response\r\n", responses);
        Assert.IsEmpty(policy.Starts);
    }

    [TestMethod]
    [DataRow("a AUTHENTICATE\r\n")]
    [DataRow("a AUTHENTICATE \r\n")]
    [DataRow("a AUTHENTICATE PLAIN(\r\n")]
    [DataRow("a AUTHENTICATE PLAIN \r\n")]
    [DataRow("a AUTHENTICATE PLAIN AHUAcA== x\r\n")]
    public async Task ServeAsync_AuthenticateMalformed_AnswersInvalidArguments(string request)
    {
        var policy = Policy();

        var (responses, _) = await ServeAsync(policy, request);

        Assert.AreEqual("a BAD Invalid arguments\r\n", responses);
        Assert.IsEmpty(policy.Starts);
    }

    [TestMethod]
    public async Task ServeAsync_AuthenticateOverTls_HandsTheTlsSessionToTheExchange()
    {
        var clock = new ManualTimeProvider();
        var policy = Policy(ScriptedLoginPolicy.Ended(SaslLoginOutcome.Accepted));
        var connection = new InMemoryConnection(Bytes("a AUTHENTICATE PLAIN AHUAcA==\r\n"), initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        await Server(AnonymousStore(clock), policy).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, policy.Starts.Single().TlsSession);
    }

    [TestMethod]
    public async Task ServeAsync_ContinuationPastTheLineLimit_AnswersBadWithTheTagAndCloses()
    {
        var clock = new ManualTimeProvider();
        var policy = Policy(ScriptedLoginPolicy.Challenge([]));
        var limits = ExchangeLimits.Default with { MaxLineBytes = 30 };

        var connection = await ImapTestExchange.ServeAsync(
            Server(AnonymousStore(clock), policy), "a AUTHENTICATE PLAIN\r\n" + new string('A', 40) + "\r\nb NOOP\r\n", clock, TestContext.CancellationToken, limits);

        Assert.AreEqual("+ \r\na BAD Command line too long\r\n", AfterGreeting(connection));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_ContinuationNotWithinTheHeadTimeout_SaysByeAndCloses()
    {
        var clock = new ManualTimeProvider();
        var policy = Policy(ScriptedLoginPolicy.Challenge([]));
        var connection = new InMemoryConnection(Bytes("a AUTHENTICATE PLAIN\r\nAH"), peerHalfClosesWhenExhausted: false);

        var serving = Server(AnonymousStore(clock), policy).ServeAsync(connection, Context(clock, TestContext.CancellationToken));
        while (clock.PendingTimerCount < 1 || !Utf8(connection.WrittenBytes).Contains("+ \r\n", StringComparison.Ordinal))
        {
            await Task.Yield();
        }

        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        Assert.AreEqual("+ \r\n* BYE surl Timeout waiting for a command, closing\r\n", AfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_PeerClosesDuringTheExchange_EndsTheSession()
    {
        var policy = Policy(ScriptedLoginPolicy.Challenge([]));

        var (responses, _) = await ServeAsync(policy, "a AUTHENTICATE PLAIN\r\n");

        Assert.AreEqual("+ \r\n", responses);
    }

    private static ScriptedLoginPolicy Policy(params SaslLoginStep[] steps) => new() { SaslMechanisms = [], Steps = steps };

    private async Task<(string Responses, Surl.MailStore.MailboxStore Store)> ServeAsync(ScriptedLoginPolicy policy, string request, IExchangeLog? log = null)
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var connection = await ImapTestExchange.ServeAsync(Server(store, policy), request, clock, TestContext.CancellationToken, log: log);
        return (AfterGreeting(connection), store);
    }
}
