using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The registry's <c>/v2/</c>, its refusals of other requests, and the connection-limit refusal,
/// whose bytes are written from the OCI Distribution Specification v1.1.0's "Determining support",
/// "Endpoints" and "Error Codes" (ADR-0115, decisions 1, 5.1 and 7).
/// </summary>
[TestClass]
public sealed class OciRegistryProtocolServerTests
{
    [TestMethod]
    public async Task Constructor_WithoutASessionIdSource_GivesEachUploadSessionAVersion4Uuid()
    {
        var harness = OciRegistryServerHarness.AllowingUploads();
        var server = new OciRegistryProtocolServer(harness.Store, harness.Policy, harness.TimeProvider);
        var tokenConnection = new InMemoryConnection([Encoding.Latin1.GetBytes($"GET /token?scope=repository:app:push HTTP/1.1\r\nHost: registry.test\r\n{UnitTestAuthenticationPolicy.BasicAuthorization}\r\nConnection: close\r\n\r\n")]);
        await server.ServeAsync(tokenConnection, harness.Context());
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes($"POST /v2/app/blobs/uploads/ HTTP/1.1\r\nHost: registry.test\r\nAuthorization: Bearer {CountingIdentifierRandom.TokenOf(1)}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")]);

        await server.ServeAsync(connection, harness.Context());

        var response = Encoding.UTF8.GetString(connection.WrittenBytes);
        var id = response.Split("Docker-Upload-UUID: ")[1].Split("\r\n")[0];
        Assert.AreEqual('4', Guid.Parse(id).ToString()[14]);
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var harness = new OciRegistryServerHarness();

        Assert.ThrowsExactly<ArgumentNullException>(() => new OciRegistryProtocolServer(null!, harness.Policy, harness.TimeProvider));
        Assert.ThrowsExactly<ArgumentNullException>(() => new OciRegistryProtocolServer(harness.Store, null!, harness.TimeProvider));
        Assert.ThrowsExactly<ArgumentNullException>(() => new OciRegistryProtocolServer(harness.Store, harness.Policy, null!));
    }

    [TestMethod]
    public void Schemes_AreOciOnly()
    {
        var server = new OciRegistryServerHarness().Server;

        CollectionAssert.AreEqual(new[] { "oci" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        var harness = new OciRegistryServerHarness();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(null!, harness.Context()));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public async Task ServeAsync_GetV2_Returns200WithAnEmptyObject()
    {
        var harness = new OciRegistryServerHarness();

        var response = await harness.SendAsync("GET", "/v2/");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{OciRegistryServerHarness.CommonFields}Content-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{{}}",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_PostV2_Returns405UnsupportedWithAllow()
    {
        var harness = new OciRegistryServerHarness();

        var response = await harness.SendAsync("POST", "/v2/", "Content-Length: 0");

        Assert.AreEqual(OciRegistryServerHarness.ExpectedError("405 Method Not Allowed", "UNSUPPORTED", "the operation is unsupported", "Allow: GET\r\n"), response);
    }

    [TestMethod]
    public async Task ServeAsync_PostManifest_Returns405WithEveryManifestMethodAllowed()
    {
        var harness = new OciRegistryServerHarness();

        var response = await harness.SendAsync("POST", "/v2/library/alpine/manifests/latest", "Content-Length: 0");

        Assert.AreEqual(OciRegistryServerHarness.ExpectedError("405 Method Not Allowed", "UNSUPPORTED", "the operation is unsupported", "Allow: GET, HEAD, PUT, DELETE\r\n"), response);
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/v2")]
    [DataRow("/v2/library/alpine/catalog")]
    public async Task ServeAsync_PathNoEndpointMatches_Returns404Unsupported(string target)
    {
        var harness = new OciRegistryServerHarness();

        var response = await harness.SendAsync("GET", target);

        Assert.AreEqual(OciRegistryServerHarness.ExpectedError("404 Not Found", "UNSUPPORTED", "the operation is unsupported"), response);
    }

    [TestMethod]
    [DataRow("/v2/Library/manifests/latest")]
    [DataRow("/v2/a__-b/manifests/latest")]
    [DataRow("/v2//manifests/latest")]
    public async Task ServeAsync_NameOffTheGrammar_Returns400NameInvalid(string target)
    {
        var harness = new OciRegistryServerHarness();

        var response = await harness.SendAsync("GET", target);

        Assert.AreEqual(OciRegistryServerHarness.ExpectedError("400 Bad Request", "NAME_INVALID", "invalid repository name"), response);
    }

    [TestMethod]
    public async Task ServeAsync_NameLongerThan255Bytes_Returns400NameInvalid()
    {
        var harness = new OciRegistryServerHarness();

        var response = await harness.SendAsync("GET", $"/v2/{new string('a', 256)}/tags/list");

        Assert.AreEqual(OciRegistryServerHarness.ExpectedError("400 Bad Request", "NAME_INVALID", "invalid repository name"), response);
    }

    [TestMethod]
    [DataRow("/v2/missing/manifests/latest")]
    [DataRow("/v2/con/tags/list")]
    public async Task ServeAsync_NoSuchRepository_Returns404NameUnknown(string target)
    {
        var harness = new OciRegistryServerHarness();
        harness.WriteFile("plain/readme.txt", "not a repository");

        var response = await harness.SendAsync("GET", target);

        Assert.AreEqual(OciRegistryServerHarness.ExpectedError("404 Not Found", "NAME_UNKNOWN", "repository name not known to registry"), response);
    }

    [TestMethod]
    public async Task ServeAsync_FolderWithoutOciLayout_Returns404NameUnknown()
    {
        var harness = new OciRegistryServerHarness();
        harness.WriteFile("plain/readme.txt", "not a repository");

        var response = await harness.SendAsync("GET", "/v2/plain/tags/list");

        Assert.AreEqual(OciRegistryServerHarness.ExpectedError("404 Not Found", "NAME_UNKNOWN", "repository name not known to registry"), response);
    }

    [TestMethod]
    public async Task ServeAsync_GetWithAnAnnouncedBody_AnswersAndClosesWithoutReadingIt()
    {
        var harness = new OciRegistryServerHarness();

        var response = await harness.SendRawAsync("GET /v2/ HTTP/1.1\r\nHost: registry.test\r\nContent-Length: 3\r\n\r\nabc");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(response, "Connection: close\r\n");
    }

    [TestMethod]
    public async Task WriteRefusalAsync_WritesTooManyRequestsWithTheErrorBodyAndCloses()
    {
        var harness = new OciRegistryServerHarness();
        var connection = new InMemoryConnection([]);

        await harness.Server.WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnections, CancellationToken.None);

        var body = OciRegistryServerHarness.ErrorBody("TOOMANYREQUESTS", "too many requests");
        Assert.AreEqual(
            $"HTTP/1.1 429 Too Many Requests\r\n{OciRegistryServerHarness.CommonFields}Content-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}",
            Encoding.UTF8.GetString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullConnection_Throws()
    {
        var harness = new OciRegistryServerHarness();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await harness.Server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, CancellationToken.None));
    }
}
