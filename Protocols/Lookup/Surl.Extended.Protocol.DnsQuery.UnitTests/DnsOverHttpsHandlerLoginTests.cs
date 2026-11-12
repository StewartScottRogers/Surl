using System.Net;
using System.Text;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.DnsQuery.DnsOverHttpsHandlerTests;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The optional DNS over HTTPS login (ADR-0092 decision 5; ADR-0084 decision 4): every request to
/// <c>/dns-query</c> is judged by the connection's <see cref="IHttpAuthenticationSession"/> first.
/// </summary>
[TestClass]
public sealed class DnsOverHttpsHandlerLoginTests
{
    private const string BasicCredentials = "Basic YWxpY2U6c2VjcmV0";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnswerAsync_AccountConfiguredAndNoCredentials_Answers401WithTheSessionsChallenges()
    {
        var written = await SendAsync(new CredentialsSession(HttpAuthenticationOutcome.Challenge), GetRequest(null));

        var head = Encoding.ASCII.GetString(written);
        StringAssert.StartsWith(head, "HTTP/1.1 401 Unauthorized\r\n");
        StringAssert.Contains(head, "\r\nWWW-Authenticate: Basic realm=\"surl\"\r\n");
        StringAssert.Contains(head, "\r\nWWW-Authenticate: Digest realm=\"surl\"\r\n");
        Assert.DoesNotContain("application/dns-message", head);
    }

    [TestMethod]
    public async Task AnswerAsync_AccountConfiguredAndBasicCredentials_Answers200WithTheDnsMessage()
    {
        var session = new CredentialsSession(HttpAuthenticationOutcome.Challenge);

        var written = await SendAsync(session, GetRequest(BasicCredentials));

        StringAssert.StartsWith(Encoding.ASCII.GetString(written), "HTTP/1.1 200 OK\r\n");
        CollectionAssert.AreEqual(ExampleAnswer, written[^ExampleAnswer.Length..]);
        Assert.AreEqual("GET", session.Judged.Single().Method);
        Assert.IsFalse(session.Judged.Single().IsWrite);
    }

    [TestMethod]
    public async Task AnswerAsync_PostWithoutCredentials_Answers401AndJudgesItAsARead()
    {
        var session = new CredentialsSession(HttpAuthenticationOutcome.Challenge);
        var head = "POST /dns-query HTTP/1.1\r\nHost: a\r\nContent-Type: application/dns-message\r\nContent-Length: 33\r\nConnection: close\r\n\r\n";

        var written = await SendAsync(session, head, ExampleQuery);

        StringAssert.StartsWith(Encoding.ASCII.GetString(written), "HTTP/1.1 401 Unauthorized\r\n");
        Assert.IsFalse(session.Judged.Single().IsWrite);
    }

    [TestMethod]
    public async Task AnswerAsync_SessionForbidsTheRequest_Answers403()
    {
        var written = await SendAsync(new CredentialsSession(HttpAuthenticationOutcome.Forbidden), GetRequest(null));

        var head = Encoding.ASCII.GetString(written);
        StringAssert.StartsWith(head, "HTTP/1.1 403 Forbidden\r\n");
        Assert.DoesNotContain("WWW-Authenticate", head);
    }

    [TestMethod]
    public async Task AnswerAsync_NoAccountConfigured_Answers200ToTheRfc8484ExampleWithNoAuthorization()
    {
        var written = await SendAsync(null, GetRequest(null));

        StringAssert.StartsWith(Encoding.ASCII.GetString(written), "HTTP/1.1 200 OK\r\n");
        CollectionAssert.AreEqual(ExampleAnswer, written[^ExampleAnswer.Length..]);
    }

    [TestMethod]
    public async Task AnswerAsync_UnknownPathWithAnAccountConfigured_Answers404WithoutJudgingTheLogin()
    {
        var session = new CredentialsSession(HttpAuthenticationOutcome.Challenge);

        var written = await SendAsync(session, "GET /other HTTP/1.1\r\nHost: a\r\nConnection: close\r\n\r\n");

        StringAssert.StartsWith(Encoding.ASCII.GetString(written), "HTTP/1.1 404 Not Found\r\n");
        Assert.IsEmpty(session.Judged);
    }

    private static string GetRequest(string? authorization) =>
        $"GET /dns-query?dns={ExampleGetParameter} HTTP/1.1\r\nHost: dnsserver.example.net\r\n{(authorization is null ? string.Empty : $"Authorization: {authorization}\r\n")}Connection: close\r\n\r\n";

    private async Task<byte[]> SendAsync(IHttpAuthenticationSession? session, string head, byte[]? body = null)
    {
        var connection = new InMemoryConnection([(byte[])[.. Encoding.ASCII.GetBytes(head), .. body ?? []]]);
        var context = new ExchangeContext(
            1,
            new ListenUrl("http", "127.0.0.1", 443).WithBoundPort(443),
            new IPEndPoint(IPAddress.Loopback, 443),
            new IPEndPoint(IPAddress.Loopback, 50000),
            new RecordingExchangeLog(),
            new ManualTimeProvider(),
            TestContext.CancellationToken);

        await new Http1ConnectionServer(new DnsOverHttpsHandler(Zone(), context, authentication: session), context).ServeAsync(connection);

        return connection.WrittenBytes;
    }

    // Serves a request that carries the Basic credentials of alice; challenges (or forbids) any other.
    private sealed class CredentialsSession(HttpAuthenticationOutcome refusal) : IHttpAuthenticationSession
    {
        public List<HttpAuthenticationRequest> Judged { get; } = [];

        public ValueTask<HttpAuthenticationVerdict> JudgeAsync(HttpAuthenticationRequest request, CancellationToken cancellationToken)
        {
            Judged.Add(request);
            var proceeds = request.Fields.Any(field => field.Key == "Authorization" && field.Value == BasicCredentials);
            return ValueTask.FromResult(proceeds
                ? new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "alice")
                : new HttpAuthenticationVerdict(refusal, ["Basic realm=\"surl\"", "Digest realm=\"surl\""], null));
        }
    }
}
