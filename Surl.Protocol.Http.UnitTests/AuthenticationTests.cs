using System.Net.Security;
using System.Security.Authentication;
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
