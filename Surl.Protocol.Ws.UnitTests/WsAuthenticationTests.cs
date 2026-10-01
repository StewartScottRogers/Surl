using System.Security.Cryptography;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ws.WsServerHarness;

namespace Surl.Protocol.Ws;

/// <summary>
/// ADR-0071 decision 3: every upgrade request is judged by the connection's
/// <see cref="IHttpAuthenticationSession"/>, after the <c>Host</c> check and before the method
/// and WebSocket fields, as the HTTP server judges a <c>GET</c> (ADR-0032 sections 4 and 6).
/// </summary>
[TestClass]
public sealed class WsAuthenticationTests
{
    private const string BasicChallenge = "Basic realm=\"surl\", charset=\"UTF-8\"";

    private const string Recorded401Response =
        "HTTP/1.1 401 Unauthorized\r\n"
        + "Date: Mon, 28 Sep 2026 12:00:00 GMT\r\n"
        + "Server: surl\r\n"
        + "Content-Length: 0\r\n"
        + "WWW-Authenticate: Basic realm=\"surl\", charset=\"UTF-8\"\r\n"
        + "\r\n";

    private static readonly HttpAuthenticationVerdict Challenge = new(HttpAuthenticationOutcome.Challenge, [BasicChallenge], null);

    private static readonly HttpAuthenticationVerdict Proceed = new(HttpAuthenticationOutcome.Proceed, [], "tester");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RecordedBasicRequest_Challenged_IsAnswered401WithTheSessionsChallenge_AndTheConnectionKept()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge);

        var (connection, log) = await ServeAsync([RecordedFixture.ReadRequestBytes("basic-401")], TestContext.CancellationToken, policy);

        Assert.AreEqual(Recorded401Response, Latin1(connection.WrittenBytes));
        Assert.IsTrue(Latin1(RecordedFixture.ReadBytes("basic-401", "transcript.txt")).Contains("< WWW-Authenticate: " + BasicChallenge, StringComparison.Ordinal));
        Assert.IsFalse(connection.WritesCompleted, "A 401 keeps the connection for the next upgrade request.");
        CollectionAssert.AreEqual(
            new[]
            {
                "WebSocket upgrade refused: 401 Unauthorized: a login is needed; the connection stays open.",
                "The client closed the connection: ConnectionClosed.",
            },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task RecordedBasicRequest_IsShownToTheSessionAsARead_WithItsAuthorizationField()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Proceed);

        var (connection, _) = await ServeAsync([RecordedFixture.ReadRequestBytes("basic-401")], TestContext.CancellationToken, policy);

        var judged = policy.JudgedRequests.Single();
        Assert.AreEqual("GET", judged.Method);
        Assert.AreEqual("/chat", judged.Target);
        Assert.IsFalse(judged.IsWrite);
        Assert.Contains(new KeyValuePair<string, string>("Authorization", "Basic dGVzdGVyOnNlY3JldA=="), judged.Fields);
        CollectionAssert.AreEqual(new TlsSession?[] { null }, policy.StartedTlsSessions.ToArray());
        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 101 Switching Protocols\r\n");
    }

    [TestMethod]
    public async Task ChallengeThenLogin_OnOneConnection_IsAnswered401Then101()
    {
        var policy = new UnitTestAuthenticationPolicy(request =>
            request.Fields.Any(field => field.Key == "Authorization") ? Proceed : Challenge);
        var withoutLogin = RecordedFixture.ReadRequestBytes("upgrade-101");
        var withLogin = RecordedUpgradeRequestWith("Host: 127.0.0.1:18301\r\n", "Host: 127.0.0.1:18301\r\nAuthorization: Basic dGVzdGVyOnNlY3JldA==\r\n");

        var (connection, log) = await ServeAsync([withoutLogin, withLogin], TestContext.CancellationToken, policy);

        Assert.AreEqual(Recorded401Response + Recorded101Response, Latin1(connection.WrittenBytes));
        Assert.HasCount(2, policy.JudgedRequests);
        Assert.AreEqual("WebSocket upgrade accepted for /chat", log.Notes[1]);
    }

    [TestMethod]
    [DataRow(" HTTP/1.1\r\n", " HTTP/1.0\r\n", DisplayName = "HTTP/1.0")]
    [DataRow("Connection: Upgrade\r\n", "Connection: Upgrade, close\r\n", DisplayName = "Connection: close")]
    [DataRow("Connection: Upgrade\r\n", "Connection: Upgrade\r\nContent-Length: 3\r\n", DisplayName = "a body announced")]
    public async Task Challenge_ThatCannotKeepTheConnection_IsARefusal(string oldText, string newText)
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge);

        var (connection, log) = await ServeAsync([RecordedUpgradeRequestWith(oldText, newText)], TestContext.CancellationToken, policy);

        Assert.AreEqual(
            "HTTP/1.1 401 Unauthorized\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\n"
                + "WWW-Authenticate: " + BasicChallenge + "\r\nConnection: close\r\n\r\n",
            Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("WebSocket upgrade refused: 401 Unauthorized: a login is needed; closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task Forbidden_IsAnswered403AndClosed()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Forbidden, [], null));

        var (connection, log) = await ServeAsync([RecordedFixture.ReadRequestBytes("basic-401")], TestContext.CancellationToken, policy);

        Assert.AreEqual(Refusal("403 Forbidden"), Latin1(connection.WrittenBytes));
        Assert.AreEqual("WebSocket upgrade refused: 403 Forbidden: the authentication policy forbade the upgrade; closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task Login_IsJudgedBeforeTheMethod()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge);

        var (connection, _) = await ServeAsync([RecordedFixture.ReadRequestBytes("head-405")], TestContext.CancellationToken, policy);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 401 Unauthorized\r\n");
    }

    [TestMethod]
    public async Task HostCheck_ComesBeforeTheLogin()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Challenge);

        var (connection, _) = await ServeAsync([RecordedUpgradeRequestWith("Host: 127.0.0.1:18301\r\n", "")], TestContext.CancellationToken, policy);

        Assert.AreEqual(Refusal("400 Bad Request"), Latin1(connection.WrittenBytes));
        Assert.IsEmpty(policy.JudgedRequests);
    }

    [TestMethod]
    public async Task CheckedLogin_IsNoted_AndProceedsChallengeValues_AreWrittenOnThe101()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(
            HttpAuthenticationOutcome.Proceed, ["Negotiate oRQwEqADCgEA"], "tester", new CheckedLogin("Negotiate", "tester", true)));

        var (connection, log) = await ServeAsync([RecordedFixture.ReadRequestBytes("upgrade-101")], TestContext.CancellationToken, policy);

        Assert.AreEqual(
            Recorded101Response.Replace("\r\n\r\n", "\r\nWWW-Authenticate: Negotiate oRQwEqADCgEA\r\n\r\n", StringComparison.Ordinal),
            Latin1(connection.WrittenBytes));
        Assert.AreEqual("Login accepted: Negotiate tester", log.Notes[0]);
    }

    [TestMethod]
    public async Task ChallengeValues_AreWrittenOnALaterRefusal()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, ["Negotiate oRQwEqADCgEA"], "tester"));

        var (connection, _) = await ServeAsync([RecordedFixture.ReadRequestBytes("head-405")], TestContext.CancellationToken, policy);

        Assert.AreEqual(
            "HTTP/1.1 405 Method Not Allowed\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nAllow: GET\r\nContent-Length: 0\r\n"
                + "WWW-Authenticate: Negotiate oRQwEqADCgEA\r\nConnection: close\r\n\r\n",
            Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task LoginBoundToTheBody_IsCheckedAgainstTheEmptyBody_AndItsVerdictAnswers()
    {
        var bodyCheck = new UnitTestRequestBodyCheck(_ => new HttpAuthenticationVerdict(
            HttpAuthenticationOutcome.Proceed, [], "AKID", new CheckedLogin("AWS4-HMAC-SHA256", "AKID", true)));
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null, null, bodyCheck));

        var (connection, log) = await ServeAsync([RecordedFixture.ReadRequestBytes("upgrade-101")], TestContext.CancellationToken, policy);

        CollectionAssert.AreEqual(SHA256.HashData(ReadOnlySpan<byte>.Empty), bodyCheck.JudgedBodySha256s.Single());
        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
        Assert.AreEqual("Login accepted: AWS4-HMAC-SHA256 AKID", log.Notes[0]);
    }

    [TestMethod]
    public async Task LoginBoundToTheBody_ThatTheBodyCheckRefuses_IsChallenged()
    {
        var bodyCheck = new UnitTestRequestBodyCheck(_ => new HttpAuthenticationVerdict(
            HttpAuthenticationOutcome.Challenge, [BasicChallenge], null, new CheckedLogin("AWS4-HMAC-SHA256", "AKID", false)));
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(
            HttpAuthenticationOutcome.Proceed, [], null, new CheckedLogin("AWS4-HMAC-SHA256", "AKID", true), bodyCheck));

        var (connection, log) = await ServeAsync([RecordedFixture.ReadRequestBytes("upgrade-101")], TestContext.CancellationToken, policy);

        Assert.AreEqual(Recorded401Response, Latin1(connection.WrittenBytes));
        Assert.AreEqual("Login accepted: AWS4-HMAC-SHA256 AKID", log.Notes[0]);
        Assert.AreEqual("Login refused: AWS4-HMAC-SHA256 AKID", log.Notes[1]);
    }
}
