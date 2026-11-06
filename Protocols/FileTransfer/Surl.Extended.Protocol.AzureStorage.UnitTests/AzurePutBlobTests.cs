using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Azure server's Put Blob, whose requests are written from the REST API's "Put Blob" page and
/// its "Sample request and response" (ADR-0109, decisions 4, 5.6 and 7).
/// </summary>
[TestClass]
public sealed class AzurePutBlobTests
{
    // MD5("hello world"), as the index's quoted hexadecimal ETag and as base64.
    private const string HelloWorldETag = "\"5eb63bbbe01eeed093cb22bb8f5acdc3\"";
    private const string HelloWorldMd5 = "XrY7u+Ae7tCTyyK7j1rNww==";

    // The page's sample request's fields, the Shared Key Authorization left for the harness's bearer.
    private static readonly string[] SampleFields =
    [
        $"x-ms-date: {AzureServerHarness.NowHttpDate}",
        "Content-Type: text/plain; charset=UTF-8",
        "x-ms-blob-content-disposition: attachment; filename=\"fname.ext\"",
        "x-ms-blob-type: BlockBlob",
        "x-ms-meta-m1: v1",
        "x-ms-meta-m2: v2",
    ];

    [TestMethod]
    public async Task ServeAsync_PutBlob_StoresTheBodyAndAnswers201()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/myblockblob", "hello world", SampleFields);

        Assert.AreEqual(
            "HTTP/1.1 201 Created\r\n" + AzureServerHarness.CommonFields(response)
                + $"ETag: {HelloWorldETag}\r\nLast-Modified: {AzureServerHarness.NowHttpDate}\r\nContent-MD5: {HelloWorldMd5}\r\n"
                + "x-ms-request-server-encrypted: false\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/myblockblob"));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobThenGetBlob_ReadsTheBlobBack()
    {
        var harness = Writable();
        await harness.SendWithBearerAndBodyAsync("PUT", "/photos/2026/a.txt", "hello world", "x-ms-blob-type: BlockBlob");

        var response = await harness.SendWithBearerAsync("GET", "/photos/2026/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(response, $"Content-MD5: {HelloWorldMd5}\r\nETag: {HelloWorldETag}\r\n");
        StringAssert.EndsWith(response, "\r\n\r\nhello world");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobMetadata_IsReadBackByGetBlobProperties()
    {
        var harness = Writable();
        await harness.SendWithBearerAndBodyAsync(
            "PUT",
            "/photos/myblockblob",
            "hello world",
            [.. SampleFields, "x-ms-blob-content-type: text/csv", "x-ms-blob-content-encoding: gzip", "x-ms-blob-content-language: en-GB", "x-ms-blob-cache-control: no-cache", "X-MS-META-Upper: v3"]);

        var response = await harness.SendWithBearerAsync("HEAD", "/photos/myblockblob");

        StringAssert.Contains(response, "Content-Length: 11\r\nContent-Type: text/csv\r\n");
        StringAssert.Contains(
            response,
            "Content-Encoding: gzip\r\nContent-Language: en-GB\r\nContent-Disposition: attachment; filename=\"fname.ext\"\r\nCache-Control: no-cache\r\n"
                + "x-ms-meta-m1: v1\r\nx-ms-meta-m2: v2\r\nx-ms-meta-upper: v3\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWithBlobContentMd5_StoresItUncheckedAndGetBlobPropertiesAnswersIt()
    {
        var harness = Writable();

        // Not the body's MD5: the page says the value is stored without being validated.
        var put = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob", "x-ms-blob-content-md5: AAECAwQFBgcICQoLDA0ODw==");
        var head = await harness.SendWithBearerAsync("HEAD", "/photos/a.txt");
        var get = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.Contains(put, $"Content-MD5: {HelloWorldMd5}\r\n");
        StringAssert.Contains(head, $"Content-MD5: AAECAwQFBgcICQoLDA0ODw==\r\nETag: {HelloWorldETag}\r\n");
        StringAssert.Contains(get, $"Content-MD5: AAECAwQFBgcICQoLDA0ODw==\r\nETag: {HelloWorldETag}\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWithContentTypeOnly_KeepsTheContentType()
    {
        var harness = Writable();
        await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob", "Content-Type: text/plain");

        var response = await harness.SendWithBearerAsync("HEAD", "/photos/a.txt");

        StringAssert.Contains(response, "Content-Type: text/plain\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_ZeroBytePutBlob_StoresAnEmptyBlob()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/empty.txt", string.Empty, "x-ms-blob-type: BlockBlob");

        StringAssert.Contains(response, "ETag: \"d41d8cd98f00b204e9800998ecf8427e\"\r\n");
        Assert.AreEqual(string.Empty, harness.ReadFile("photos/empty.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobInAnExistingFolder_IsStoredThere()
    {
        var harness = Writable();
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/2026/a.txt", "hello world", "x-ms-blob-type: BlockBlob");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/2026/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobOverAnExistingBlob_ReplacesIt()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "old bytes that are longer");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    [DataRow("If-Match: \"0123\"", DisplayName = "If-Match failing")]
    [DataRow("If-Unmodified-Since: Wed, 04 Nov 2026 12:00:00 GMT", DisplayName = "If-Unmodified-Since failing")]
    [DataRow("If-None-Match: \"5eb63bbbe01eeed093cb22bb8f5acdc3\"", DisplayName = "If-None-Match matching")]
    [DataRow("If-Modified-Since: Thu, 05 Nov 2026 12:00:00 GMT", DisplayName = "If-Modified-Since unmodified")]
    public async Task ServeAsync_PutBlobWithAFailingCondition_Is412ConditionNotMetAndNothingChanges(string condition)
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "new bytes", "x-ms-blob-type: BlockBlob", condition);

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "412 Precondition Failed", "ConditionNotMet", "The condition specified using HTTP conditional header(s) is not met."),
            response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWithHoldingConditions_IsStored()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAndBodyAsync(
            "PUT",
            "/photos/a.txt",
            "new bytes",
            "x-ms-blob-type: BlockBlob",
            $"If-Match: {HelloWorldETag}",
            $"If-Unmodified-Since: {AzureServerHarness.NowHttpDate}",
            "If-None-Match: \"0123\"",
            "If-Modified-Since: Wed, 04 Nov 2026 12:00:00 GMT");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual("new bytes", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobIfNoneMatchAnyOnAnExistingBlob_Is409BlobAlreadyExists()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "new bytes", "x-ms-blob-type: BlockBlob", "If-None-Match: *");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "409 Conflict", "BlobAlreadyExists", "The specified blob already exists."),
            response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobIfNoneMatchAnyOnAMissingBlob_IsStored()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob", "If-None-Match: *");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobIfMatchOnAMissingBlob_Is412ConditionNotMetAndNothingIsStored()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob", "If-Match: *");

        StringAssert.StartsWith(response, "HTTP/1.1 412 Precondition Failed\r\n");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWithUploadsOff_Is403AuthorizationPermissionMismatch()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "403 Forbidden", "AuthorizationPermissionMismatch", "This request is not authorized to perform this operation using this permission."),
            response);
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWithoutABlobType_Is400MissingRequiredHeader()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "400 Bad Request", "MissingRequiredHeader", "An HTTP header that's mandatory for this request is not specified.", "<HeaderName>x-ms-blob-type</HeaderName>"),
            response);
    }

    [TestMethod]
    [DataRow("AppendBlob", DisplayName = "an append blob")]
    [DataRow("PageBlob", DisplayName = "a page blob")]
    public async Task ServeAsync_PutBlobOfAnotherBlobType_Is400UnsupportedHeader(string blobType)
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", string.Empty, $"x-ms-blob-type: {blobType}");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "400 Bad Request", "UnsupportedHeader", "One of the HTTP headers specified in the request is not supported.", "<HeaderName>x-ms-blob-type</HeaderName>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobOfAnUnknownBlobType_Is400InvalidHeaderValue()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", string.Empty, "x-ms-blob-type: blockblob");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "400 Bad Request", "InvalidHeaderValue", "The value provided for one of the HTTP headers was not in the correct format.",
                "<HeaderName>x-ms-blob-type</HeaderName><HeaderValue>blockblob</HeaderValue>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWithACopySource_Is400UnsupportedHeader()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", string.Empty, "x-ms-blob-type: BlockBlob", "x-ms-copy-source: https://x/y");

        StringAssert.Contains(response, "<Code>UnsupportedHeader</Code>");
        StringAssert.Contains(response, "<HeaderName>x-ms-copy-source</HeaderName>");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWhoseContentMd5IsNotTheBodys_Is400Md5MismatchAndNothingIsStored()
    {
        var harness = Writable();
        var otherMd5 = Convert.ToBase64String(MD5.HashData(Encoding.UTF8.GetBytes("other")));

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob", $"Content-MD5: {otherMd5}");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "400 Bad Request", "Md5Mismatch", "The MD5 value specified in the request did not match the MD5 value calculated by the server."),
            response);
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWhoseContentMd5IsTheBodys_IsStored()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob", $"Content-MD5: {HelloWorldMd5}");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
    }

    [TestMethod]
    [DataRow("abc", DisplayName = "not base64")]
    [DataRow("aGVsbG8=", DisplayName = "base64 of 5 bytes")]
    public async Task ServeAsync_PutBlobWithAContentMd5ThatIsNo16Bytes_Is400InvalidMd5(string contentMd5)
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob", $"Content-MD5: {contentMd5}");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(
                response, "400 Bad Request", "InvalidMd5", "The MD5 value specified in the request is invalid. The MD5 value must be 128 bits and Base64-encoded."),
            response);
    }

    [TestMethod]
    [DataRow("x-ms-meta-1st: v", DisplayName = "a name starting with a digit")]
    [DataRow("x-ms-meta-a-b: v", DisplayName = "a name with a hyphen")]
    [DataRow("x-ms-meta-: v", DisplayName = "an empty name")]
    public async Task ServeAsync_PutBlobWithAMetadataNameThatIsNoIdentifier_Is400InvalidMetadata(string field)
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob", field);

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "400 Bad Request", "InvalidMetadata", "The metadata specified is invalid. It has characters that are not permitted."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWithAnUnderscoreMetadataName_IsStored()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob", "x-ms-meta-_note2: v");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWithMetadataPast8KiB_Is400MetadataTooLarge()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob", $"x-ms-meta-big: {new string('a', 8190)}");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "400 Bad Request", "MetadataTooLarge", "The size of the specified metadata exceeds the maximum size permitted."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWithoutAContentLength_Is411MissingContentLengthHeader()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAsync("PUT", "/photos/a.txt", "x-ms-blob-type: BlockBlob");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "411 Length Required", "MissingContentLengthHeader", "The Content-Length header was not specified."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWithAChunkedBody_IsStored()
    {
        var harness = Writable();

        var response = await harness.SendRawAsync(
            $"PUT /photos/a.txt HTTP/1.1\r\nHost: h\r\n{BearerFields()}x-ms-blob-type: BlockBlob\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\nb\r\nhello world\r\n0\r\n\r\n");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWithAnUnreadableBody_Is400InvalidInput()
    {
        var harness = Writable();

        var response = await harness.SendRawAsync($"PUT /photos/a.txt HTTP/1.1\r\nHost: h\r\n{BearerFields()}x-ms-blob-type: BlockBlob\r\nTransfer-Encoding: gzip\r\n\r\n");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>InvalidInput</Code><Message>One of the request inputs is not valid.\n");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWhoseBodyEndsEarly_Is400InvalidInputAndNothingIsStored()
    {
        var harness = Writable();

        var response = await harness.SendRawAsync($"PUT /photos/a.txt HTTP/1.1\r\nHost: h\r\n{BearerFields()}x-ms-blob-type: BlockBlob\r\nContent-Length: 20\r\n\r\nhello");

        StringAssert.Contains(response, "<Code>InvalidInput</Code>");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobAnnouncedPastMaxFilesize_Is413RequestBodyTooLarge()
    {
        var harness = Writable(AzureServerHarness.Writable with { MaxUploadBytes = 10 });

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob");

        StringAssert.StartsWith(response, "HTTP/1.1 413 Content Too Large\r\n");
        StringAssert.Contains(response, "<Code>RequestBodyTooLarge</Code><Message>The size of the request body exceeds the maximum size permitted.\n");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedPutBlobPastMaxFilesize_Is413RequestBodyTooLarge()
    {
        var harness = Writable(AzureServerHarness.Writable with { MaxUploadBytes = 10 });

        var response = await harness.SendRawAsync(
            $"PUT /photos/a.txt HTTP/1.1\r\nHost: h\r\n{BearerFields()}x-ms-blob-type: BlockBlob\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\nb\r\nhello world\r\n0\r\n\r\n");

        StringAssert.StartsWith(response, "HTTP/1.1 413 Content Too Large\r\n");
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWithNoUploadLimit_IsStored()
    {
        var harness = Writable(AzureServerHarness.Writable with { MaxUploadBytes = 0 });

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_ZeroBytePutBlobEndingInASlash_CreatesTheFolder()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/2026/", string.Empty, "x-ms-blob-type: BlockBlob");

        Assert.AreEqual(
            "HTTP/1.1 201 Created\r\n" + AzureServerHarness.CommonFields(response)
                + $"ETag: \"d41d8cd98f00b204e9800998ecf8427e\"\r\nLast-Modified: {AzureServerHarness.NowHttpDate}\r\nContent-MD5: 1B2M2Y8AsgTpgAmY7PhCfg==\r\n"
                + "x-ms-request-server-encrypted: false\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        Assert.AreEqual(ContentEntryKind.Directory, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "2026")));
    }

    [TestMethod]
    public async Task ServeAsync_NonEmptyPutBlobEndingInASlash_Is400InvalidResourceName()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/2026/", "x", "x-ms-blob-type: BlockBlob");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "400 Bad Request", "InvalidResourceName", "The specified resource name contains invalid characters."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWhoseFolderIsAFile_Is409InvalidResourceName()
    {
        var harness = Writable();
        harness.WriteFile("photos/a.txt", "x");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt/b.txt", "hello world", "x-ms-blob-type: BlockBlob");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "409 Conflict", "InvalidResourceName", "The specified resource name contains invalid characters."),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobOverAFolder_Is409InvalidResourceName()
    {
        var harness = Writable();
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/2026", "hello world", "x-ms-blob-type: BlockBlob");

        StringAssert.StartsWith(response, "HTTP/1.1 409 Conflict\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobOfANameTheStoreCannotHold_Is400InvalidResourceName()
    {
        var harness = Writable();

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a%00b", "hello world", "x-ms-blob-type: BlockBlob");

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>InvalidResourceName</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobToAMissingContainer_Is404ContainerNotFound()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable);

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob");

        StringAssert.StartsWith(response, "HTTP/1.1 404 Not Found\r\n");
        StringAssert.Contains(response, "<Code>ContainerNotFound</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobToAShareThatOnlyGrantsReading_Is403AuthorizationPermissionMismatch()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        var share = new ContentShare("shared", AzureServerHarness.Root, shareFileSystem)
        {
            Grants = new Dictionary<string, ContentShareAccess> { ["alice"] = ContentShareAccess.Read },
        };
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [share], accountName: "alice");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/shared/a.txt", "hello world", "x-ms-blob-type: BlockBlob");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "403 Forbidden", "AuthorizationPermissionMismatch", "This request is not authorized to perform this operation using this permission."),
            response);
        Assert.AreEqual(ContentEntryKind.None, shareFileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "a.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobInAFolderOfAReadOnlyShare_Is403AuthorizationPermissionMismatch()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        var share = new ContentShare("shared", AzureServerHarness.Root, shareFileSystem) { IsReadOnly = true };
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [share]);

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/shared/2026/a.txt", "hello world", "x-ms-blob-type: BlockBlob");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
        StringAssert.Contains(response, "<Code>AuthorizationPermissionMismatch</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobToAWritableShare_IsStoredAndAnsweredWithTheIndexDefaults()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, shares: [new ContentShare("shared", AzureServerHarness.Root, shareFileSystem)]);

        await harness.SendWithBearerAndBodyAsync("PUT", "/shared/a.txt", "hello world", "x-ms-blob-type: BlockBlob", "x-ms-blob-content-type: text/csv");
        var response = await harness.SendWithBearerAsync("HEAD", "/shared/a.txt");

        StringAssert.Contains(response, $"Content-Type: text/plain\r\nContent-MD5: {HelloWorldMd5}\r\nETag: {HelloWorldETag}\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobBehindABucketName_KeepsItsMetadataUnderTheContentPath()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable, buckets: [new ContentBucket("media", "/store/photos")]);
        harness.CreateFolder("store");
        harness.CreateFolder("store/photos");

        await harness.SendWithBearerAndBodyAsync("PUT", "/media/a.txt", "hello world", "x-ms-blob-type: BlockBlob", "x-ms-blob-content-type: text/csv");
        var response = await harness.SendWithBearerAsync("HEAD", "/media/a.txt");

        Assert.AreEqual("hello world", harness.ReadFile("store/photos/a.txt"));
        StringAssert.Contains(response, "Content-Type: text/csv\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWithTheIndexFull_Is503ServerBusyAndTheBlobIsRemoved()
    {
        var harness = Writable(maxIndexEntries: 1);
        await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/b.txt", "hello world", "x-ms-blob-type: BlockBlob");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "503 Service Unavailable", "ServerBusy", "The server is currently unable to receive requests. Please retry your request."),
            response);
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, "photos", "b.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_PutBlobWhoseIndexCannotBeWritten_IsStoredAndTheFailureNoted()
    {
        var harness = new AzureServerHarness(maxTotalBytes: 22, persistsIndex: true, exposure: AzureServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "x-ms-blob-type: BlockBlob");

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.IsTrue(harness.Log.Notes.Any(note => note.StartsWith("The object metadata index could not be written: ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_FolderAppearingAtTheNameWhileTheBodyIsRead_Is403AuthorizationPermissionMismatch()
    {
        var harness = Writable();
        var inner = new InMemoryConnection([
            Encoding.Latin1.GetBytes($"PUT /photos/a.txt HTTP/1.1\r\nHost: h\r\n{BearerFields()}x-ms-blob-type: BlockBlob\r\nContent-Length: 11\r\nConnection: close\r\n\r\n"),
            Encoding.Latin1.GetBytes("hello world")]);
        var connection = new UnitTestActingConnection(inner, () => harness.CreateFolder("photos/a.txt"));

        await harness.Server.ServeAsync(connection, harness.Context());

        StringAssert.StartsWith(Encoding.UTF8.GetString(inner.WrittenBytes), "HTTP/1.1 403 Forbidden\r\n");
    }

    [TestMethod]
    public void BodyRefusal_EachOutcome_HasItsError()
    {
        Assert.IsNull(AzurePutBlob.BodyRefusal(HttpRequestBodyDiscardOutcome.Discarded, isTooLarge: false));
        Assert.AreSame(AzureError.RequestBodyTooLarge, AzurePutBlob.BodyRefusal(HttpRequestBodyDiscardOutcome.Discarded, isTooLarge: true));
        Assert.AreSame(AzureError.RequestBodyTooLarge, AzurePutBlob.BodyRefusal(HttpRequestBodyDiscardOutcome.TooLarge, isTooLarge: false));
        Assert.AreSame(AzureError.InvalidInput, AzurePutBlob.BodyRefusal(HttpRequestBodyDiscardOutcome.Incomplete, isTooLarge: false));
    }

    [TestMethod]
    public void CommitRefusal_EachUploadResult_IsAnsweredAsADeniedWriteUnlessWritten()
    {
        Assert.IsNull(AzurePutBlob.CommitRefusal(ContentUploadResult.Written));
        Assert.AreSame(AzureError.AuthorizationPermissionMismatch, AzurePutBlob.CommitRefusal(ContentUploadResult.NotPermitted));
    }

    [TestMethod]
    public void MetadataRefusal_EachStoreOutcome_HasItsError()
    {
        Assert.IsNull(AzurePutBlob.MetadataRefusal(ObjectMetadataStoreOutcome.Stored));
        Assert.IsNull(AzurePutBlob.MetadataRefusal(ObjectMetadataStoreOutcome.NoSuchFile));
        Assert.AreSame(AzureError.MetadataTooLarge, AzurePutBlob.MetadataRefusal(ObjectMetadataStoreOutcome.MetadataTooLarge));
        Assert.AreSame(AzureError.ServerBusy, AzurePutBlob.MetadataRefusal(ObjectMetadataStoreOutcome.MetadataIndexFull));
    }

    private static AzureServerHarness Writable(ContentExposureOptions? exposure = null, int maxIndexEntries = ObjectMetadataIndex.DefaultMaxEntries)
    {
        var harness = new AzureServerHarness(exposure: exposure ?? AzureServerHarness.Writable, maxIndexEntries: maxIndexEntries);
        harness.CreateFolder("photos");

        return harness;
    }

    private static string BearerFields() => string.Concat(AzureServerHarness.Bearer.Select(field => field + "\r\n"));
}
