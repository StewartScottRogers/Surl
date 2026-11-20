using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// The login is judged before the method (ADR-0180): POST, DELETE and PATCH are 401 on a
/// server with no account and no <c>--allow-anonymous</c>, and 405 with <c>Allow</c> under it.
/// </summary>
[TestClass]
public sealed class HttpMethodLoginOrderTests
{
    private const string DigestChallenge = "Digest realm=\"surl\", qop=\"auth\", algorithm=MD5, nonce=\"abc\"";

    private const string DateAndServer = "Date: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("POST")]
    [DataRow("DELETE")]
    [DataRow("PATCH")]
    public async Task ServeAsync_NoAccountAndNoAllowAnonymous_Answers401WithTheDigestChallengeNot405(string method)
    {
        // The real session lets only a read in when no account exists (ADR-0032 section 4).
        var policy = new UnitTestAuthenticationPolicy(request => request.IsWrite
            ? new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, [DigestChallenge], null)
            : new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));

        var written = await ServeAsync(policy, method);

        Assert.AreEqual(
            "HTTP/1.1 401 Unauthorized\r\n" + DateAndServer + "Content-Type: text/plain; charset=utf-8\r\nContent-Length: 17\r\nWWW-Authenticate: " + DigestChallenge + "\r\nConnection: close\r\n\r\n401 Unauthorized\n",
            written);
        Assert.DoesNotContain("Allow:", written);
    }

    [TestMethod]
    [DataRow("POST")]
    [DataRow("DELETE")]
    [DataRow("PATCH")]
    public async Task ServeAsync_AllowAnonymousWithoutAllowUploads_Answers405AllowingGetAndHead(string method)
    {
        var written = await ServeAsync(new AnonymousAuthenticationPolicy(), method);

        Assert.AreEqual(
            "HTTP/1.1 405 Method Not Allowed\r\n" + DateAndServer + "Allow: GET, HEAD\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: 23\r\nConnection: close\r\n\r\n405 Method Not Allowed\n",
            written);
    }

    private async Task<string> ServeAsync(IAuthenticationPolicy policy, string method)
    {
        var request = $"{method} /file.txt HTTP/1.1\r\nHost: h\r\nContent-Length: 1\r\n\r\nx";
        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes(request)]);
        var server = new HttpProtocolServer(new ContentStore(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot), policy);

        await server.ServeAsync(connection, Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), TestContext.CancellationToken));

        return Latin1(connection.WrittenBytes);
    }
}
