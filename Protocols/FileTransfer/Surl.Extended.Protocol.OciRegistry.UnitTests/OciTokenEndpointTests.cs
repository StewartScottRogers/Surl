using H = Surl.Extended.Protocol.OciRegistry.OciRegistryServerHarness;
using P = Surl.Extended.Protocol.OciRegistry.UnitTestAuthenticationPolicy;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The registry's own token endpoint, <c>GET /token</c>, whose bytes are written from the Docker
/// Token Authentication Specification's "Requesting a Token" and "Token Response Fields" and
/// RFC 7617 (ADR-0115, decisions 4.3 and 4.4).
/// </summary>
[TestClass]
public sealed class OciTokenEndpointTests
{
    private const string BasicChallenge = "WWW-Authenticate: Basic realm=\"surl\", charset=\"UTF-8\"\r\n";

    private const string AppPullAndPush = "scope=repository:app:pull,push";

    [TestMethod]
    public async Task AnswerAsync_GoodCredentials_IssuesATokenWithItsLifetimeAndIssueTime()
    {
        var harness = H.AllowingUploads();

        var response = await harness.SendAsync("GET", $"/token?service=surl&{AppPullAndPush}&account=tester&client_id=docker", P.BasicAuthorization);

        var token = CountingIdentifierRandom.TokenOf(1);
        var body = $"{{\"token\":\"{token}\",\"access_token\":\"{token}\",\"expires_in\":300,\"issued_at\":\"2026-11-05T12:00:00Z\"}}";
        Assert.AreEqual($"HTTP/1.1 200 OK\r\n{H.CommonFields}Content-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}", response);
        Assert.AreEqual("tester", harness.Policy.Logins.Single().UserName);
    }

    [TestMethod]
    public async Task AnswerAsync_NoQueryAtAll_IssuesATokenForNoScope()
    {
        var harness = new H();

        var response = await harness.SendAsync("GET", "/token", P.BasicAuthorization);

        var token = CountingIdentifierRandom.TokenOf(1);
        var body = $"{{\"token\":\"{token}\",\"access_token\":\"{token}\",\"expires_in\":300,\"issued_at\":\"2026-11-05T12:00:00Z\"}}";
        Assert.AreEqual($"HTTP/1.1 200 OK\r\n{H.CommonFields}Content-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}", response);
    }

    [TestMethod]
    public async Task AnswerAsync_GoodCredentials_TokenServesAPushAsTheAccount()
    {
        var harness = H.AllowingUploads();
        await harness.SendAsync("GET", $"/token?{AppPullAndPush}", P.BasicAuthorization);

        var response = await harness.SendAsync("POST", "/v2/app/blobs/uploads/", $"Authorization: Bearer {CountingIdentifierRandom.TokenOf(1)}", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 202 Accepted\r\n");
    }

    [TestMethod]
    [DataRow("Authorization: Basic dGVzdGVyOndyb25n")]
    [DataRow("Authorization: Basic bm9ib2R5OnNlY3JldA==")]
    [DataRow("Authorization: Basic dGVzdGVy")]
    [DataRow("Authorization: Basic !!!")]
    [DataRow("Authorization: Bearer abc")]
    public async Task AnswerAsync_BadOrUnreadableCredentials_Returns401WithTheBasicChallenge(string authorization)
    {
        var harness = H.AllowingUploads();

        var response = await harness.SendAsync("GET", $"/token?{AppPullAndPush}", authorization);

        Assert.AreEqual(H.ExpectedError("401 Unauthorized", "UNAUTHORIZED", "authentication required", BasicChallenge), response);
    }

    [TestMethod]
    public async Task AnswerAsync_NoCredentialsWithAnonymousOff_Returns401WithTheBasicChallenge()
    {
        var harness = H.AllowingUploads();

        var response = await harness.SendAsync("GET", $"/token?{AppPullAndPush}");

        Assert.AreEqual(H.ExpectedError("401 Unauthorized", "UNAUTHORIZED", "authentication required", BasicChallenge), response);
    }

    [TestMethod]
    public async Task AnswerAsync_NoCredentialsUnderAllowAnonymous_IssuesAPullOnlyToken()
    {
        var harness = H.AllowingUploads();
        harness.Policy.AllowsAnonymous = true;
        harness.CreateRepository("app");
        var tokenAnswer = await harness.SendAsync("GET", $"/token?{AppPullAndPush}");
        var bearer = $"Authorization: Bearer {CountingIdentifierRandom.TokenOf(1)}";

        var pull = await harness.SendAsync("GET", "/v2/app/tags/list", bearer);
        var push = await harness.SendAsync("POST", "/v2/app/blobs/uploads/", bearer, "Content-Length: 0");

        StringAssert.StartsWith(tokenAnswer, "HTTP/1.1 200 OK\r\n");
        StringAssert.StartsWith(pull, "HTTP/1.1 200 OK\r\n");
        StringAssert.StartsWith(push, "HTTP/1.1 403 Forbidden\r\n");
        StringAssert.Contains(push, "error=\"insufficient_scope\"");
    }

    [TestMethod]
    public async Task AnswerAsync_CredentialsUnderAllowAnonymous_AreAcceptedUncheckedAsAnonymousPullOnly()
    {
        var harness = H.AllowingUploads();
        harness.Policy.AllowsAnonymous = true;
        await harness.SendAsync("GET", $"/token?{AppPullAndPush}", "Authorization: Basic dGVzdGVyOndyb25n");

        var push = await harness.SendAsync("POST", "/v2/app/blobs/uploads/", $"Authorization: Bearer {CountingIdentifierRandom.TokenOf(1)}", "Content-Length: 0");

        StringAssert.StartsWith(push, "HTTP/1.1 403 Forbidden\r\n");
    }

    [TestMethod]
    public async Task AnswerAsync_CredentialsOverPlainTextRefused_Returns403Denied()
    {
        var harness = H.AllowingUploads();
        harness.Policy.AllowsPlaintext = false;

        var response = await harness.SendAsync("GET", $"/token?{AppPullAndPush}", P.BasicAuthorization);

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", "credentials over plain text are refused"), response);
    }

    [TestMethod]
    public async Task AnswerAsync_Post_Returns405WithAllowGet()
    {
        var harness = H.AllowingUploads();

        var response = await harness.SendAsync("POST", "/token", P.BasicAuthorization, "Content-Length: 0");

        Assert.AreEqual(H.ExpectedError("405 Method Not Allowed", "UNSUPPORTED", "the operation is unsupported", "Allow: GET\r\n"), response);
    }

    [TestMethod]
    public async Task AnswerAsync_AnotherService_Returns400Unsupported()
    {
        var harness = H.AllowingUploads();

        var response = await harness.SendAsync("GET", $"/token?service=registry.example&{AppPullAndPush}", P.BasicAuthorization);

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "UNSUPPORTED", "the operation is unsupported"), response);
        Assert.IsEmpty(harness.Policy.Logins);
    }

    [TestMethod]
    public async Task AnswerAsync_PastTheLiveTokenBound_Returns429TooManyRequests()
    {
        var harness = H.WithTokenBound(1);
        await harness.SendAsync("GET", $"/token?{AppPullAndPush}", P.BasicAuthorization);

        var response = await harness.SendAsync("GET", $"/token?{AppPullAndPush}", P.BasicAuthorization);

        Assert.AreEqual(H.ExpectedError("429 Too Many Requests", "TOOMANYREQUESTS", "too many requests"), response);
    }

    [TestMethod]
    [DataRow("scope=repository:app:fly", "GET", "/v2/app/tags/list", "403 Forbidden")]
    [DataRow("scope=registry:catalog:*", "GET", "/v2/app/tags/list", "403 Forbidden")]
    [DataRow("scope=repository:App:pull", "GET", "/v2/app/tags/list", "403 Forbidden")]
    [DataRow("scope=repository:app:pull:extra", "GET", "/v2/app/tags/list", "403 Forbidden")]
    [DataRow("scope=repository:other:pull%20repository:app:pull", "GET", "/v2/app/tags/list", "200 OK")]
    [DataRow("scope=repository:app:pull&scope=repository:app:push", "POST", "/v2/app/blobs/uploads/", "202 Accepted")]
    [DataRow("scope=repository:app:*", "DELETE", "/v2/app/manifests/sha256:0000000000000000000000000000000000000000000000000000000000000000", "404 Not Found")]
    [DataRow("scope=repository:app:pull,push", "DELETE", "/v2/app/manifests/sha256:0000000000000000000000000000000000000000000000000000000000000000", "403 Forbidden")]
    public async Task AnswerAsync_Scopes_GrantWhatTheyNameAndDropTheRest(string scopes, string method, string target, string status)
    {
        var harness = H.AllowingUploads();
        harness.CreateRepository("app");
        await harness.SendAsync("GET", $"/token?{scopes}", P.BasicAuthorization);

        var response = await harness.SendAsync(method, target, $"Authorization: Bearer {CountingIdentifierRandom.TokenOf(1)}", "Content-Length: 0");

        StringAssert.StartsWith(response, $"HTTP/1.1 {status}\r\n");
    }

    [TestMethod]
    public async Task AnswerAsync_ScopesPastSixteen_AreDropped()
    {
        var harness = H.AllowingUploads();
        harness.CreateRepository("app");
        var others = string.Concat(Enumerable.Range(1, OciTokenEndpoint.MaxScopes).Select(number => $"scope=repository:r{number}:pull&"));
        await harness.SendAsync("GET", $"/token?{others}scope=repository:app:pull", P.BasicAuthorization);

        var response = await harness.SendAsync("GET", "/v2/app/tags/list", $"Authorization: Bearer {CountingIdentifierRandom.TokenOf(1)}");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
    }

    [TestMethod]
    public async Task AnswerAsync_UploadsOffAndEveryActionAsked_StillGrantsPull()
    {
        var harness = new H();
        harness.CreateRepository("app");
        await harness.SendAsync("GET", "/token?scope=repository:app:*", P.BasicAuthorization);

        var response = await harness.SendAsync("GET", "/v2/app/tags/list", $"Authorization: Bearer {CountingIdentifierRandom.TokenOf(1)}");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    [DataRow("/token", true)]
    [DataRow("/token?scope=x", true)]
    [DataRow("/tokens", false)]
    [DataRow("/v2/token", false)]
    public void IsTokenTarget_Target_IsTrueForTheTokenPathOnly(string target, bool expected)
    {
        var isToken = OciTokenEndpoint.IsTokenTarget(target);

        Assert.AreEqual(expected, isToken);
    }
}
