using System.Net.Security;
using System.Security.Authentication;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.HttpProxy.ProxyTestKit;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// <c>Proxy-Authorization</c> (ADR-0088 decision 5 and Amendment 3), driven with the bytes pinned
/// upstream curl 8.21.0 sent with <c>-U</c>, <c>--proxy-anyauth</c>, <c>--proxy-digest</c> and
/// <c>--proxy-ntlm</c>; every <c>response.bin</c> is what curl was measured completing against
/// (Fixtures/README.md).
/// </summary>
[TestClass]
public sealed class HttpProxyProtocolServerAuthenticationTests
{
    private const string OriginResponse = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    private const string ForwardedGet = "GET /a HTTP/1.1\r\nHost: origin.test\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nVia: 1.1 surl\r\n\r\n";

    [TestMethod]
    public void Constructor_NullProxyAuthenticationPolicy_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpProxyProtocolServer(new InMemoryOriginConnector(), null!));
    }

    [TestMethod]
    public async Task ServeAsync_ForwardedGetWithoutCredentials_Answers407WithTheChallengesAndKeepsTheConnection()
    {
        var policy = new UnitTestProxyAuthenticationPolicy();
        var origins = new InMemoryOriginConnector();
        var client = ConnectionOf(Fixture("proxy-unauthorized-407", "request.bin"));
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(client, origins, policy, log);

        Assert.AreEqual(FixtureText("proxy-unauthorized-407", "response.bin"), written);
        Assert.IsEmpty(origins.Requests);
        Assert.IsFalse(client.WritesCompleted);
        Assert.AreEqual("/a", policy.JudgedRequests.Single().Target);
        CollectionAssert.Contains(log.Notes.ToList(), "Answered 407: GET http://origin.test/a: a proxy login is needed.");
    }

    [TestMethod]
    public async Task ServeAsync_ConnectWithoutCredentials_Answers407CurlReportsAsATunnelFailure()
    {
        var policy = new UnitTestProxyAuthenticationPolicy();
        var origins = new InMemoryOriginConnector();
        var client = ConnectionOf(Fixture("proxy-connect-unauthorized-407", "request.bin"));

        var written = await ServeAsync(client, origins, policy);

        Assert.AreEqual(FixtureText("proxy-connect-unauthorized-407", "response.bin"), written);
        Assert.IsEmpty(origins.Requests);
        Assert.AreEqual("origin.test:8080", policy.JudgedRequests.Single().Target);
    }

    [TestMethod]
    public async Task ServeAsync_ForwardedGetWithBasic_ForwardsItWithoutProxyAuthorization()
    {
        var policy = new UnitTestProxyAuthenticationPolicy();
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var client = ConnectionOf(Fixture("proxy-basic-forward", "request.bin"));

        var written = await ServeAsync(client, origins, policy);

        Assert.AreEqual(FixtureText("proxy-basic-forward", "response.bin"), written);
        Assert.AreEqual(ForwardedGet, Latin1(origin.WrittenBytes));
        Assert.AreEqual("Basic dTpw", Authorization(policy.JudgedRequests.Single()));
    }

    [TestMethod]
    public async Task ServeAsync_ConnectWithBasic_TunnelsWithoutProxyAuthorizationReachingTheOrigin()
    {
        var policy = new UnitTestProxyAuthenticationPolicy();
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 8080, origin);
        var client = ConnectionOf(Fixture("proxy-basic-connect", "request-1.bin"), Fixture("proxy-basic-connect", "request-2.bin"));

        var written = await ServeAsync(client, origins, policy);

        Assert.AreEqual(FixtureText("proxy-basic-connect", "response.bin"), written);
        Assert.AreEqual(FixtureText("proxy-basic-connect", "request-2.bin"), Latin1(origin.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_AnyauthDigestForwardedGet_Challenges407ThenForwardsWithThePathAsTheDigestUri()
    {
        var policy = new UnitTestProxyAuthenticationPolicy();
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var client = ConnectionOf(Fixture("proxy-anyauth-digest-forward", "request-1.bin"), Fixture("proxy-anyauth-digest-forward", "request-2.bin"));

        var written = await ServeAsync(client, origins, policy);

        Assert.AreEqual(FixtureText("proxy-anyauth-digest-forward", "response.bin"), written);
        Assert.AreEqual(ForwardedGet.Replace("GET /a ", "GET /a?b ", StringComparison.Ordinal), Latin1(origin.WrittenBytes));
        Assert.AreEqual("/a?b", policy.JudgedRequests[1].Target);
        Assert.HasCount(1, policy.StartedTlsSessions);
    }

    [TestMethod]
    public async Task ServeAsync_DigestConnect_Challenges407ThenTunnelsWithTheAuthorityAsTheDigestUri()
    {
        var policy = new UnitTestProxyAuthenticationPolicy();
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 8080, origin);
        var client = ConnectionOf(
            Fixture("proxy-digest-connect", "request-1.bin"),
            Fixture("proxy-digest-connect", "request-2.bin"),
            Fixture("proxy-digest-connect", "request-3.bin"));

        var written = await ServeAsync(client, origins, policy);

        Assert.AreEqual(FixtureText("proxy-digest-connect", "response.bin"), written);
        Assert.AreEqual(FixtureText("proxy-digest-connect", "request-3.bin"), Latin1(origin.WrittenBytes));
        Assert.AreEqual("origin.test:8080", policy.JudgedRequests[1].Target);
    }

    [TestMethod]
    public async Task ServeAsync_NtlmForwardedGet_KeepsOneConnectionAndSessionAcrossTheLegsThenForwards()
    {
        var policy = new UnitTestProxyAuthenticationPolicy();
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var client = ConnectionOf(Fixture("proxy-ntlm-forward", "request-1.bin"), Fixture("proxy-ntlm-forward", "request-2.bin"));

        var written = await ServeAsync(client, origins, policy);

        Assert.AreEqual(FixtureText("proxy-ntlm-forward", "response.bin"), written);
        Assert.AreEqual(ForwardedGet, Latin1(origin.WrittenBytes));
        Assert.HasCount(2, policy.JudgedRequests);
        Assert.HasCount(1, policy.StartedTlsSessions);
    }

    [TestMethod]
    public async Task ServeAsync_NtlmConnect_KeepsOneConnectionAcrossTheLegsThenTunnels()
    {
        var policy = new UnitTestProxyAuthenticationPolicy();
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 8080, origin);
        var client = ConnectionOf(
            Fixture("proxy-ntlm-connect", "request-1.bin"),
            Fixture("proxy-ntlm-connect", "request-2.bin"),
            Fixture("proxy-ntlm-connect", "request-3.bin"));

        var written = await ServeAsync(client, origins, policy);

        Assert.AreEqual(FixtureText("proxy-ntlm-connect", "response.bin"), written);
        Assert.AreEqual(FixtureText("proxy-ntlm-connect", "request-3.bin"), Latin1(origin.WrittenBytes));
        Assert.HasCount(1, policy.StartedTlsSessions);
    }

    [TestMethod]
    public async Task ServeAsync_NtlmAuthenticateOnAFreshConnection_Answers407()
    {
        // The legs are bound to curl's connection: the AUTHENTICATE_MESSAGE alone, on a connection
        // that never sent the NEGOTIATE_MESSAGE, is not let through.
        var policy = new UnitTestProxyAuthenticationPolicy();
        var origins = new InMemoryOriginConnector();
        var client = ConnectionOf(Fixture("proxy-ntlm-forward", "request-2.bin"));

        var written = await ServeAsync(client, origins, policy);

        StringAssert.StartsWith(written, "HTTP/1.1 407 Proxy Authentication Required\r\n");
        Assert.IsEmpty(origins.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_WrongCredentialsTwice_Answers407EachTimeOnOneConnection()
    {
        var policy = new UnitTestProxyAuthenticationPolicy();
        var origins = new InMemoryOriginConnector();
        var wrong = "GET http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\nProxy-Authorization: Basic dTp3cm9uZw==\r\n\r\n";
        var client = Connection(wrong, wrong.Replace("dTp3cm9uZw==", "dTpw", StringComparison.Ordinal).Replace("Basic", "Bearer", StringComparison.Ordinal));
        var unauthorized = FixtureText("proxy-unauthorized-407", "response.bin");

        var written = await ServeAsync(client, origins, policy);

        Assert.AreEqual(unauthorized + unauthorized, written);
        Assert.IsEmpty(origins.Requests);
        Assert.IsFalse(client.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_ChallengedRequestWithABody_Answers407AndCloses()
    {
        var policy = new UnitTestProxyAuthenticationPolicy();
        var client = Connection("POST http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\nContent-Length: 3\r\n\r\nabc");

        var written = await ServeAsync(client, new InMemoryOriginConnector(), policy);

        StringAssert.Contains(written, "Content-Length: 34\r\nConnection: close\r\n\r\n407 Proxy Authentication Required\n");
        Assert.IsTrue(client.WritesCompleted);
        Assert.IsTrue(policy.JudgedRequests.Single().IsWrite);
    }

    [TestMethod]
    public async Task ServeAsync_ChallengedRequestAskingToClose_Answers407AndCloses()
    {
        var policy = new UnitTestProxyAuthenticationPolicy();
        var client = HeldOpenConnection("HEAD http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\nProxy-Connection: close\r\n\r\n");

        var written = await ServeAsync(client, new InMemoryOriginConnector(), policy);

        StringAssert.Contains(written, "Connection: close\r\n");
        Assert.IsTrue(client.WritesCompleted);
        Assert.IsFalse(policy.JudgedRequests.Single().IsWrite);
    }

    [TestMethod]
    public async Task ServeAsync_PolicyForbidsTheRequest_Answers403AndCloses()
    {
        var policy = new UnitTestProxyAuthenticationPolicy { RefusalOutcome = HttpAuthenticationOutcome.Forbidden };
        var origins = new InMemoryOriginConnector();
        var client = HeldOpenConnection("CONNECT origin.test:443 HTTP/1.1\r\nHost: origin.test:443\r\n\r\n");

        var written = await ServeAsync(client, origins, policy);

        Assert.AreEqual(Answer(403, "Forbidden", closes: true), written);
        Assert.IsEmpty(origins.Requests);
        Assert.IsTrue(client.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_VerdictWithACheckedLogin_NotesItWithoutTheCredentials()
    {
        var checkedLogin = new CheckedLogin("Basic", "u", IsAccepted: true);
        var policy = new UnitTestProxyAuthenticationPolicy { CheckedLogin = checkedLogin };
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, Connection(OriginResponse));
        var client = ConnectionOf(Fixture("proxy-basic-forward", "request.bin"));
        var log = new RecordingExchangeLog();

        await ServeAsync(client, origins, policy, log);

        CollectionAssert.Contains(log.Notes.ToList(), checkedLogin.Note);
        Assert.IsFalse(log.Notes.Any(note => note.Contains("dTpw", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_OriginAuthorizationAndProxyAuthorization_JudgesOnlyTheProxysAndForwardsTheOrigins()
    {
        var policy = new UnitTestProxyAuthenticationPolicy();
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var client = Connection("GET http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\nAuthorization: Basic b3JpZ2luOnB3\r\nProxy-Authorization: Basic dTpw\r\n\r\n");

        await ServeAsync(client, origins, policy);

        Assert.AreEqual("Basic dTpw", Authorization(policy.JudgedRequests.Single()));
        Assert.HasCount(1, policy.JudgedRequests.Single().Fields.Where(field => field.Key == "Authorization"));
        Assert.AreEqual("GET /a HTTP/1.1\r\nHost: origin.test\r\nAuthorization: Basic b3JpZ2luOnB3\r\nVia: 1.1 surl\r\n\r\n", Latin1(origin.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_TargetNotAbsoluteForm_IsJudgedAsReceivedBeforeItIsRefused()
    {
        var policy = new UnitTestProxyAuthenticationPolicy();
        var client = Connection("GET /a HTTP/1.1\r\nHost: origin.test\r\nProxy-Authorization: Basic dTpw\r\n\r\n");

        var written = await ServeAsync(client, new InMemoryOriginConnector(), policy);

        Assert.AreEqual("/a", policy.JudgedRequests.Single().Target);
        StringAssert.StartsWith(written, "HTTP/1.1 400 Bad Request\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_HttpsProxyConnection_StartsTheSessionWithItsTls()
    {
        var tls = new TlsSession(SslProtocols.Tls13, TlsCipherSuite.TLS_AES_128_GCM_SHA256, "http/1.1", "localhost", null);
        var policy = new UnitTestProxyAuthenticationPolicy();
        var client = new InMemoryConnection([Bytes("GET http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\n\r\n")], initialTlsSession: tls);

        await ServeAsync(client, new InMemoryOriginConnector(), policy);

        Assert.AreEqual(tls, policy.StartedTlsSessions.Single());
    }

    [TestMethod]
    public async Task ServeAsync_NoProxyAccounts_ForwardsARequestWithoutCredentials()
    {
        // ADR-0088 decision 5: with no proxy account the proxy takes no login; the
        // policy-less constructor is the composition's for that case.
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var client = ConnectionOf(Fixture("proxy-unauthorized-407", "request.bin"));

        var written = await ServeAsync(client, origins);

        Assert.AreEqual(FixtureText("proxy-basic-forward", "response.bin"), written);
        Assert.AreEqual(ForwardedGet, Latin1(origin.WrittenBytes));
    }

    private static string? Authorization(HttpAuthenticationRequest request) =>
        request.Fields.FirstOrDefault(field => field.Key == "Authorization").Value;
}
