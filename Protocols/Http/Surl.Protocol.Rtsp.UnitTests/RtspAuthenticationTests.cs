using System.Security.Cryptography;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Rtsp.RtspServerHarness;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// The RTSP server asks the authentication contract about every request and acts on its verdict
/// alone (ADR-0074 decision 7; ADR-0032, sections 4 and 6). The responses were fed to pinned
/// upstream curl before they were pinned here (Fixtures/README.md, BL-314).
/// </summary>
[TestClass]
public sealed class RtspAuthenticationTests
{
    private const string DigestMd5 = "Digest realm=\"surl\", qop=\"auth\", algorithm=MD5, nonce=\"MDAwMDAwMDAwMDAwMDAwMA\"";
    private const string DigestSha256 = "Digest realm=\"surl\", qop=\"auth\", algorithm=SHA-256, nonce=\"MDAwMDAwMDAwMDAwMDAwMA\"";
    private const string BasicTesterSecret = "Basic dGVzdGVyOnNlY3JldA==";
    private const string NextOptions = "OPTIONS * RTSP/1.0\r\nCSeq: 9\r\n\r\n";

    // What upstream curl sent, or a piece of it, that must never be repeated to the client or the log.
    private static readonly string[] Secrets = ["dGVzdGVyOnNlY3JldA==", "secret", "68ab5351aab91767d686a89ee0f0203a"];

    private static readonly CheckedLogin DigestTesterAccepted = new("Digest", "tester", true);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RecordedRequestWithNoLogin_Is401WithTheSessionsChallengesInOrderAndKeepsTheConnection()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge());

        var (connection, log) = await ServeAsync([RecordedRequest("no-login-401")], TestContext.CancellationToken, authenticationPolicy: policy);

        CollectionAssert.AreEqual(RecordedResponse("no-login-401"), connection.WrittenBytes);
        Assert.IsFalse(connection.WritesCompleted);
        CollectionAssert.AreEqual(
            new[] { "RTSP OPTIONS refused: 401 Unauthorized: a login is needed", "The client closed the connection: ConnectionClosed." },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task RecordedDigestLogin_ChallengesTheFirstRequestAndServesTheAnswerOnTheSameConnection()
    {
        var policy = new UnitTestAuthenticationPolicy(request => Authorization(request) is { } authorization && authorization.StartsWith("Digest ", StringComparison.Ordinal)
            ? new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "tester", DigestTesterAccepted)
            : Challenge());

        var (connection, log) = await ServeAsync([RecordedRequest("digest-login")], TestContext.CancellationToken, authenticationPolicy: policy);

        CollectionAssert.AreEqual(RecordedResponse("digest-login"), connection.WrittenBytes);
        Assert.HasCount(1, policy.StartedTlsSessions);
        Assert.HasCount(2, policy.JudgedRequests);
        Assert.AreEqual("*", policy.JudgedRequests[1].Target);
        CollectionAssert.AreEqual(
            new[] { "RTSP OPTIONS refused: 401 Unauthorized: a login is needed", "Login accepted: Digest tester", "The client closed the connection: ConnectionClosed." },
            log.Notes.ToArray());
        AssertNoSecretIn(connection, log);
    }

    [TestMethod]
    public async Task RecordedDigestLogin_WithAWrongAnswer_IsChallengedAgainWithTheRefusalNoted()
    {
        var policy = new UnitTestAuthenticationPolicy(request => Authorization(request) is null
            ? Challenge()
            : Challenge(new CheckedLogin("Digest", "tester", false)));

        var (connection, log) = await ServeAsync([RecordedRequest("digest-login")], TestContext.CancellationToken, authenticationPolicy: policy);

        Assert.AreEqual(ChallengeResponse("1") + ChallengeResponse("2"), Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        CollectionAssert.AreEqual(
            new[] { "RTSP OPTIONS refused: 401 Unauthorized: a login is needed", "Login refused: Digest tester", "RTSP OPTIONS refused: 401 Unauthorized: a login is needed", "The client closed the connection: ConnectionClosed." },
            log.Notes.ToArray());
        AssertNoSecretIn(connection, log);
    }

    [TestMethod]
    public async Task RecordedBasicLogin_OverRtspWithoutAllowPlaintextAuth_Is403UncheckedAndKeepsTheConnection()
    {
        var policy = new UnitTestAuthenticationPolicy(PlainTextRule(allowsPlaintextAuth: false));

        var (connection, log) = await ServeAsync([RecordedRequest("basic-forbidden-403"), Ascii(NextOptions)], TestContext.CancellationToken, authenticationPolicy: policy);

        Assert.AreEqual(Latin1(RecordedResponse("basic-forbidden-403")) + ResponseHead("403 Forbidden", "9"), Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        CollectionAssert.AreEqual(new TlsSession?[] { null }, policy.StartedTlsSessions.ToArray(), "rtsp:// is plain text: the session is started with no TLS.");
        Assert.AreEqual("RTSP OPTIONS refused: 403 Forbidden: the authentication policy forbade the request", log.Notes[0]);
        Assert.IsFalse(log.Notes.Any(note => note.StartsWith("Login ", StringComparison.Ordinal)), "A clear password over plain text is refused unchecked.");
        AssertNoSecretIn(connection, log);
    }

    [TestMethod]
    public async Task RecordedBasicLogin_WithAllowPlaintextAuth_IsCheckedAndServed()
    {
        var policy = new UnitTestAuthenticationPolicy(PlainTextRule(allowsPlaintextAuth: true));

        var (connection, log) = await ServeAsync([RecordedRequest("basic-login")], TestContext.CancellationToken, authenticationPolicy: policy);

        CollectionAssert.AreEqual(RecordedResponse("basic-login"), connection.WrittenBytes);
        CollectionAssert.AreEqual(
            new[] { "Login accepted: Basic tester", "The client closed the connection: ConnectionClosed." },
            log.Notes.ToArray());
        AssertNoSecretIn(connection, log);
    }

    [TestMethod]
    public async Task ProceedWithChallengeValues_WritesThemOnTheAnswer_AndNotOnTheNextRequestsAnswer()
    {
        var judged = 0;
        var policy = new UnitTestAuthenticationPolicy(_ => judged++ == 0
            ? new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, ["Negotiate oYG3"], "tester")
            : new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "tester"));

        var (connection, _) = await ServeAsync([Ascii("OPTIONS * RTSP/1.0\r\nCSeq: 1\r\n\r\n" + "OPTIONS * RTSP/1.0\r\nCSeq: 2\r\nCSeq: 3\r\n\r\n")], TestContext.CancellationToken, authenticationPolicy: policy);

        Assert.AreEqual(
            ResponseHead("200 OK", "1", "WWW-Authenticate: Negotiate oYG3", "Public: OPTIONS, DESCRIBE, ANNOUNCE, SETUP, PLAY, PAUSE, TEARDOWN, GET_PARAMETER, SET_PARAMETER, RECORD")
                + ResponseHead("400 Bad Request", null),
            Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task LoginIsJudgedBeforeTheMethod_SoAnUnknownMethodIsChallengedNot501()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge());

        var (connection, _) = await ServeAsync([Ascii("GET / RTSP/1.0\r\nCSeq: 4\r\n\r\n")], TestContext.CancellationToken, authenticationPolicy: policy);

        Assert.AreEqual(ChallengeResponse("4"), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task LoginIsJudgedAfterTheCSeqAndTheBodyFraming()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge());

        var (connection, _) = await ServeAsync(
            [Ascii("OPTIONS * RTSP/1.0\r\n\r\n" + "OPTIONS * RTSP/1.0\r\nCSeq: 2\r\nTransfer-Encoding: chunked\r\n\r\n")],
            TestContext.CancellationToken,
            peerHalfCloses: false,
            authenticationPolicy: policy);

        Assert.AreEqual(ResponseHead("400 Bad Request", null) + ResponseHead("400 Bad Request", "2"), Latin1(connection.WrittenBytes));
        Assert.IsEmpty(policy.JudgedRequests);
    }

    [TestMethod]
    public async Task ChallengedRequestWithABody_ReadsTheBodySoTheNextRequestIsAnswered()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge());

        var (connection, _) = await ServeAsync([Ascii("ANNOUNCE rtsp://h/a RTSP/1.0\r\nCSeq: 2\r\nContent-Length: 4\r\n\r\nv=0\n" + NextOptions)], TestContext.CancellationToken, authenticationPolicy: policy);

        Assert.AreEqual(ChallengeResponse("2") + ChallengeResponse("9"), Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    [DataRow("OPTIONS * RTSP/1.0\r\nCSeq: 1\r\n\r\n", false)]
    [DataRow("DESCRIBE rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 1\r\n\r\n", false)]
    [DataRow("PLAY * RTSP/1.0\r\nCSeq: 1\r\nSession: 1\r\n\r\n", false)]
    [DataRow("ANNOUNCE rtsp://h/a RTSP/1.0\r\nCSeq: 1\r\n\r\n", true)]
    [DataRow("RECORD * RTSP/1.0\r\nCSeq: 1\r\nSession: 1\r\n\r\n", true)]
    [DataRow("SETUP rtsp://h/a RTSP/1.0\r\nCSeq: 1\r\n\r\n", false)]
    [DataRow("SETUP rtsp://h/a RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;interleaved=0-1\r\n\r\n", false)]
    [DataRow("SETUP rtsp://h/a RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;interleaved=0-1;mode=play\r\n\r\n", false)]
    [DataRow("SETUP rtsp://h/a RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;interleaved=0-1;mode=record\r\n\r\n", true)]
    [DataRow("SETUP rtsp://h/a RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;interleaved=0-1; MODE=\"RECORD\"\r\n\r\n", true)]
    [DataRow("SETUP rtsp://h/a RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP;unicast\r\nTransport: RTP/AVP/TCP;mode=record\r\n\r\n", true)]
    public async Task EveryRequestIsJudged_WithAnnounceRecordAndARecordingSetupAsWrites(string request, bool isWrite)
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge());

        await ServeAsync([Ascii(request)], TestContext.CancellationToken, authenticationPolicy: policy);

        Assert.HasCount(1, policy.JudgedRequests);
        Assert.AreEqual(isWrite, policy.JudgedRequests[0].IsWrite);
    }

    [TestMethod]
    public async Task JudgedRequest_CarriesTheMethodTheTargetAndEveryFieldInOrder()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge());

        await ServeAsync([RecordedRequest("basic-login")], TestContext.CancellationToken, authenticationPolicy: policy);

        var judged = policy.JudgedRequests.Single();
        Assert.AreEqual("OPTIONS", judged.Method);
        Assert.AreEqual("*", judged.Target);
        CollectionAssert.AreEqual(
            new[] { new KeyValuePair<string, string>("CSeq", "1"), new("User-Agent", "curl/8.21.0"), new("Authorization", BasicTesterSecret) },
            judged.Fields.ToArray());
    }

    [TestMethod]
    [DataRow("", true)]
    [DataRow("Content-Length: 4\r\n\r\nv=0\n", true)]
    [DataRow("Content-Length: 4\r\n\r\nv=1\n", false)]
    public async Task LoginBoundToTheBody_IsJudgedWithTheBodysSha256(string bodyFraming, bool bodyMatches)
    {
        var expectedHash = SHA256.HashData(Ascii(bodyFraming.Length == 0 ? string.Empty : "v=0\n"));
        var bodyCheck = new UnitTestBodyCheck(expectedHash);
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "tester", null, bodyCheck));
        var request = bodyFraming.Length == 0 ? "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\n\r\n" : $"OPTIONS * RTSP/1.0\r\nCSeq: 1\r\n{bodyFraming}";

        var (connection, log) = await ServeAsync([Ascii(request)], TestContext.CancellationToken, authenticationPolicy: policy);

        Assert.AreEqual(bodyMatches ? "RTSP/1.0 200 OK" : "RTSP/1.0 403 Forbidden", Latin1(connection.WrittenBytes).Split("\r\n")[0]);
        Assert.AreEqual(bodyMatches ? "Login accepted: AWS4-HMAC-SHA256 tester" : "Login refused: AWS4-HMAC-SHA256 tester", log.Notes[0]);
    }

    [TestMethod]
    public async Task LoginBoundToABodyThatEndsEarly_Is400AndCloses()
    {
        var bodyCheck = new UnitTestBodyCheck([]);
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "tester", null, bodyCheck));

        var (connection, _) = await ServeAsync([Ascii("OPTIONS * RTSP/1.0\r\nCSeq: 2\r\nContent-Length: 10\r\n\r\n1234")], TestContext.CancellationToken, authenticationPolicy: policy);

        Assert.AreEqual(ResponseHead("400 Bad Request", "2"), Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual(0, bodyCheck.JudgedCount);
    }

    private static HttpAuthenticationVerdict Challenge(CheckedLogin? checkedLogin = null) =>
        new(HttpAuthenticationOutcome.Challenge, [DigestMd5, DigestSha256], null, checkedLogin);

    private static string ChallengeResponse(string cseq) =>
        ResponseHead("401 Unauthorized", cseq, $"WWW-Authenticate: {DigestMd5}", $"WWW-Authenticate: {DigestSha256}");

    // ADR-0032 section 4 step 2, as the real policy applies it: a clear password with no TLS is
    // refused unchecked unless --allow-plaintext-auth, and checked otherwise.
    private static Func<TlsSession?, HttpAuthenticationRequest, HttpAuthenticationVerdict> PlainTextRule(bool allowsPlaintextAuth) => (tlsSession, request) =>
        (tlsSession, allowsPlaintextAuth, Authorization(request)) switch
        {
            (null, false, _) => new(HttpAuthenticationOutcome.Forbidden, [], null),
            (_, _, BasicTesterSecret) => new(HttpAuthenticationOutcome.Proceed, [], "tester", new CheckedLogin("Basic", "tester", true)),
            _ => Challenge(new CheckedLogin("Basic", "tester", false)),
        };

    private static string? Authorization(HttpAuthenticationRequest request) =>
        request.Fields.Where(field => field.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)).Select(field => field.Value).SingleOrDefault();

    private static void AssertNoSecretIn(InMemoryConnection connection, RecordingExchangeLog log)
    {
        var written = Latin1(connection.WrittenBytes);
        foreach (var secret in Secrets)
        {
            Assert.DoesNotContain(secret, written);
            Assert.IsFalse(log.Notes.Any(note => note.Contains(secret, StringComparison.Ordinal)), $"The log repeated {secret}.");
        }
    }

    private sealed class UnitTestBodyCheck(byte[] expectedSha256) : IHttpRequestBodyCheck
    {
        public int JudgedCount { get; private set; }

        public ValueTask<HttpAuthenticationVerdict> JudgeBodyAsync(ReadOnlyMemory<byte> bodySha256, CancellationToken cancellationToken)
        {
            JudgedCount++;
            var matches = bodySha256.Span.SequenceEqual(expectedSha256);

            return ValueTask.FromResult(new HttpAuthenticationVerdict(
                matches ? HttpAuthenticationOutcome.Proceed : HttpAuthenticationOutcome.Forbidden,
                [],
                matches ? "tester" : null,
                new CheckedLogin("AWS4-HMAC-SHA256", "tester", matches)));
        }
    }
}
