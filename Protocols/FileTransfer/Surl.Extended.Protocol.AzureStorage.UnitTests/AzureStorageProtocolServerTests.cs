using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Azure Storage server's Get Blob and Get Blob Properties, whose bytes are written from the
/// Azure Storage REST API pages "Get Blob", "Get Blob Properties", "Status and error codes",
/// "Common REST API error codes", "Blob Storage error codes" and "Authorize with Microsoft Entra
/// ID" (ADR-0109).
/// </summary>
[TestClass]
public sealed class AzureStorageProtocolServerTests
{
    // MD5("hello world"), the ETag a file with no metadata entry answers, and its base64.
    private const string HelloWorldETag = "\"5eb63bbbe01eeed093cb22bb8f5acdc3\"";
    private const string HelloWorldContentMd5 = "XrY7u+Ae7tCTyyK7j1rNww==";

    private const string BlobFields =
        $"ETag: {HelloWorldETag}\r\nLast-Modified: {AzureServerHarness.NowHttpDate}\r\nAccept-Ranges: bytes\r\n"
        + "x-ms-blob-type: BlockBlob\r\nx-ms-lease-status: unlocked\r\nx-ms-lease-state: available\r\nx-ms-server-encrypted: false\r\n";

    private const string BearerChallengeField =
        "WWW-Authenticate: Bearer authorization_uri=https://login.microsoftonline.com/common/oauth2/authorize resource_id=https://storage.azure.com\r\n";

    private const string AuthenticationMessage = "Server failed to authenticate the request. Please refer to the information in the www-authenticate header.";

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var harness = new AzureServerHarness();

        Assert.ThrowsExactly<ArgumentNullException>(() => new AzureStorageProtocolServer(null!, harness.Index, harness.Policy, harness.TimeProvider));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AzureStorageProtocolServer(harness.Store, null!, harness.Policy, harness.TimeProvider));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AzureStorageProtocolServer(harness.Store, harness.Index, null!, harness.TimeProvider));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AzureStorageProtocolServer(harness.Store, harness.Index, harness.Policy, null!));
    }

    [TestMethod]
    public void Schemes_AreTheBlobAndDataLakeSchemes()
    {
        var server = new AzureServerHarness().Server;

        CollectionAssert.AreEqual(new[] { "az", "abfs", "abfss" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        var harness = new AzureServerHarness();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(null!, harness.Context()));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public async Task ServeAsync_GetBlob_ReturnsTheBytesAndHeaders()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{AzureServerHarness.CommonFields(response)}"
                + $"Content-Length: 11\r\nContent-Type: text/plain\r\nContent-MD5: {HelloWorldContentMd5}\r\n{BlobFields}"
                + "Connection: close\r\n\r\nhello world",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_GetBlobProperties_ReturnsTheHeadersOnly()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("HEAD", "/photos/a.txt");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{AzureServerHarness.CommonFields(response)}"
                + $"Content-Length: 11\r\nContent-Type: text/plain\r\nContent-MD5: {HelloWorldContentMd5}\r\n{BlobFields}"
                + "Connection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_GetBlobInAFolderWithAnEscapedName_ReturnsTheBytes()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.CreateFolder("photos/2026");
        harness.WriteFile("photos/2026/a b.txt", "nested");

        var response = await harness.SendWithBearerAsync("GET", "/photos/2026/a%20b.txt?timeout=30");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "\r\n\r\nnested");
    }

    [TestMethod]
    public async Task ServeAsync_GetBlobWithStoredMetadata_SendsEveryStoredFieldAndMetadata()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");
        await harness.Index.StoreAsync(
            "photos/a.txt",
            new ObjectMetadata
            {
                ContentType = "text/plain",
                ContentEncoding = "gzip",
                ContentDisposition = "attachment",
                CacheControl = "no-cache",
                ContentLanguage = "en",
                UserMetadata = new Dictionary<string, string> { ["colour"] = "blue", ["owner"] = "ann" },
                ETag = "\"stored\"",
            },
            CancellationToken.None);

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.Contains(
            response,
            "Content-Length: 11\r\nContent-Type: text/plain\r\nETag: \"stored\"\r\n"
                + $"Last-Modified: {AzureServerHarness.NowHttpDate}\r\nAccept-Ranges: bytes\r\n"
                + "x-ms-blob-type: BlockBlob\r\nx-ms-lease-status: unlocked\r\nx-ms-lease-state: available\r\nx-ms-server-encrypted: false\r\n"
                + "Content-Encoding: gzip\r\nContent-Language: en\r\nContent-Disposition: attachment\r\nCache-Control: no-cache\r\n"
                + "x-ms-meta-colour: blue\r\nx-ms-meta-owner: ann\r\nConnection: close\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_MissingBlob_Is404BlobNotFound()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.jpg");

        Assert.AreEqual(AzureServerHarness.ExpectedError(response, "404 Not Found", "BlobNotFound", "The specified blob does not exist."), response);
    }

    [TestMethod]
    [DataRow("/photos/2026", DisplayName = "a folder")]
    [DataRow("/photos/a%00b", DisplayName = "a name the store refuses")]
    public async Task ServeAsync_FolderOrUnholdableNameAsABlob_Is404BlobNotFound(string target)
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAsync("GET", target);

        StringAssert.StartsWith(response, "HTTP/1.1 404 Not Found\r\n");
        StringAssert.Contains(response, "<Code>BlobNotFound</Code>");
    }

    [TestMethod]
    [DataRow("/missing/a.jpg", DisplayName = "no such folder")]
    [DataRow("/top.txt/a.jpg", DisplayName = "a top-level file")]
    [DataRow("/.surl/objects", DisplayName = "the service-state folder")]
    [DataRow("/..%2F/a.jpg", DisplayName = "a container the store refuses")]
    public async Task ServeAsync_MissingContainer_Is404ContainerNotFound(string target)
    {
        var harness = new AzureServerHarness();
        harness.WriteFile("top.txt", "a file");
        harness.CreateFolder(".surl");
        harness.CreateFolder(".surl/objects");

        var response = await harness.SendWithBearerAsync("GET", target);

        Assert.AreEqual(AzureServerHarness.ExpectedError(response, "404 Not Found", "ContainerNotFound", "The specified container does not exist."), response);
    }

    [TestMethod]
    public async Task ServeAsync_HeadOfAMissingBlob_Is404WithTheErrorCodeAndNoBody()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("HEAD", "/photos/a.jpg");

        Assert.AreEqual(
            $"HTTP/1.1 404 Not Found\r\n{AzureServerHarness.CommonFields(response)}x-ms-error-code: BlobNotFound\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_BlobNameOver1024Characters_Is400InvalidResourceName()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("GET", "/photos/" + new string('a', 1025));

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "400 Bad Request", "InvalidResourceName", "The specified resource name contains invalid characters."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_NoCredentialsWithAnonymousOff_Is401NoAuthenticationInformationWithTheChallenge()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/photos/a.txt", "x-ms-version: 2025-01-05");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "401 Unauthorized", "NoAuthenticationInformation", AuthenticationMessage, extraFields: BearerChallengeField),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_WrongBearerToken_Is401InvalidAuthenticationInfoWithTheChallenge()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/photos/a.txt", "x-ms-version: 2025-01-05", "Authorization: Bearer wrong-token");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "401 Unauthorized", "InvalidAuthenticationInfo", AuthenticationMessage, extraFields: BearerChallengeField),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_WrongBearerTokenUnderAllowAnonymous_Is401InvalidAuthenticationInfoWithTheChallenge()
    {
        var harness = new AzureServerHarness(allowsAnonymous: true);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/photos/a.txt", "x-ms-version: 2025-01-05", "Authorization: Bearer wrong");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "401 Unauthorized", "InvalidAuthenticationInfo", AuthenticationMessage, extraFields: BearerChallengeField),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_GoodBearerTokenUnderAllowAnonymous_IsServedAsTheAccount()
    {
        var harness = new AzureServerHarness(allowsAnonymous: true, accountName: "alice", exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("PUT", "/photos/a.txt", "x-ms-blob-type: BlockBlob", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual(string.Empty, harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    [DataRow("Basic YWxpY2U6c2VjcmV0", DisplayName = "another scheme")]
    [DataRow("Bearer  ", DisplayName = "a bearer with no token")]
    [DataRow("Digest username=\"alice\"", DisplayName = "Digest")]
    public async Task ServeAsync_AuthorizationThatIsNoBearerToken_Is400InvalidAuthenticationInfo(string authorization)
    {
        var harness = new AzureServerHarness();

        var response = await harness.SendAsync("GET", "/photos/a.txt", "x-ms-version: 2025-01-05", $"Authorization: {authorization}");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "400 Bad Request", "InvalidAuthenticationInfo",
                "Authentication information is not given in the correct format. Check the value of Authorization header."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_BearerWithAVersionBefore20171109_Is403AuthenticationFailed()
    {
        var harness = new AzureServerHarness();

        var response = await harness.SendAsync("GET", "/photos/a.txt", "x-ms-version: 2017-07-29", $"Authorization: Bearer {UnitTestBearerAuthenticationPolicy.Token}");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "403 Forbidden", "AuthenticationFailed",
                "Server failed to authenticate the request. Make sure the value of Authorization header is formed correctly including the signature.",
                "<AuthenticationErrorDetail>Bearer authorization needs x-ms-version 2017-11-09 or later.</AuthenticationErrorDetail>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_BearerWithoutAVersion_Is400MissingRequiredHeader()
    {
        var harness = new AzureServerHarness();

        var response = await harness.SendAsync("GET", "/photos/a.txt", $"Authorization: Bearer {UnitTestBearerAuthenticationPolicy.Token}");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "400 Bad Request", "MissingRequiredHeader", "An HTTP header that's mandatory for this request is not specified.",
                "<HeaderName>x-ms-version</HeaderName>"),
            response);
    }

    [TestMethod]
    [DataRow("x-ms-version", "2025-13-45", DisplayName = "a version that is no date")]
    [DataRow("x-ms-date", "yesterday <noon>", DisplayName = "a date that is no RFC 1123 time")]
    public async Task ServeAsync_MalformedVersionOrDate_Is400InvalidHeaderValue(string name, string value)
    {
        var harness = new AzureServerHarness();

        var response = await harness.SendAsync("GET", "/photos/a.txt", $"{name}: {value}");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "400 Bad Request", "InvalidHeaderValue", "The value provided for one of the HTTP headers was not in the correct format.",
                $"<HeaderName>{name}</HeaderName><HeaderValue>{value.Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)}</HeaderValue>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_WellFormedDate_IsServed()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", $"x-ms-date: {AzureServerHarness.NowHttpDate}");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousReadWithoutAVersion_IsServedUnderAllowAnonymous()
    {
        var harness = new AzureServerHarness(allowsAnonymous: true);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/photos/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "hello world");
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousWriteUnderAllowAnonymous_Is401NoAuthenticationInformation()
    {
        var harness = new AzureServerHarness(allowsAnonymous: true);

        var response = await harness.SendAsync("PUT", "/photos/a.txt", "Content-Length: 0");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "401 Unauthorized", "NoAuthenticationInformation", AuthenticationMessage, extraFields: BearerChallengeField),
            response);
        Assert.IsTrue(harness.Policy.Requests.Single().IsWrite);
    }

    [TestMethod]
    public async Task ServeAsync_BearerAsANamedAccount_ReadsAsThatAccount()
    {
        var harness = new AzureServerHarness(accountName: "alice");
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.EndsWith(response, "hello world");
        Assert.IsFalse(harness.Policy.Requests.Single().IsWrite);
    }

    [TestMethod]
    public async Task ServeAsync_EachConnection_StartsAnAuthenticationSessionWithItsTlsSession()
    {
        var harness = new AzureServerHarness();

        await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        Assert.IsNull(harness.Policy.StartedConnections.Single());
    }

    [TestMethod]
    [DataRow("POST", DisplayName = "POST")]
    [DataRow("PATCH", DisplayName = "PATCH")]
    public async Task ServeAsync_MethodTheBlobDoesNotTake_Is405UnsupportedHttpVerb(string method)
    {
        var harness = new AzureServerHarness();

        var response = await harness.SendWithBearerAsync(method, "/photos/a.txt", "Content-Length: 0");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "405 Method Not Allowed", "UnsupportedHttpVerb", "The resource doesn't support the specified HTTP verb."),
            response);
        Assert.Contains($"{method} /photos/a.txt is no operation the Azure Blob server serves; answered UnsupportedHttpVerb.", harness.Log.Notes);
    }

    [TestMethod]
    [DataRow("/photos?restype=container&comp=metadata", "restype", "container", DisplayName = "Get Container Metadata")]
    [DataRow("/photos?restype=share", "restype", "share", DisplayName = "a restype other than container")]
    [DataRow("/photos?restype=container&comp=lis", "restype", "container", DisplayName = "a List Blobs with another comp")]
    [DataRow("/photos/a.txt?comp=lease", "comp", "lease", DisplayName = "a lease")]
    [DataRow("/photos/a.txt?snapshot=2026-11-05T12%3A00%3A00Z", "snapshot", "2026-11-05T12:00:00Z", DisplayName = "a snapshot")]
    [DataRow("/photos/a.txt?VersionId=v1", "versionid", "v1", DisplayName = "a version")]
    public async Task ServeAsync_UnservedQueryParameter_Is400InvalidQueryParameterValue(string target, string name, string value)
    {
        var harness = new AzureServerHarness();

        var response = await harness.SendWithBearerAsync("GET", target);

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "400 Bad Request", "InvalidQueryParameterValue", "An invalid value was specified for one of the query parameters in the request URI.",
                $"<QueryParameterName>{name}</QueryParameterName><QueryParameterValue>{value}</QueryParameterValue><Reason>This operation is not served by this server.</Reason>"),
            response);
    }

    [TestMethod]
    [DataRow("/", DisplayName = "the account")]
    [DataRow("/photos", DisplayName = "a container")]
    [DataRow("/photos/", DisplayName = "a container with its slash")]
    public async Task ServeAsync_PathThatNamesNoBlob_Is400InvalidUri(string target)
    {
        var harness = new AzureServerHarness();

        var response = await harness.SendWithBearerAsync("GET", target);

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "400 Bad Request", "InvalidUri", "The requested URI does not represent any resource on the server."),
            response);
    }

    [TestMethod]
    [DataRow("x-ms-lease-id: 0f6c2a0e-1111-2222-3333-444455556666", "x-ms-lease-id", DisplayName = "a lease")]
    [DataRow("x-ms-encryption-key: a2V5", "x-ms-encryption-key", DisplayName = "a customer key")]
    public async Task ServeAsync_HeaderForAnUnservedFeature_Is400UnsupportedHeader(string field, string name)
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", field);

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "400 Bad Request", "UnsupportedHeader", "One of the HTTP headers specified in the request is not supported.",
                $"<HeaderName>{name}</HeaderName>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_ClientRequestId_IsEchoed()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", "x-ms-client-request-id: client-7");

        StringAssert.Contains(response, $"{AzureServerHarness.CommonFields(response)}x-ms-client-request-id: client-7\r\nContent-Length: 11\r\n");
    }

    [TestMethod]
    [DataRow("client 7", DisplayName = "a space, which is not visible")]
    [DataRow("café", DisplayName = "a byte outside ASCII")]
    [DataRow(null, DisplayName = "1025 bytes")]
    public async Task ServeAsync_ClientRequestIdThatIsNotEchoable_IsLeftOut(string? value)
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt", $"x-ms-client-request-id: {value ?? new string('a', 1025)}");

        Assert.DoesNotContain("x-ms-client-request-id", response);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_ConnectionPastALimit_Is503ServerBusyThenClosed()
    {
        var harness = new AzureServerHarness();
        var connection = new InMemoryConnection([]);

        await harness.Server.WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnections, CancellationToken.None);

        var response = Encoding.UTF8.GetString(connection.WrittenBytes);
        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "503 Service Unavailable", "ServerBusy", "The server is currently unable to receive requests. Please retry your request."),
            response);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullConnection_Throws()
    {
        var harness = new AzureServerHarness();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, CancellationToken.None).AsTask());
    }
}
