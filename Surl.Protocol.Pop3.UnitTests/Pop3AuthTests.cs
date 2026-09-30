using System.Text;
using Surl.LineProtocol;
using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Pop3.Pop3TestExchange;

namespace Surl.Protocol.Pop3;

/// <summary>
/// ADR-0056 decision 4's <c>AUTH</c> rows and ADR-0049 sections 6 and 7: the SASL exchange the
/// server frames and the policy decides, its POP3 words, and the maildrop lock it takes.
/// </summary>
[TestClass]
public sealed class Pop3AuthTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_AuthAccepted_SendsTheChallengeAndOpensTheMaildrop()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var policy = new Pop3TestPolicy
        {
            SaslMechanisms = ["CRAM-MD5"],
            Steps = [Pop3TestPolicy.Challenge("abc"u8.ToArray()), Pop3TestPolicy.Ended(MailLoginOutcome.Accepted, new CheckedLogin("CRAM-MD5", "u", true))],
        };

        var connection = await ServeAsync(AccountStore(clock, Message), "auth cram-md5\r\ndXNlcg==\r\nSTAT\r\n", clock, TestContext.CancellationToken, policy, log: log);

        Assert.AreEqual("+ YWJj\r\n+OK Authentication successful\r\n+OK 1 133\r\n", RepliesAfterGreeting(connection));
        var start = policy.Starts.Single();
        Assert.AreEqual("pop3", start.Scheme);
        Assert.AreEqual("cram-md5", start.Mechanism);
        Assert.IsFalse(start.InitialResponse.HasValue);
        Assert.IsNull(start.TlsSession);
        Assert.AreEqual("user", Encoding.ASCII.GetString(policy.Responses.Single()));
        CollectionAssert.AreEqual(new[] { "Login accepted: CRAM-MD5 u", "Maildrop opened: 1 messages, 133 octets" }, log.Notes.ToList());
    }

    [TestMethod]
    public async Task ServeAsync_EmptyChallenge_IsAPlusAndASpace()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { Steps = [Pop3TestPolicy.Challenge([]), Pop3TestPolicy.Ended(MailLoginOutcome.Accepted)] };

        var connection = await ServeAsync(AccountStore(clock), "AUTH PLAIN\r\n\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("+ \r\n+OK Authentication successful\r\n", RepliesAfterGreeting(connection));
        Assert.IsEmpty(policy.Responses.Single());
    }

    [TestMethod]
    [DataRow("AUTH PLAIN AHUAcA==", "\0u\0p")]
    [DataRow("AUTH PLAIN =", "")]
    public async Task ServeAsync_InitialResponse_IsDecodedAndHandedToThePolicy(string line, string expected)
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { Steps = [Pop3TestPolicy.Ended(MailLoginOutcome.Accepted)] };

        var connection = await ServeAsync(AccountStore(clock), line + "\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("+OK Authentication successful\r\n", RepliesAfterGreeting(connection));
        Assert.AreEqual(expected, Encoding.ASCII.GetString(policy.Starts.Single().InitialResponse!.Value.Span));
    }

    [TestMethod]
    [DataRow("AUTH PLAIN !!")]
    [DataRow("AUTH PLAIN *")]
    public async Task ServeAsync_InitialResponseNotBase64_AnswersCannotDecodeWithoutStarting(string line)
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy();

        var connection = await ServeAsync(AccountStore(clock), line + "\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("-ERR Cannot decode response\r\n", RepliesAfterGreeting(connection));
        Assert.IsEmpty(policy.Starts);
    }

    [TestMethod]
    [DataRow("AUTH PLAIN a b")]
    [DataRow("AUTH PLAIN  a")]
    public async Task ServeAsync_AuthWithTooManyWords_AnswersInvalidArguments(string line)
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy();

        var connection = await ServeAsync(AccountStore(clock), line + "\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("-ERR Invalid arguments\r\n", RepliesAfterGreeting(connection));
        Assert.IsEmpty(policy.Starts);
    }

    // The note is written before the refusal, and the session stays unauthorized (ADR-0038).
    [TestMethod]
    public async Task ServeAsync_AuthRefused_NotesTheLoginAndStaysUnauthorized()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var policy = new Pop3TestPolicy
        {
            Steps = [Pop3TestPolicy.Challenge([]), Pop3TestPolicy.Ended(MailLoginOutcome.RefusedCredentials, new CheckedLogin("PLAIN", "u", false))],
        };

        var connection = await ServeAsync(AccountStore(clock), "AUTH PLAIN\r\nAHUAeA==\r\nSTAT\r\n", clock, TestContext.CancellationToken, policy, log: log);

        Assert.AreEqual("+ \r\n-ERR [AUTH] Authentication failed\r\n-ERR [AUTH] Authentication required\r\n", RepliesAfterGreeting(connection));
        Assert.AreEqual("Login refused: PLAIN u", log.Notes[0]);
    }

    // A plain-text mechanism over no TLS is refused before any credential is read (ADR-0049,
    // section 1).
    [TestMethod]
    [DataRow(MailLoginOutcome.RefusedPlaintext, "-ERR [AUTH] Encryption required")]
    [DataRow(MailLoginOutcome.RefusedMechanism, "-ERR Unsupported authentication mechanism")]
    public async Task ServeAsync_AuthRefusedAtOnce_AnswersInPop3Words(MailLoginOutcome outcome, string reply)
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { Steps = [Pop3TestPolicy.Ended(outcome)] };

        var connection = await ServeAsync(AccountStore(clock), "AUTH PLAIN AHUAcA==\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual(reply + "\r\n", RepliesAfterGreeting(connection));
        Assert.IsEmpty(policy.Responses);
    }

    [TestMethod]
    public async Task ServeAsync_ClientCancelsWithStar_AnswersCancelledAndGoesOn()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { Steps = [Pop3TestPolicy.Challenge([])] };

        var connection = await ServeAsync(AccountStore(clock), "AUTH LOGIN\r\n*\r\nNOOP\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("+ \r\n-ERR Authentication cancelled\r\n-ERR [AUTH] Authentication required\r\n", RepliesAfterGreeting(connection));
        Assert.IsEmpty(policy.Responses);
    }

    [TestMethod]
    public async Task ServeAsync_ContinuationNotBase64_AnswersCannotDecode()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { Steps = [Pop3TestPolicy.Challenge([])] };

        var connection = await ServeAsync(AccountStore(clock), "AUTH LOGIN\r\nnot base64\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("+ \r\n-ERR Cannot decode response\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_PeerClosesDuringTheExchange_EndsTheSession()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { Steps = [Pop3TestPolicy.Challenge([])] };

        var connection = await ServeAsync(AccountStore(clock), "AUTH LOGIN\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("+ \r\n", RepliesAfterGreeting(connection));
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_ContinuationTooLong_AnswersAndCloses()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { Steps = [Pop3TestPolicy.Challenge([])] };
        var limits = ExchangeLimits.Default with { MaxLineBytes = 64 };

        var connection = await ServeAsync(AccountStore(clock), "AUTH LOGIN\r\n" + new string('A', 100) + "\r\n", clock, TestContext.CancellationToken, policy, limits);

        Assert.AreEqual("+ \r\n-ERR Command line too long, closing\r\n", RepliesAfterGreeting(connection));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    [DataRow(SaslContinuationOutcome.LineTooLong, CrlfLineReadOutcome.LineTooLong)]
    [DataRow(SaslContinuationOutcome.HeadTimedOut, CrlfLineReadOutcome.HeadTimedOut)]
    [DataRow(SaslContinuationOutcome.Closed, CrlfLineReadOutcome.Closed)]
    public void AsLineReadOutcome_EachOutcome_EndsTheSessionAsACommandLineWould(SaslContinuationOutcome outcome, CrlfLineReadOutcome expected)
    {
        Assert.AreEqual(expected, Pop3Session.AsLineReadOutcome(outcome));
    }

    [TestMethod]
    public async Task ServeAsync_AuthAcceptedUnchecked_OpensTheAnonymousMaildrop()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { Steps = [Pop3TestPolicy.Ended(MailLoginOutcome.AcceptedUnchecked)] };

        var connection = await ServeAsync(AnonymousStore(clock, Message), "AUTH PLAIN =\r\nSTAT\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("+OK Authentication successful\r\n+OK 1 133\r\n", RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_AuthAcceptedWhileTheMaildropIsLocked_AnswersInUse()
    {
        var clock = new ManualTimeProvider();
        var store = AccountStore(clock);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(store.ViewFor("u"), out var held));
        var policy = new Pop3TestPolicy { Steps = [Pop3TestPolicy.Ended(MailLoginOutcome.Accepted)] };

        using (held)
        {
            var connection = await ServeAsync(store, "AUTH PLAIN AHUAcA==\r\nSTAT\r\n", clock, TestContext.CancellationToken, policy);

            Assert.AreEqual("-ERR [IN-USE] Maildrop is locked by another session\r\n-ERR [AUTH] Authentication required\r\n", RepliesAfterGreeting(connection));
        }
    }

    [TestMethod]
    public async Task ServeAsync_BareAuth_ListsTheOfferedMechanisms()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { SaslMechanisms = ["CRAM-MD5", "PLAIN"] };

        var connection = await ServeAsync(AccountStore(clock), "AUTH\r\nCAPA\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual(
            "+OK SASL mechanisms follow\r\nCRAM-MD5\r\nPLAIN\r\n.\r\n"
            + "+OK Capability list follows\r\nTOP\r\nUIDL\r\nRESP-CODES\r\nAUTH-RESP-CODE\r\nPIPELINING\r\nUSER\r\nSASL CRAM-MD5 PLAIN\r\n.\r\n",
            RepliesAfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_CapaAfterAuth_LeavesOutTheLoginCapabilities()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { SaslMechanisms = ["PLAIN"], Steps = [Pop3TestPolicy.Ended(MailLoginOutcome.Accepted)] };

        var connection = await ServeAsync(AccountStore(clock), "AUTH PLAIN =\r\nCAPA\r\n", clock, TestContext.CancellationToken, policy, isStlsAvailable: true);

        Assert.AreEqual(
            "+OK Authentication successful\r\n+OK Capability list follows\r\nTOP\r\nUIDL\r\nRESP-CODES\r\nAUTH-RESP-CODE\r\nPIPELINING\r\n.\r\n",
            RepliesAfterGreeting(connection));
    }
}
