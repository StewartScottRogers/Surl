using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Xmpp.UnitTests.XmppLoginScript;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// SASL on an XMPP stream (BL-538; ADR-0114 decision 3), driven through the server, every stream
/// and answer written by hand from RFC 6120 sections 6.4 and 6.5, with the SCRAM examples of RFC
/// 5802 section 5 and RFC 7677 section 3 for the account <c>user</c>/<c>pencil</c>.
/// </summary>
[TestClass]
public sealed class XmppSaslLoginTests
{
    private static readonly string TlsFeatures = MechanismFeatures("SCRAM-SHA-256", "SCRAM-SHA-1", "PLAIN");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("SCRAM-SHA-256",
        "n,,n=user,r=rOprNGfwEbeRWgbNEkqO",
        "r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096",
        "c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p=dHzbZapWIk4jUhN+Ute9ytag9zjfMHgsqmmiz7AndVQ=",
        "v=6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl95G4=")]
    [DataRow("SCRAM-SHA-1",
        "n,,n=user,r=fyko+d2lbbFgONRv9qkxdawL",
        "r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096",
        "c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=",
        "v=rmF9pqV8S7suAoZWja4dJRkFsKQ=")]
    public async Task StartAsync_ScramWithTheRfcExample_ChallengesThenSucceedsWithTheServerFinalMessageAndRestartsTheStream(
        string mechanism, string clientFirst, string serverFirst, string clientFinal, string serverFinal)
    {
        var script = mechanism == "SCRAM-SHA-1" ? UnitTestScramExchangeScript.Sha1 : UnitTestScramExchangeScript.Sha256;
        var policy = new UnitTestXmppSaslPolicy([], ["SCRAM-SHA-256", "SCRAM-SHA-1", "PLAIN"]) { Answer = script.Answer };

        var (answers, log) = await ServeAsync(
            Options(policy), TestContext.CancellationToken,
            ClientHeader, Auth(mechanism, Base64(clientFirst)), Response(Base64(clientFinal)), ClientHeader, Close);

        Assert.AreEqual(
            ResponseHeader(1) + TlsFeatures + Challenge(Base64(serverFirst)) +
            $"<success xmlns='urn:ietf:params:xml:ns:xmpp-sasl'>{Base64(serverFinal)}</success>" +
            ResponseHeader(2) + BindFeatures + Close,
            answers);
        var start = policy.Starts.Single();
        Assert.AreEqual(mechanism, start.Mechanism);
        Assert.AreEqual(clientFirst, Encoding.UTF8.GetString(start.InitialResponse!.Value.Span));
        Assert.AreEqual(InMemoryConnection.DefaultUpgradeTlsSession, start.TlsSession);
        Assert.IsTrue(start.CanCarrySecurityLayer);
        Assert.AreEqual("xmpps", start.Scheme);
        CollectionAssert.Contains(log.Notes.ToList(), $"Login accepted: {mechanism} user");
        CollectionAssert.Contains(log.Notes.ToList(), "Logged in as user@im.example.com.");
    }

    [TestMethod]
    public async Task ContinueAsync_ScramWithABadProof_IsNotAuthorizedAndTheStreamKept()
    {
        var policy = new UnitTestXmppSaslPolicy([], ["SCRAM-SHA-256"]) { Answer = UnitTestScramExchangeScript.Sha256.Answer };
        const string badFinal = "c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

        var (answers, log) = await ServeAsync(
            Options(policy), TestContext.CancellationToken,
            ClientHeader, Auth("SCRAM-SHA-256", Base64("n,,n=user,r=rOprNGfwEbeRWgbNEkqO")), Response(Base64(badFinal)), Close);

        Assert.EndsWith(Failure("not-authorized") + Close, answers);
        CollectionAssert.Contains(log.Notes.ToList(), "Login refused: SCRAM-SHA-256 user");
        CollectionAssert.Contains(log.Notes.ToList(), "SCRAM: invalid-proof");
        CollectionAssert.Contains(log.Notes.ToList(), "The SASL login was refused; answered not-authorized.");
    }

    [TestMethod]
    public async Task StartAsync_PlainOverTls_SucceedsEmptyAndOffersBindAfterTheRestart()
    {
        var (answers, _) = await ServeAsync(
            Options(PlainPolicy()), TestContext.CancellationToken, ClientHeader, Auth("PLAIN", PlainUserPencil), ClientHeader, Close);

        Assert.AreEqual(ResponseHeader(1) + MechanismFeatures("PLAIN") + Success + ResponseHeader(2) + BindFeatures + Close, answers);
    }

    [TestMethod]
    public async Task StartAsync_PlainWithAWrongPassword_IsNotAuthorized()
    {
        var (answers, _) = await ServeAsync(
            Options(PlainPolicy()), TestContext.CancellationToken, ClientHeader, Auth("PLAIN", Base64("\0user\0wrong")), Close);

        Assert.AreEqual(ResponseHeader(1) + MechanismFeatures("PLAIN") + Failure("not-authorized") + Close, answers);
    }

    [TestMethod]
    [DataRow("DIGEST-MD5")]
    [DataRow("plain")]
    [DataRow("ANONYMOUS")]
    public async Task StartAsync_MechanismNotOffered_IsInvalidMechanismAndNoExchangeStarts(string mechanism)
    {
        var policy = Policy();

        var (answers, _) = await ServeAsync(Options(policy), TestContext.CancellationToken, ClientHeader, Auth(mechanism, "="), Close);

        Assert.AreEqual(ResponseHeader(1) + TlsFeatures + Failure("invalid-mechanism") + Close, answers);
        Assert.IsEmpty(policy.Starts);
    }

    [TestMethod]
    public async Task StartAsync_NoMechanism_IsMalformedRequest()
    {
        var (answers, _) = await ServeAsync(
            Options(Policy()), TestContext.CancellationToken, ClientHeader, "<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl'/>", Close);

        Assert.AreEqual(ResponseHeader(1) + TlsFeatures + Failure("malformed-request") + Close, answers);
    }

    [TestMethod]
    public async Task StartAsync_InitialResponseNotBase64_IsIncorrectEncoding()
    {
        var (answers, _) = await ServeAsync(Options(Policy()), TestContext.CancellationToken, ClientHeader, Auth("PLAIN", "not*base64"), Close);

        Assert.AreEqual(ResponseHeader(1) + TlsFeatures + Failure("incorrect-encoding") + Close, answers);
    }

    [TestMethod]
    public async Task StartAsync_EqualsSign_IsAnEmptyInitialResponse()
    {
        var policy = Policy();

        await ServeAsync(Options(policy), TestContext.CancellationToken, ClientHeader, Auth("PLAIN", "="), Close);

        Assert.IsTrue(policy.Starts.Single().InitialResponse!.Value.IsEmpty);
    }

    [TestMethod]
    public async Task StartAsync_NoText_IsNoInitialResponseAndAnEmptyChallengeIsAnEmptyElement()
    {
        var policy = new UnitTestXmppSaslPolicy([], ["PLAIN"])
        {
            Answer = message => message is null
                ? new SaslLoginStep(SaslLoginOutcome.Challenge, ReadOnlyMemory<byte>.Empty, null, null)
                : UnitTestXmppSaslPolicy.AcceptedUnchecked,
        };

        var (answers, _) = await ServeAsync(
            Options(policy), TestContext.CancellationToken,
            ClientHeader, Auth("PLAIN", string.Empty), "<response xmlns='urn:ietf:params:xml:ns:xmpp-sasl'/>", ClientHeader, Close);

        Assert.IsNull(policy.Starts.Single().InitialResponse);
        Assert.AreEqual(
            ResponseHeader(1) + MechanismFeatures("PLAIN") + "<challenge xmlns='urn:ietf:params:xml:ns:xmpp-sasl'/>" + Success +
            ResponseHeader(3) + BindFeatures + Close,
            answers);
        Assert.IsEmpty(policy.Messages[1]!);
    }

    [TestMethod]
    public async Task ContinueAsync_ResponseNotBase64_IsIncorrectEncoding()
    {
        var policy = new UnitTestXmppSaslPolicy([], ["PLAIN"]) { Answer = _ => new SaslLoginStep(SaslLoginOutcome.Challenge, "x"u8.ToArray(), null, null) };

        var (answers, _) = await ServeAsync(
            Options(policy), TestContext.CancellationToken, ClientHeader, Auth("PLAIN", string.Empty), Response("%%"), Close);

        Assert.AreEqual(ResponseHeader(1) + MechanismFeatures("PLAIN") + Challenge("eA==") + Failure("incorrect-encoding") + Close, answers);
    }

    [TestMethod]
    public async Task AbortAsync_DuringAnExchange_IsAbortedAndNotCountedAsAFailure()
    {
        var policy = new UnitTestXmppSaslPolicy([], ["SCRAM-SHA-256"]) { Answer = UnitTestScramExchangeScript.Sha256.Answer };
        var auth = Auth("SCRAM-SHA-256", Base64("n,,n=user,r=rOprNGfwEbeRWgbNEkqO"));
        const string abort = "<abort xmlns='urn:ietf:params:xml:ns:xmpp-sasl'/>";

        var (answers, log) = await ServeAsync(
            Options(policy), TestContext.CancellationToken, ClientHeader, auth, abort, auth, abort, auth, abort, auth, abort, Close);

        Assert.AreEqual(4, CountOf(answers, Failure("aborted")));
        Assert.EndsWith(Failure("aborted") + Close, answers);
        CollectionAssert.Contains(log.Notes.ToList(), "The client aborted the SASL exchange; answered aborted.");
    }

    [TestMethod]
    [DataRow("<abort xmlns='urn:ietf:params:xml:ns:xmpp-sasl'/>")]
    [DataRow("<response xmlns='urn:ietf:params:xml:ns:xmpp-sasl'>=</response>")]
    [DataRow("<success xmlns='urn:ietf:params:xml:ns:xmpp-sasl'/>")]
    public async Task ServeAsync_SaslElementWithNoExchangeUnderWay_IsPolicyViolation(string element)
    {
        var (answers, _) = await ServeAsync(Options(Policy()), TestContext.CancellationToken, ClientHeader, element);

        Assert.AreEqual(ResponseHeader(1) + TlsFeatures + ErrorAndClose("policy-violation"), answers);
    }

    [TestMethod]
    public async Task StartAsync_ThirdFailure_IsAnsweredThenEndsTheStreamWithPolicyViolation()
    {
        var wrong = Auth("PLAIN", Base64("\0user\0wrong"));

        var (answers, log) = await ServeAsync(Options(PlainPolicy()), TestContext.CancellationToken, ClientHeader, wrong, wrong, wrong, wrong);

        Assert.AreEqual(
            ResponseHeader(1) + MechanismFeatures("PLAIN") + Failure("not-authorized") + Failure("not-authorized") + Failure("not-authorized") +
            ErrorAndClose("policy-violation"),
            answers);
        CollectionAssert.Contains(log.Notes.ToList(), "The stream failed SASL 3 times; answered the stream error policy-violation and closed.");
    }

    [TestMethod]
    public async Task StartAsync_AfterTwoFailures_ASuccessStillLogsIn()
    {
        var wrong = Auth("PLAIN", Base64("\0user\0wrong"));

        var (answers, _) = await ServeAsync(
            Options(PlainPolicy()), TestContext.CancellationToken, ClientHeader, wrong, wrong, Auth("PLAIN", PlainUserPencil), ClientHeader, Close);

        Assert.EndsWith(Failure("not-authorized") + Success + ResponseHeader(2) + BindFeatures + Close, answers);
    }

    [TestMethod]
    [DataRow(SaslLoginOutcome.RefusedPlaintext, "encryption-required")]
    [DataRow(SaslLoginOutcome.RefusedMechanism, "invalid-mechanism")]
    [DataRow(SaslLoginOutcome.RefusedCredentials, "not-authorized")]
    public async Task StartAsync_PolicyRefusal_IsItsFailureCondition(SaslLoginOutcome outcome, string condition)
    {
        var policy = new UnitTestXmppSaslPolicy([], ["PLAIN"]) { Answer = _ => new SaslLoginStep(outcome, ReadOnlyMemory<byte>.Empty, null, null) };

        var (answers, _) = await ServeAsync(Options(policy), TestContext.CancellationToken, ClientHeader, Auth("PLAIN", "="), Close);

        Assert.AreEqual(ResponseHeader(1) + MechanismFeatures("PLAIN") + Failure(condition) + Close, answers);
    }

    [TestMethod]
    [DataRow("al ice")]
    [DataRow("al/ice")]
    [DataRow("")]
    [DataRow("user@other.example")]
    public async Task StartAsync_AccountUnusableAsAJidOnTheStreamDomain_IsNotAuthorized(string accountName)
    {
        var (answers, log) = await ServeAsync(
            Options(PlainPolicy(accountName)), TestContext.CancellationToken, ClientHeader, Auth("PLAIN", PlainUserPencil), Close);

        Assert.AreEqual(ResponseHeader(1) + MechanismFeatures("PLAIN") + Failure("not-authorized") + Close, answers);
        CollectionAssert.Contains(
            log.Notes.ToList(), "The account's name cannot be a JID on this stream's domain; answered not-authorized.");
    }

    [TestMethod]
    public async Task StartAsync_AuthBeforeTlsWhileRequired_CountsAsAFailedAttempt()
    {
        var auth = Auth("PLAIN", PlainUserPencil);

        var (answers, _) = await ServeAsync(Options(PlainPolicy()), TestContext.CancellationToken, isTls: false, ClientHeader, auth, auth, auth);

        Assert.EndsWith(Failure("encryption-required") + ErrorAndClose("policy-violation"), answers);
        Assert.AreEqual(3, CountOf(answers, Failure("encryption-required")));
    }

    [TestMethod]
    [DataRow("<message to='romeo@example.net'><body>hi</body></message>", "not-authorized")]
    [DataRow("<thing/>", "unsupported-stanza-type")]
    [DataRow("<bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/>", "policy-violation")]
    public async Task ServeAsync_OtherElementBeforeLogin_EndsTheStream(string element, string condition)
    {
        var (answers, _) = await ServeAsync(Options(Policy()), TestContext.CancellationToken, ClientHeader, element);

        Assert.AreEqual(ResponseHeader(1) + TlsFeatures + ErrorAndClose(condition), answers);
    }

    private static int CountOf(string text, string part) => (text.Length - text.Replace(part, string.Empty, StringComparison.Ordinal).Length) / part.Length;
}
