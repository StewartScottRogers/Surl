using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// The HTTP server asks the authentication contract about every request and acts on its
/// verdict alone (ADR-0032, sections 4 and 6). The responses were fed to pinned upstream curl
/// before they were pinned here (Fixtures/README.md, BL-114).
/// </summary>
[TestClass]
public sealed class AuthenticationTests
{
    private const string DigestMd5 = "Digest realm=\"surl\", qop=\"auth\", algorithm=MD5, nonce=\"abc\"";
    private const string DigestSha256 = "Digest realm=\"surl\", qop=\"auth\", algorithm=SHA-256, nonce=\"abc\"";
    private const string DigestSha512256 = "Digest realm=\"surl\", qop=\"auth\", algorithm=SHA-512-256, nonce=\"abc\"";
    private const string BasicChallenge = "Basic realm=\"surl\", charset=\"UTF-8\"";
    private const string BearerChallenge = "Bearer realm=\"surl\"";

    // What upstream curl sent, or a piece of it, that must never be repeated to the client or the log.
    private static readonly string[] Secrets = ["dGVzdGVyOnNlY3JldA==", "secret", "Bearer tok", "b4a02b13510764079b4765e1895a7d09", "round-1", "round-3"];

    private static readonly TlsSession Tls = new(SslProtocols.Tls13, TlsCipherSuite.TLS_AES_128_GCM_SHA256, "http/1.1", "localhost", null);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullAuthenticationPolicy_Throws()
    {
        var contentStore = new ContentStore(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);

        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpProtocolServer(contentStore, null!));
    }

    [TestMethod]
    [DataRow("basic-401", new[] { DigestMd5, DigestSha256, DigestSha512256, BasicChallenge })]
    [DataRow("bearer-401", new[] { BearerChallenge })]
    [DataRow("anonymous-401-fail", new[] { DigestMd5, DigestSha256, DigestSha512256 })]
    public async Task ServeAsync_RecordedRequestChallenged_Sends401WithTheContractsChallengesAndKeepsTheConnection(string caseName, string[] challenges)
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge(challenges));

        var (connection, log) = await ServeAsync(policy, RecordedFixture.ReadRequestBytes(caseName));

        CollectionAssert.AreEqual(RecordedResponse(caseName), connection.WrittenBytes);
        Assert.IsFalse(connection.WritesCompleted);
        Assert.AreEqual("GET /file.txt: 401, a login is needed", log.Notes[0]);
        AssertNoSecretIn(connection, log);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedDigestLogin_ChallengesTheFirstRequestAndServesTheSecond()
    {
        var policy = new UnitTestAuthenticationPolicy(request => Authorization(request) is null
            ? Challenge([DigestMd5, DigestSha256, DigestSha512256])
            : new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "tester"));

        var (connection, log) = await ServeAsync(policy, RecordedFixture.ReadRequestBytes("digest-401-then-200"));

        CollectionAssert.AreEqual(RecordedResponse("digest-401-then-200"), connection.WrittenBytes);
        Assert.HasCount(2, policy.JudgedRequests);
        Assert.HasCount(1, policy.StartedTlsSessions);
        AssertNoSecretIn(connection, log);
    }

    [TestMethod]
    public async Task ServeAsync_DigestLoginWhenNoAccountExists_Sends401WithTheChallenges()
    {
        // ADR-0032, section 4, step 3: every login with no account configured is refused
        // with the challenges, the same answer as a wrong password.
        var secondHead = SecondHead(RecordedFixture.ReadRequestBytes("digest-401-then-200"));
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge([DigestMd5, DigestSha256, DigestSha512256]));

        var (connection, log) = await ServeAsync(policy, secondHead);

        CollectionAssert.AreEqual(RecordedResponse("anonymous-401-fail"), connection.WrittenBytes);
        AssertNoSecretIn(connection, log);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedBasicOverPlaintextForbidden_Sends403AndCloses()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Forbidden, [], null));

        var (connection, log) = await ServeAsync(policy, RecordedFixture.ReadRequestBytes("basic-plaintext-403"));

        CollectionAssert.AreEqual(RecordedResponse("basic-plaintext-403"), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("GET /file.txt: the authentication policy forbade the request; answered 403 and closed.", log.Notes[0]);
        Assert.IsNull(policy.StartedTlsSessions[0]);
        AssertNoSecretIn(connection, log);
    }

    [TestMethod]
    [DataRow("get-file", "GET", "get-file")]
    [DataRow("head-file", "HEAD", "head-file")]
    public async Task ServeAsync_AnonymousReadWithNoAccount_IsServed(string caseName, string method, string responseCase)
    {
        // ADR-0032, section 4: with no account configured, a GET or HEAD needs no login.
        var policy = new UnitTestAuthenticationPolicy(request => request.IsWrite || Authorization(request) is not null
            ? Challenge([DigestMd5])
            : new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));

        var (connection, _) = await ServeAsync(policy, RecordedFixture.ReadRequestBytes(caseName));

        CollectionAssert.AreEqual(RecordedResponse(responseCase), connection.WrittenBytes);
        Assert.AreEqual(method, policy.JudgedRequests[0].Method);
        Assert.AreEqual("/file.txt", policy.JudgedRequests[0].Target);
        Assert.IsFalse(policy.JudgedRequests[0].IsWrite);
    }

    [TestMethod]
    [DataRow("get-file")]
    [DataRow("head-file")]
    public async Task ServeAsync_AnonymousReadOnceAnAccountExists_IsChallenged(string caseName)
    {
        // ADR-0032, section 4: once any account is configured, every request needs a login.
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge([DigestMd5, DigestSha256, DigestSha512256]));

        var (connection, _) = await ServeAsync(policy, RecordedFixture.ReadRequestBytes(caseName));

        CollectionAssert.AreEqual(RecordedResponse("anonymous-401-fail"), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_RequestFields_AreShownToTheContractInOrder()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge([BearerChallenge]));

        await ServeAsync(policy, RecordedFixture.ReadRequestBytes("bearer-401"));

        var fields = policy.JudgedRequests[0].Fields;
        CollectionAssert.AreEqual(
            new[] { "Host", "Authorization", "User-Agent", "Accept" },
            fields.Select(field => field.Key).ToArray());
        Assert.AreEqual("Bearer tok", fields[1].Value);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ServeAsync_ConnectionEncryption_IsPassedToTheContract(bool encrypted)
    {
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));
        var connection = new InMemoryConnection([RecordedFixture.ReadRequestBytes("get-file")], initialTlsSession: encrypted ? Tls : null);

        await Server(policy).ServeAsync(connection, Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), TestContext.CancellationToken));

        Assert.HasCount(1, policy.StartedTlsSessions);
        Assert.AreSame(encrypted ? Tls : null, policy.StartedTlsSessions[0]);
    }

    [TestMethod]
    public async Task ServeAsync_ProceedWithValues_WritesThemOnTheResponse()
    {
        // RFC 4559, section 5: Negotiate's final token goes on the response that is served.
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, ["Negotiate final"], "tester"));

        var (connection, _) = await ServeAsync(policy, RecordedFixture.ReadRequestBytes("get-file"));

        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\n"
            + "Last-Modified: Tue, 01 Sep 2026 08:30:00 GMT\r\nContent-Type: application/octet-stream\r\nContent-Length: 17\r\n"
            + "WWW-Authenticate: Negotiate final\r\n\r\n" + FileBody,
            Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_UnauthenticatedWrite_Gets401NotTheMethodRefusalAndTheBodyIsNotRead()
    {
        // ADR-0032, section 4: judged before method dispatch, and a request that announced a
        // body it was not allowed to send is refused like any other: Connection: close.
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge([DigestMd5]));

        var (connection, log) = await ServeAsync(policy, RecordedFixture.ReadRequestBytes("post-refused-405"));

        Assert.AreEqual(
            "HTTP/1.1 401 Unauthorized\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\n"
            + "WWW-Authenticate: " + DigestMd5 + "\r\nConnection: close\r\n\r\n",
            Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsTrue(policy.JudgedRequests[0].IsWrite);
        Assert.AreEqual("POST /file.txt: a login is needed, and the body announced was not read; answered 401 and closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_UploadPastTheLimitWithoutALogin_Gets401Before413()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge([DigestMd5]));

        var (connection, _) = await ServeAsync(policy, RecordedFixture.ReadRequestBytes("upload-too-large-413"), ExchangeLimits.Default with { MaxUploadBytes = 1024 });

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 401 Unauthorized\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_InvalidContentLength_Gets400WithoutAskingTheContract()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge([DigestMd5]));

        var (connection, _) = await ServeAsync(policy, RecordedFixture.ReadRequestBytes("invalid-content-length-400"));

        CollectionAssert.AreEqual(RecordedResponse("invalid-content-length-400"), connection.WrittenBytes);
        Assert.IsEmpty(policy.JudgedRequests);
    }

    [TestMethod]
    public async Task ServeAsync_ChallengeThenMalformedHead_Sends400WithoutTheChallenge()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge([DigestMd5]));
        var request = Concat(RecordedFixture.ReadRequestBytes("anonymous-401-fail"), Ascii("GET /file.txt HTTP/1.1\r\nBad field\r\n\r\n"));

        var (connection, _) = await ServeAsync(policy, request);

        var written = Latin1(connection.WrittenBytes);
        var second = written[written.IndexOf("HTTP/1.1 400", StringComparison.Ordinal)..];
        Assert.DoesNotContain("WWW-Authenticate", second);
    }

    [TestMethod]
    public async Task ServeAsync_TwoRoundHandshakeOnOneConnection_KeepsItsStateAcrossRequests()
    {
        var request = Ascii(NtlmRequest("round-1") + NtlmRequest("round-3"));

        var (connection, log) = await ServeAsync(new UnitTestTwoRoundAuthenticationPolicy(), request);

        Assert.AreEqual(
            Unauthorized("NTLM round-2") + "HTTP/1.1 200 OK\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\n"
            + "Last-Modified: Tue, 01 Sep 2026 08:30:00 GMT\r\nContent-Type: application/octet-stream\r\nContent-Length: 17\r\n\r\n" + FileBody,
            Latin1(connection.WrittenBytes));
        AssertNoSecretIn(connection, log);
    }

    [TestMethod]
    public async Task ServeAsync_TwoRoundHandshakeAcrossConnections_StartsOverOnTheNewConnection()
    {
        var policy = new UnitTestTwoRoundAuthenticationPolicy();

        var (first, _) = await ServeAsync(policy, Ascii(NtlmRequest("round-1")));
        var (second, _) = await ServeAsync(policy, Ascii(NtlmRequest("round-3")));

        Assert.AreEqual(Unauthorized("NTLM round-2"), Latin1(first.WrittenBytes));
        Assert.AreEqual(Unauthorized("NTLM"), Latin1(second.WrittenBytes));
    }

    [TestMethod]
    [DataRow("basic-401", "Basic", "tester", DisplayName = "Basic")]
    [DataRow("bearer-401", "Bearer", CheckedLogin.BearerTokenUser, DisplayName = "Bearer")]
    public async Task ServeAsync_CheckedLoginAccepted_NotesLoginAcceptedAndServes(string caseName, string method, string user)
    {
        // ADR-0032, section 8: the note names the method and the user as sent, never the secret.
        var policy = new UnitTestAuthenticationPolicy(_ =>
            new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "tester", new CheckedLogin(method, user, true)));

        var (connection, log) = await ServeAsync(policy, RecordedFixture.ReadRequestBytes(caseName));

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual($"Login accepted: {method} {user}", log.Notes[0]);
        AssertNoSecretIn(connection, log);
    }

    [TestMethod]
    [DataRow("basic-401", "Basic", "tester", DisplayName = "Basic")]
    [DataRow("bearer-401", "Bearer", CheckedLogin.BearerTokenUser, DisplayName = "Bearer")]
    public async Task ServeAsync_CheckedLoginRefused_NotesLoginRefusedThenThe401(string caseName, string method, string user)
    {
        var policy = new UnitTestAuthenticationPolicy(_ =>
            Challenge([BasicChallenge]) with { CheckedLogin = new CheckedLogin(method, user, false) });

        var (connection, log) = await ServeAsync(policy, RecordedFixture.ReadRequestBytes(caseName));

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 401 Unauthorized\r\n");
        CollectionAssert.AreEqual(
            new[] { $"Login refused: {method} {user}", "GET /file.txt: 401, a login is needed" }, log.Notes.Take(2).ToArray());
        AssertNoSecretIn(connection, log);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedDigestLogin_NotesOnlyTheCheckedLogin()
    {
        // The first request carries no credentials, so only the second is noted.
        var policy = new UnitTestAuthenticationPolicy(request => Authorization(request) is null
            ? Challenge([DigestMd5, DigestSha256, DigestSha512256])
            : new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "tester", new CheckedLogin("Digest", "tester", true)));

        var (connection, log) = await ServeAsync(policy, RecordedFixture.ReadRequestBytes("digest-401-then-200"));

        Assert.AreEqual(1, log.Notes.Count(note => note.StartsWith("Login ", StringComparison.Ordinal)));
        CollectionAssert.Contains(log.Notes.ToArray(), "Login accepted: Digest tester");
        AssertNoSecretIn(connection, log);
    }

    [TestMethod]
    public async Task ServeAsync_VerdictWithoutCheckedLogin_WritesNoLoginNote()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge([BearerChallenge]));

        var (_, log) = await ServeAsync(policy, RecordedFixture.ReadRequestBytes("bearer-401"));

        Assert.IsFalse(log.Notes.Any(note => note.StartsWith("Login ", StringComparison.Ordinal)));
    }

    // ADR-0045: a login bound to the body. The policy lets the head in with a body check; the
    // server reads and hashes the body, then answers as the body check's verdict says.
    private static readonly byte[] BodySha256 = SHA256.HashData("body"u8);

    private const string AwsAuthorization = "Authorization: AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20260929/us-east-1/ec2/aws4_request, SignedHeaders=host;x-amz-date, Signature=0\r\n";

    private static UnitTestRequestBodyCheck BodyCheckAccepting(byte[] sha256) => new(hash => hash.SequenceEqual(sha256)
        ? new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "AKIDEXAMPLE", new CheckedLogin("AWS4-HMAC-SHA256", "AKIDEXAMPLE", true))
        : Challenge([DigestMd5]) with { CheckedLogin = new CheckedLogin("AWS4-HMAC-SHA256", "AKIDEXAMPLE", false) });

    private static UnitTestAuthenticationPolicy BodyBindingPolicy(IHttpRequestBodyCheck bodyCheck) =>
        new(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null, BodyCheck: bodyCheck));

    private static byte[] Put(string body, string extraFields = "") =>
        Ascii($"PUT /upload HTTP/1.1\r\nHost: 127.0.0.1:18136\r\n{AwsAuthorization}{extraFields}Content-Length: {body.Length}\r\n\r\n{body}");

    [TestMethod]
    public async Task ServeAsync_BodyBoundLoginWithTheSignedBody_IsLetInAfterTheBodyIsRead()
    {
        // HTTP serves no PUT yet, so a write let in meets the method refusal, not the 401.
        var bodyCheck = BodyCheckAccepting(BodySha256);

        var (connection, log) = await ServeAsync(BodyBindingPolicy(bodyCheck), Put("body"));

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 405 Method Not Allowed\r\n");
        CollectionAssert.AreEqual(BodySha256, bodyCheck.JudgedBodySha256s.Single());
        CollectionAssert.AreEqual(
            new[] { "Login accepted: AWS4-HMAC-SHA256 AKIDEXAMPLE", "PUT /upload: the method is not served; answered 405 and closed." },
            log.Notes.Take(2).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_BodyBoundLoginWithAChangedBody_Gets401AndKeepsTheConnection()
    {
        var (connection, log) = await ServeAsync(BodyBindingPolicy(BodyCheckAccepting(BodySha256)), Put("bodY"));

        Assert.AreEqual(
            "HTTP/1.1 401 Unauthorized\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\n"
            + "WWW-Authenticate: " + DigestMd5 + "\r\n\r\n",
            Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        CollectionAssert.AreEqual(
            new[] { "Login refused: AWS4-HMAC-SHA256 AKIDEXAMPLE", "PUT /upload: 401, a login is needed" }, log.Notes.Take(2).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_BodyBoundLoginWithAChunkedBody_HashesTheChunkDataAlone()
    {
        var bodyCheck = BodyCheckAccepting(BodySha256);
        var request = Ascii($"GET /file.txt HTTP/1.1\r\nHost: h\r\n{AwsAuthorization}Transfer-Encoding: chunked\r\n\r\n2;ext=1\r\nbo\r\n2\r\ndy\r\n0\r\nTrailer: x\r\n\r\n");

        var (connection, _) = await ServeAsync(BodyBindingPolicy(bodyCheck), request);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(Latin1(connection.WrittenBytes), FileBody);
        CollectionAssert.AreEqual(BodySha256, bodyCheck.JudgedBodySha256s.Single());
    }

    [TestMethod]
    public async Task ServeAsync_BodyBoundLoginWithNoBody_IsJudgedOnTheEmptyBodysHash()
    {
        var bodyCheck = BodyCheckAccepting(SHA256.HashData([]));

        var (connection, _) = await ServeAsync(BodyBindingPolicy(bodyCheck), RecordedFixture.ReadRequestBytes("get-file"));

        CollectionAssert.AreEqual(RecordedResponse("get-file"), connection.WrittenBytes);
        Assert.HasCount(1, bodyCheck.JudgedBodySha256s);
    }

    [TestMethod]
    public async Task ServeAsync_BodyBoundLoginExpectingContinue_Sends100ContinueBeforeReadingTheBody()
    {
        var (connection, _) = await ServeAsync(BodyBindingPolicy(BodyCheckAccepting(BodySha256)), Put("body", "Expect: 100-continue\r\n"));

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 405 Method Not Allowed\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_BodyBoundLoginPastTheUploadLimit_Gets413WithoutReadingTheBody()
    {
        var bodyCheck = BodyCheckAccepting(BodySha256);

        var (connection, _) = await ServeAsync(BodyBindingPolicy(bodyCheck), Put("body"), ExchangeLimits.Default with { MaxUploadBytes = 3 });

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 413 Content Too Large\r\n");
        Assert.IsEmpty(bodyCheck.JudgedBodySha256s);
    }

    [TestMethod]
    public async Task ServeAsync_BodyBoundLoginWithABodyEndingEarly_Gets400AndIsNotJudged()
    {
        var bodyCheck = BodyCheckAccepting(BodySha256);
        var request = Ascii($"PUT /upload HTTP/1.1\r\nHost: h\r\n{AwsAuthorization}Content-Length: 10\r\n\r\nbody");

        var (connection, log) = await ServeAsync(BodyBindingPolicy(bodyCheck), request);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 400 Bad Request\r\n");
        Assert.AreEqual("PUT /upload: the body was malformed or ended early; answered 400 and closed.", log.Notes[0]);
        Assert.IsEmpty(bodyCheck.JudgedBodySha256s);
    }

    [TestMethod]
    public async Task ServeAsync_BodyBoundLoginWithAnUnreadableBody_Gets400AndIsNotJudged()
    {
        var bodyCheck = BodyCheckAccepting(BodySha256);
        var request = Ascii($"PUT /upload HTTP/1.1\r\nHost: h\r\n{AwsAuthorization}Transfer-Encoding: gzip\r\n\r\nbody");

        var (connection, log) = await ServeAsync(BodyBindingPolicy(bodyCheck), request);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 400 Bad Request\r\n");
        Assert.AreEqual(
            "PUT /upload: the login is bound to the body, and the body's framing is not one the server reads; answered 400 and closed.",
            log.Notes[0]);
        Assert.IsEmpty(bodyCheck.JudgedBodySha256s);
    }

    [TestMethod]
    public async Task ServeAsync_BodyCheckAnsweringWithAnotherBodyCheck_IsJudgedAgainOverNoBody()
    {
        // The contract says a body check's verdict carries none; one that does is still never served unchecked.
        var inner = BodyCheckAccepting(SHA256.HashData([]));
        var outer = new UnitTestRequestBodyCheck(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null, BodyCheck: inner));

        await ServeAsync(BodyBindingPolicy(outer), Put("body"));

        CollectionAssert.AreEqual(BodySha256, outer.JudgedBodySha256s.Single());
        CollectionAssert.AreEqual(SHA256.HashData([]), inner.JudgedBodySha256s.Single());
    }

    private static HttpAuthenticationVerdict Challenge(string[] challenges) => new(HttpAuthenticationOutcome.Challenge, challenges, null);

    private static string? Authorization(HttpAuthenticationRequest request) =>
        request.Fields.Where(field => field.Key == "Authorization").Select(field => field.Value).FirstOrDefault();

    private static string NtlmRequest(string token) =>
        $"GET /file.txt HTTP/1.1\r\nHost: 127.0.0.1:18114\r\nAuthorization: NTLM {token}\r\n\r\n";

    private static string Unauthorized(string challenge) =>
        $"HTTP/1.1 401 Unauthorized\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\nWWW-Authenticate: {challenge}\r\n\r\n";

    private static byte[] SecondHead(byte[] twoHeads)
    {
        var text = Latin1(twoHeads);

        return Ascii(text[(text.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..]);
    }

    private static byte[] Concat(byte[] first, byte[] second) => [.. first, .. second];

    private static HttpProtocolServer Server(IAuthenticationPolicy policy) =>
        new(new ContentStore(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot), policy);

    private async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(IAuthenticationPolicy policy, byte[] request, ExchangeLimits? limits = null)
    {
        var connection = new InMemoryConnection([request]);
        var log = new RecordingExchangeLog();

        await Server(policy).ServeAsync(connection, Context(log, new ManualTimeProvider(Now), TestContext.CancellationToken, limits));

        return (connection, log);
    }

    // The bytes-received log records the raw request, as it always has; nothing else may
    // repeat a credential: not a response byte, not a note.
    private static void AssertNoSecretIn(InMemoryConnection connection, RecordingExchangeLog log)
    {
        var written = Encoding.Latin1.GetString(connection.WrittenBytes);
        foreach (var secret in Secrets)
        {
            Assert.DoesNotContain(secret, written);
            Assert.IsFalse(log.Notes.Any(note => note.Contains(secret, StringComparison.Ordinal)), $"A note repeats {secret}.");
        }
    }
}
