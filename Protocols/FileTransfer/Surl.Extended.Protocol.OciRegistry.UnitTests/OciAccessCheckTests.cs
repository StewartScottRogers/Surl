using System.Text;
using Surl.Protocol.Abstractions;
using H = Surl.Extended.Protocol.OciRegistry.OciRegistryServerHarness;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Token authentication on every <c>/v2/</c> request, whose bytes are written from the Docker
/// Token Authentication Specification's "How to authenticate", RFC 6750 section 3 and the
/// distribution specification's error codes (ADR-0115, decisions 4.1 and 4.4).
/// </summary>
[TestClass]
public sealed class OciAccessCheckTests
{
    private const string Realm = "Bearer realm=\"http://127.0.0.1:5000/token\",service=\"surl\"";

    private const string Unauthorized = "authentication required";

    private const string Denied = "requested access to the resource is denied";

    private static string Challenge(string? scope = null, string? error = null) =>
        $"WWW-Authenticate: {Realm}{(scope is null ? string.Empty : $",scope=\"{scope}\"")}{(error is null ? string.Empty : $",error=\"{error}\"")}\r\n";

    private static H Registry()
    {
        var harness = H.AllowingUploads();
        harness.SendsTokens = false;
        harness.CreateRepository("library/alpine");

        return harness;
    }

    private static string Bearer(string token) => $"Authorization: Bearer {token}";

    [TestMethod]
    public async Task CheckAsync_PullWithoutAToken_Returns401WithTheRealmServiceAndScope()
    {
        var harness = Registry();

        var response = await harness.SendAsync("GET", "/v2/library/alpine/manifests/latest");

        Assert.AreEqual(H.ExpectedError("401 Unauthorized", "UNAUTHORIZED", Unauthorized, Challenge("repository:library/alpine:pull")), response);
    }

    [TestMethod]
    public async Task CheckAsync_HeadWithoutAToken_Returns401WithTheChallengeAndNoBody()
    {
        var harness = Registry();

        var response = await harness.SendAsync("HEAD", "/v2/library/alpine/manifests/latest");

        var length = H.ErrorBody("UNAUTHORIZED", Unauthorized).Length;
        Assert.AreEqual($"HTTP/1.1 401 Unauthorized\r\n{H.CommonFields}Content-Type: application/json\r\n{Challenge("repository:library/alpine:pull")}Content-Length: {length}\r\nConnection: close\r\n\r\n", response);
    }

    [TestMethod]
    public async Task CheckAsync_V2WithoutAToken_Returns401WithNoScope()
    {
        var harness = Registry();

        var response = await harness.SendAsync("GET", "/v2/");

        Assert.AreEqual(H.ExpectedError("401 Unauthorized", "UNAUTHORIZED", Unauthorized, Challenge()), response);
    }

    [TestMethod]
    [DataRow("POST", "/v2/library/alpine/blobs/uploads/", "repository:library/alpine:pull,push")]
    [DataRow("PUT", "/v2/library/alpine/manifests/latest", "repository:library/alpine:pull,push")]
    [DataRow("GET", "/v2/library/alpine/blobs/uploads/00000000-0000-4000-8000-000000000001", "repository:library/alpine:pull,push")]
    [DataRow("DELETE", "/v2/library/alpine/blobs/uploads/00000000-0000-4000-8000-000000000001", "repository:library/alpine:pull,push")]
    [DataRow("DELETE", "/v2/library/alpine/manifests/latest", "repository:library/alpine:delete")]
    [DataRow("DELETE", "/v2/library/alpine/blobs/sha256:0000000000000000000000000000000000000000000000000000000000000000", "repository:library/alpine:delete")]
    public async Task CheckAsync_WriteWithoutAToken_Returns401WithTheScopeItNeeds(string method, string target, string scope)
    {
        var harness = Registry();

        var response = await harness.SendAsync(method, target, "Content-Length: 0");

        Assert.AreEqual(H.ExpectedError("401 Unauthorized", "UNAUTHORIZED", Unauthorized, Challenge(scope)), response);
    }

    [TestMethod]
    public async Task CheckAsync_PullWithAPullScopedToken_IsServed()
    {
        var harness = Registry();
        var manifest = "{\"schemaVersion\":2}";
        harness.WriteBlob("library/alpine", manifest);
        harness.WriteIndex("library/alpine", H.Descriptor(H.ManifestMediaType, manifest, "latest"));
        var token = await harness.FetchTokenAsync("repository:library/alpine:pull");

        var response = await harness.SendAsync("GET", "/v2/library/alpine/manifests/latest", Bearer(token));

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, manifest);
    }

    [TestMethod]
    public async Task CheckAsync_V2WithAnyToken_Returns200()
    {
        var harness = Registry();
        var token = await harness.FetchTokenAsync();

        var response = await harness.SendAsync("GET", "/v2/", Bearer(token));

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task CheckAsync_PushWithAPullOnlyToken_Returns403DeniedWithInsufficientScope()
    {
        var harness = Registry();
        var token = await harness.FetchTokenAsync("repository:library/alpine:pull");

        var response = await harness.SendAsync("POST", "/v2/library/alpine/blobs/uploads/", Bearer(token), "Content-Length: 0");

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", Denied, Challenge("repository:library/alpine:pull,push", "insufficient_scope")), response);
    }

    [TestMethod]
    public async Task CheckAsync_PullWithAnotherRepositorysToken_Returns403DeniedWithInsufficientScope()
    {
        var harness = Registry();
        var token = await harness.FetchTokenAsync("repository:library/busybox:pull");

        var response = await harness.SendAsync("GET", "/v2/library/alpine/tags/list", Bearer(token));

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", Denied, Challenge("repository:library/alpine:pull", "insufficient_scope")), response);
    }

    [TestMethod]
    public async Task CheckAsync_ExpiredToken_Returns401InvalidToken()
    {
        var harness = Registry();
        var token = await harness.FetchTokenAsync("repository:library/alpine:pull");
        harness.TimeProvider.UtcNow += OciTokenBook.Lifetime;

        var response = await harness.SendAsync("GET", "/v2/library/alpine/tags/list", Bearer(token));

        var answer = H.ExpectedError("401 Unauthorized", "UNAUTHORIZED", Unauthorized, Challenge("repository:library/alpine:pull", "invalid_token"));
        Assert.AreEqual(answer.Replace(H.NowHttpDate, "Thu, 05 Nov 2026 12:05:00 GMT", StringComparison.Ordinal), response);
    }

    [TestMethod]
    public async Task CheckAsync_TokenJustBeforeItsExpiry_IsServed()
    {
        var harness = Registry();
        var token = await harness.FetchTokenAsync("repository:library/alpine:pull");
        harness.TimeProvider.UtcNow += OciTokenBook.Lifetime - TimeSpan.FromSeconds(1);

        var response = await harness.SendAsync("GET", "/v2/library/alpine/tags/list", Bearer(token));

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    [DataRow("Authorization: Bearer not-a-token-this-server-issued")]
    [DataRow("Authorization: Basic dGVzdGVyOnNlY3JldA==")]
    public async Task CheckAsync_UnknownTokenOrAnotherScheme_Returns401InvalidToken(string authorization)
    {
        var harness = Registry();

        var response = await harness.SendAsync("GET", "/v2/library/alpine/tags/list", authorization);

        Assert.AreEqual(H.ExpectedError("401 Unauthorized", "UNAUTHORIZED", Unauthorized, Challenge("repository:library/alpine:pull", "invalid_token")), response);
    }

    [TestMethod]
    public async Task CheckAsync_AnonymousPullUnderAllowAnonymous_IsServedAsTheAnonymousCaller()
    {
        var harness = Registry();
        harness.Policy.AllowsAnonymous = true;

        var tags = await harness.SendAsync("GET", "/v2/library/alpine/tags/list");
        var root = await harness.SendAsync("GET", "/v2/");

        StringAssert.StartsWith(tags, "HTTP/1.1 200 OK\r\n");
        StringAssert.StartsWith(root, "HTTP/1.1 200 OK\r\n");
        Assert.IsNull(harness.Policy.Logins[0].UserName);
    }

    [TestMethod]
    public async Task CheckAsync_AnonymousPushUnderAllowAnonymous_Returns401WithTheChallenge()
    {
        var harness = Registry();
        harness.Policy.AllowsAnonymous = true;

        var response = await harness.SendAsync("POST", "/v2/library/alpine/blobs/uploads/", "Content-Length: 0");

        Assert.AreEqual(H.ExpectedError("401 Unauthorized", "UNAUTHORIZED", Unauthorized, Challenge("repository:library/alpine:pull,push")), response);
    }

    [TestMethod]
    public async Task CheckAsync_BadTokenUnderAllowAnonymous_IsNotServedAsAnonymous()
    {
        var harness = Registry();
        harness.Policy.AllowsAnonymous = true;

        var response = await harness.SendAsync("GET", "/v2/library/alpine/tags/list", Bearer("unknown"));

        StringAssert.StartsWith(response, "HTTP/1.1 401 Unauthorized\r\n");
        StringAssert.Contains(response, "error=\"invalid_token\"");
    }

    [TestMethod]
    public async Task CheckAsync_WriteWhileUploadsAreOff_Returns403DeniedBeforeTheToken()
    {
        var harness = new H { SendsTokens = false };

        var response = await harness.SendAsync("POST", "/v2/library/alpine/blobs/uploads/", "Content-Length: 0");

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", Denied), response);
    }

    [TestMethod]
    public async Task CheckAsync_NameOffTheGrammar_Returns400BeforeTheToken()
    {
        var harness = Registry();

        var response = await harness.SendAsync("GET", "/v2/Library/tags/list");

        Assert.AreEqual(H.ExpectedError("400 Bad Request", "NAME_INVALID", "invalid repository name"), response);
    }

    [TestMethod]
    public async Task CheckAsync_TokenOfAnAccount_ServesTheRepositoryAsThatAccount()
    {
        var harness = Registry();
        var token = await harness.FetchTokenAsync("repository:library/alpine:pull,push");

        var response = await harness.SendAsync("POST", "/v2/library/alpine/blobs/uploads/", Bearer(token), "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 202 Accepted\r\n");
    }

    [TestMethod]
    [DataRow("repository:app:pull,push", "202 Accepted")]
    [DataRow("repository:app:pull,push repository:source:pull", "201 Created")]
    public async Task CheckAsync_Mount_ReadsTheSourceOnlyWhereTheTokenGrantsPull(string scope, string status)
    {
        var harness = Registry();
        harness.CreateRepository("source");
        var digest = harness.WriteBlob("source", Encoding.UTF8.GetBytes("layer"));
        var token = await harness.FetchTokenAsync(scope);

        var response = await harness.SendAsync("POST", $"/v2/app/blobs/uploads/?mount={digest}&from=source", Bearer(token), "Content-Length: 0");

        StringAssert.StartsWith(response, $"HTTP/1.1 {status}\r\n");
    }

    [TestMethod]
    public async Task CheckAsync_BearerTokenOverPlainTextRefusingIt_Returns403DeniedWithThePlaintextMessage()
    {
        var harness = Registry();
        var token = await harness.FetchTokenAsync("repository:library/alpine:pull");
        harness.Policy.AllowsPlaintext = false;

        var response = await harness.SendAsync("GET", "/v2/library/alpine/tags/list", Bearer(token));

        Assert.AreEqual(H.ExpectedError("403 Forbidden", "DENIED", "credentials over plain text are refused"), response);
    }

    [TestMethod]
    public async Task CheckAsync_BearerTokenOverPlainTextAllowed_IsServed()
    {
        var harness = Registry();
        var token = await harness.FetchTokenAsync("repository:library/alpine:pull");

        var response = await harness.SendAsync("GET", "/v2/library/alpine/tags/list", Bearer(token));

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task CheckAsync_BearerTokenOverTlsWhilePlaintextIsRefused_IsServed()
    {
        var harness = Registry();
        var token = await harness.FetchTokenAsync("repository:library/alpine:pull");
        harness.Policy.AllowsPlaintext = false;
        harness.TlsSession = InMemoryConnection.DefaultUpgradeTlsSession;

        var response = await harness.SendAsync("GET", "/v2/library/alpine/tags/list", Bearer(token));

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task CheckAsync_BasicOverPlainTextRefusingIt_Returns401InvalidTokenNotThePlaintextRefusal()
    {
        var harness = Registry();
        harness.Policy.AllowsPlaintext = false;

        var response = await harness.SendAsync("GET", "/v2/library/alpine/tags/list", "Authorization: Basic dGVzdGVyOnNlY3JldA==");

        StringAssert.StartsWith(response, "HTTP/1.1 401 Unauthorized\r\n");
    }
}