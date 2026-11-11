using Surl.Content;
using static Surl.Extended.Protocol.GoogleCloudStorage.GcsJsonServer;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The JSON API's reads and delete (ADR-0148, decisions 1 to 6): <c>buckets.list</c>, <c>buckets.get</c>,
/// <c>objects.list</c>, <c>objects.get</c> and <c>alt=media</c>, <c>objects.delete</c>, and the <c>501</c> for every
/// other request, written by hand from the Cloud Storage JSON API reference pages.
/// </summary>
[TestClass]
public sealed class GcsJsonRequestAnswererTests
{
    private const string Bucket = "/storage/v1/b/photos";

    [TestMethod]
    public async Task ServeAsync_StoragePath_IsJsonAndAnyOtherPathIsTheXmlApi()
    {
        var harness = HarnessWithHello();

        var json = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/a.txt");
        var xml = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        Assert.AreEqual(Json(HelloObject()), json);
        StringAssert.StartsWith(xml, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(xml, "hello world");
    }

    [TestMethod]
    public async Task ServeAsync_DownloadAndUploadPaths_AreJson()
    {
        var harness = HarnessWithHello();

        var download = await harness.SendWithBearerAsync("GET", "/download/storage/v1/b/photos/o/a.txt?alt=media");
        var upload = await harness.SendWithBearerAsync("GET", "/upload/storage/v1/b/photos/o");

        StringAssert.StartsWith(download, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(download, "hello world");
        Assert.AreEqual(NotImplemented, upload);
    }

    [TestMethod]
    public async Task ServeAsync_VirtualHostedRequestUnderTheStoragePath_IsTheXmlApi()
    {
        var harness = new GcsServerHarness(listenHost: "localhost");
        harness.CreateFolder("photos");

        var response = await harness.SendRawAsync($"GET /storage/v1/b/x HTTP/1.1\r\nHost: photos.localhost:4443\r\n{GcsServerHarness.BearerField}\r\nConnection: close\r\n\r\n");

        Assert.AreEqual(GcsServerHarness.ExpectedError("404 Not Found", "NoSuchKey", "The specified key does not exist."), response);
    }

    [TestMethod]
    public async Task ServeAsync_BucketsList_IsTheTopLevelFoldersInByteOrder()
    {
        var harness = HarnessWithHello();
        harness.CreateFolder("archive");

        var response = await harness.SendWithBearerAsync("GET", "/storage/v1/b?project=any-project&prettyPrint=true&fields=items");

        var bucket = (string name) =>
            $"{{\"kind\":\"storage#bucket\",\"id\":\"{name}\",\"name\":\"{name}\",\"selfLink\":\"http://127.0.0.1:4443/storage/v1/b/{name}\",\"projectNumber\":\"0\","
            + $"\"metageneration\":\"1\",\"location\":\"US\",\"storageClass\":\"STANDARD\",\"etag\":\"CAE=\",\"timeCreated\":\"{GcsServerHarness.NowListTime}\",\"updated\":\"{GcsServerHarness.NowListTime}\"}}";
        Assert.AreEqual(Json($"{{\"kind\":\"storage#buckets\",\"items\":[{bucket("archive")},{bucket("photos")}]}}"), response);
    }

    [TestMethod]
    [DataRow("/storage/v1/b", DisplayName = "no project")]
    [DataRow("/storage/v1/b?project=", DisplayName = "an empty project")]
    public async Task ServeAsync_BucketsListWithoutAProject_Is400Required(string target)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", target);

        Assert.AreEqual(JsonError("400 Bad Request", "Required parameter missing.", "required"), response);
    }

    [TestMethod]
    public async Task ServeAsync_BucketsListWithAnotherAlt_Is400InvalidParameter()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", "/storage/v1/b?project=p&alt=media");

        Assert.AreEqual(JsonError("400 Bad Request", "Invalid parameter.", "invalidParameter"), response);
    }

    [TestMethod]
    public async Task ServeAsync_BucketsListWithListingOff_Is403Forbidden()
    {
        var harness = HarnessWithHello(new ContentExposureOptions { AllowUploads = true });

        var response = await harness.SendWithBearerAsync("GET", "/storage/v1/b?project=p");

        Assert.AreEqual(JsonError("403 Forbidden", "Access denied.", "forbidden"), response);
    }

    [TestMethod]
    public async Task ServeAsync_BucketsGet_IsTheBucketResource()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", Bucket);

        Assert.AreEqual(
            Json(
                "{\"kind\":\"storage#bucket\",\"id\":\"photos\",\"name\":\"photos\",\"selfLink\":\"http://127.0.0.1:4443/storage/v1/b/photos\",\"projectNumber\":\"0\","
                + $"\"metageneration\":\"1\",\"location\":\"US\",\"storageClass\":\"STANDARD\",\"etag\":\"CAE=\",\"timeCreated\":\"{GcsServerHarness.NowListTime}\",\"updated\":\"{GcsServerHarness.NowListTime}\"}}"),
            response);
    }

    [TestMethod]
    [DataRow("/storage/v1/b/missing", DisplayName = "a bucket that is not there")]
    [DataRow("/storage/v1/b/photos%2Fa.txt", DisplayName = "a name that is a file")]
    [DataRow("/storage/v1/b/missing/o/a.txt", DisplayName = "an object in a missing bucket")]
    [DataRow("/storage/v1/b/missing/o", DisplayName = "a listing of a missing bucket")]
    public async Task ServeAsync_NoSuchBucket_Is404NotFound(string target)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", target);

        Assert.AreEqual(NotFound, response);
    }

    [TestMethod]
    public async Task ServeAsync_BucketsGetWithAnotherAlt_Is400InvalidParameter()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}?alt=xml");

        Assert.AreEqual(JsonError("400 Bad Request", "Invalid parameter.", "invalidParameter"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ObjectsList_IsTheItemsAndPrefixesInByteOrder()
    {
        var harness = HarnessWithHello();
        harness.CreateFolder("photos/2026");
        harness.WriteFile("photos/2026/b.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o?delimiter=/");

        Assert.AreEqual(Json($"{{\"kind\":\"storage#objects\",\"prefixes\":[\"2026/\"],\"items\":[{HelloObject()}]}}"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ObjectsListWithAPrefix_ListsTheNamesUnderIt()
    {
        var harness = HarnessWithHello();
        harness.CreateFolder("photos/2026");
        harness.WriteFile("photos/2026/b.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o?prefix=2026/");

        Assert.AreEqual(Json($"{{\"kind\":\"storage#objects\",\"items\":[{HelloObject("2026/b.txt", "2026%2Fb.txt")}]}}"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ObjectsListPaged_ResumesAfterTheLastEntryReturned()
    {
        var harness = HarnessWithHello();
        harness.WriteFile("photos/b.txt", "hello world");
        var token = GcsContinuationToken.Encode("a.txt");

        var first = await harness.SendWithBearerAsync("GET", $"{Bucket}/o?maxResults=1");
        var second = await harness.SendWithBearerAsync("GET", $"{Bucket}/o?maxResults=1&pageToken={token}");

        Assert.AreEqual(Json($"{{\"kind\":\"storage#objects\",\"nextPageToken\":\"{token}\",\"items\":[{HelloObject()}]}}"), first);
        Assert.AreEqual(Json($"{{\"kind\":\"storage#objects\",\"items\":[{HelloObject("b.txt", "b.txt")}]}}"), second);
    }

    [TestMethod]
    public async Task ServeAsync_ObjectsListWithMaxResults0_IsAnEmptyPage()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o?maxResults=0");

        Assert.AreEqual(Json("{\"kind\":\"storage#objects\",\"items\":[]}"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ObjectsListWithOffsets_ListsFromTheStartInclusiveToTheEndExclusive()
    {
        var harness = HarnessWithHello();
        harness.WriteFile("photos/b.txt", "hello world");
        harness.WriteFile("photos/c.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o?startOffset=b.txt&endOffset=c.txt&includeTrailingDelimiter=true");

        Assert.AreEqual(Json($"{{\"kind\":\"storage#objects\",\"items\":[{HelloObject("b.txt", "b.txt")}]}}"), response);
    }

    [TestMethod]
    [DataRow("maxResults=-1", DisplayName = "a negative maxResults")]
    [DataRow("maxResults=many", DisplayName = "a maxResults that is no number")]
    [DataRow("pageToken=!!!", DisplayName = "a pageToken that does not decode")]
    [DataRow("includeTrailingDelimiter=maybe", DisplayName = "an includeTrailingDelimiter that is no boolean")]
    [DataRow("alt=media", DisplayName = "an alt other than json")]
    public async Task ServeAsync_ObjectsListWithABadValue_Is400InvalidParameter(string query)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o?{query}");

        Assert.AreEqual(JsonError("400 Bad Request", "Invalid parameter.", "invalidParameter"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ObjectsListWithListingOff_Is403Forbidden()
    {
        var harness = HarnessWithHello(new ContentExposureOptions { AllowUploads = true });

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o");

        Assert.AreEqual(JsonError("403 Forbidden", "Access denied.", "forbidden"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ObjectsGet_IsTheObjectResourceWithItsStoredFields()
    {
        var harness = HarnessWithHello();
        await harness.Index.StoreAsync(
            "photos/a.txt",
            new ObjectMetadata
            {
                ContentType = "text/x-greeting",
                ContentEncoding = "identity",
                ContentDisposition = "inline",
                CacheControl = "max-age=60",
                ContentLanguage = "en",
                UserMetadata = new Dictionary<string, string> { ["colour"] = "blue", ["kind"] = "demo" },
                Generation = long.Parse(GcsServerHarness.NowGeneration, System.Globalization.CultureInfo.InvariantCulture),
                Metageneration = 4,
            },
            CancellationToken.None);

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/a.txt");

        var stored = ",\"contentEncoding\":\"identity\",\"contentDisposition\":\"inline\",\"cacheControl\":\"max-age=60\",\"contentLanguage\":\"en\",\"metadata\":{\"colour\":\"blue\",\"kind\":\"demo\"}";
        Assert.AreEqual(Json(HelloObject(extraMembers: stored, metageneration: "4", contentType: "text/x-greeting")), response);
    }

    [TestMethod]
    public async Task ServeAsync_ObjectsGetWithAGenerationNotInTimeRange_ReadsTheCreationTimeFromTheLastWrite()
    {
        var harness = HarnessWithHello();
        await harness.Index.StoreAsync("photos/a.txt", new ObjectMetadata { Generation = long.MaxValue }, CancellationToken.None);

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/a.txt");

        StringAssert.Contains(response, $"\"timeCreated\":\"{GcsServerHarness.NowListTime}\",\"updated\":\"{GcsServerHarness.NowListTime}\"");
    }

    [TestMethod]
    [DataRow("a%2Etxt", "a.txt", DisplayName = "a name percent-encoded once")]
    [DataRow("2026%2Fb.txt", "2026/b.txt", DisplayName = "an encoded slash")]
    [DataRow("2026/b.txt", "2026/b.txt", DisplayName = "a literal slash")]
    [DataRow("2026%2fb.txt", "2026/b.txt", DisplayName = "a lower-case encoded slash")]
    public async Task ServeAsync_ObjectsGetOfAFolderedName_SeparatesFoldersByEitherSlash(string rawName, string name)
    {
        var harness = HarnessWithHello();
        harness.CreateFolder("photos/2026");
        harness.WriteFile("photos/2026/b.txt", "hello world");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/{rawName}");

        StringAssert.Contains(response, $"\"name\":\"{name}\"");
        StringAssert.Contains(response, $"\"selfLink\":\"http://127.0.0.1:4443/storage/v1/b/photos/o/{Uri.EscapeDataString(name)}\"");
    }

    [TestMethod]
    [DataRow("missing.txt", DisplayName = "no such object")]
    [DataRow("2026", DisplayName = "a folder")]
    [DataRow("%2e%2e%2fx", DisplayName = "a name the store cannot hold")]
    [DataRow("a.txt?generation=5", DisplayName = "another generation")]
    public async Task ServeAsync_ObjectsGetOfNoObject_Is404NotFound(string rawNameAndQuery)
    {
        var harness = HarnessWithHello();
        harness.CreateFolder("photos/2026");

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/{rawNameAndQuery}");

        Assert.AreEqual(NotFound, response);
    }

    [TestMethod]
    public async Task ServeAsync_ObjectsGetWithTheCurrentGeneration_IsTheResource()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/a.txt?generation={GcsServerHarness.NowGeneration}&alt=json");

        Assert.AreEqual(Json(HelloObject()), response);
    }

    [TestMethod]
    public async Task ServeAsync_ObjectsGetOfANameOver1024Bytes_Is400Invalid()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/{new string('x', 1025)}");

        Assert.AreEqual(JsonError("400 Bad Request", "Invalid request.", "invalid"), response);
    }

    [TestMethod]
    [DataRow("generation=abc", DisplayName = "a generation that is no number")]
    [DataRow("ifGenerationMatch=-1", DisplayName = "a condition that is no number")]
    [DataRow("alt=xml", DisplayName = "an alt other than json and media")]
    public async Task ServeAsync_ObjectsGetWithABadValue_Is400InvalidParameter(string query)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/a.txt?{query}");

        Assert.AreEqual(JsonError("400 Bad Request", "Invalid parameter.", "invalidParameter"), response);
    }

    [TestMethod]
    [DataRow("ifGenerationMatch=5", DisplayName = "another generation")]
    [DataRow("ifGenerationMatch=0", DisplayName = "no object wanted")]
    [DataRow("ifMetagenerationMatch=2", DisplayName = "another metageneration")]
    public async Task ServeAsync_ObjectsGetWithAFailingCondition_Is412ConditionNotMet(string query)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/a.txt?{query}");

        Assert.AreEqual(JsonError("412 Precondition Failed", "Precondition Failed", "conditionNotMet"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ObjectsGetWithAHoldingCondition_IsTheResource()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/a.txt?ifGenerationMatch={GcsServerHarness.NowGeneration}&ifMetagenerationMatch=1&ifGenerationNotMatch=5");

        Assert.AreEqual(Json(HelloObject()), response);
    }

    [TestMethod]
    [DataRow($"ifGenerationNotMatch={GcsServerHarness.NowGeneration}", DisplayName = "the same generation")]
    [DataRow("ifMetagenerationNotMatch=1", DisplayName = "the same metageneration")]
    public async Task ServeAsync_ObjectsGetWithAMatchingNotMatchCondition_Is304WithNoBody(string query)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/a.txt?{query}");

        Assert.AreEqual(
            $"HTTP/1.1 304 Not Modified\r\n{GcsServerHarness.CommonFields}Last-Modified: {GcsServerHarness.NowHttpDate}\r\nETag: {HelloWorldETag}\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    [DataRow($"If-None-Match: {HelloWorldETag}", "304 Not Modified", DisplayName = "a matching If-None-Match")]
    [DataRow($"If-Modified-Since: {GcsServerHarness.NowHttpDate}", "304 Not Modified", DisplayName = "an unmodified If-Modified-Since")]
    [DataRow("If-Match: \"other\"", "412 Precondition Failed", DisplayName = "a failing If-Match")]
    [DataRow("If-Unmodified-Since: Thu, 05 Nov 2026 11:00:00 GMT", "412 Precondition Failed", DisplayName = "a failing If-Unmodified-Since")]
    public async Task ServeAsync_ObjectsGetWithAnHttpCondition_Is304Or412(string field, string status)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/a.txt?alt=media", field);

        StringAssert.StartsWith(response, $"HTTP/1.1 {status}\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_AltMedia_ServesTheBytesWithTheXmlApisHeaders()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/a.txt?alt=media");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{GcsServerHarness.CommonFields}Last-Modified: {GcsServerHarness.NowHttpDate}\r\nETag: {HelloWorldETag}\r\nAccept-Ranges: bytes\r\nContent-Type: text/plain\r\n"
            + $"x-goog-generation: {GcsServerHarness.NowGeneration}\r\nx-goog-metageneration: 1\r\nx-goog-stored-content-encoding: identity\r\nx-goog-stored-content-length: 11\r\n"
            + $"x-goog-storage-class: STANDARD\r\nx-goog-hash: crc32c={HelloWorldCrc32c},md5={HelloWorldMd5}\r\nContent-Length: 11\r\nConnection: close\r\n\r\nhello world",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_DownloadPathWithNoAlt_ServesTheBytes()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", "/download/storage/v1/b/photos/o/a.txt");

        StringAssert.EndsWith(response, "\r\n\r\nhello world");
    }

    [TestMethod]
    public async Task ServeAsync_DownloadPathWithAltJson_Is400InvalidParameter()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", "/download/storage/v1/b/photos/o/a.txt?alt=json");

        Assert.AreEqual(JsonError("400 Bad Request", "Invalid parameter.", "invalidParameter"), response);
    }

    [TestMethod]
    public async Task ServeAsync_AltMediaWithARange_Is206WithThatRange()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/a.txt?alt=media", "Range: bytes=0-4");

        StringAssert.StartsWith(response, "HTTP/1.1 206 Partial Content\r\n");
        StringAssert.Contains(response, "Content-Range: bytes 0-4/11\r\n");
        StringAssert.EndsWith(response, "\r\n\r\nhello");
    }

    [TestMethod]
    public async Task ServeAsync_ObjectResourceWithARange_IgnoresTheRange()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/a.txt", "Range: bytes=0-4");

        Assert.AreEqual(Json(HelloObject()), response);
    }

    [TestMethod]
    public async Task ServeAsync_AltMediaWithAnUnsatisfiableRange_Is416()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/a.txt?alt=media", "Range: bytes=50-60");

        Assert.AreEqual(JsonError("416 Range Not Satisfiable", "Requested range not satisfiable.", "requestedRangeNotSatisfiable"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ObjectsDelete_Is204AndRemovesTheFile()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("DELETE", $"{Bucket}/o/a.txt");

        Assert.AreEqual($"HTTP/1.1 204 No Content\r\n{GcsServerHarness.CommonFields}Connection: close\r\n\r\n", response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    [DataRow("missing.txt", DisplayName = "no such object")]
    [DataRow("a.txt?generation=5", DisplayName = "another generation")]
    public async Task ServeAsync_ObjectsDeleteOfNoObject_Is404NotFound(string rawNameAndQuery)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("DELETE", $"{Bucket}/o/{rawNameAndQuery}");

        Assert.AreEqual(NotFound, response);
        Assert.AreEqual(ContentEntryKind.File, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ObjectsDeleteWithoutAllowUploads_Is403Forbidden()
    {
        var harness = HarnessWithHello(new ContentExposureOptions { ListDirectories = true });

        var response = await harness.SendWithBearerAsync("DELETE", $"{Bucket}/o/a.txt");

        Assert.AreEqual(JsonError("403 Forbidden", "Access denied.", "forbidden"), response);
        Assert.AreEqual(ContentEntryKind.File, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ObjectsDeleteInAReadOnlyShare_Is403Forbidden()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        GcsServerHarness.WriteFile(shareFileSystem, "a.txt", "hello world");
        var share = new ContentShare("shared", GcsServerHarness.Root, shareFileSystem) { IsReadOnly = true };
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, shares: [share]);

        var response = await harness.SendWithBearerAsync("DELETE", "/storage/v1/b/shared/o/a.txt");

        Assert.AreEqual(JsonError("403 Forbidden", "Access denied.", "forbidden"), response);
    }

    [TestMethod]
    [DataRow($"ifGenerationMatch={GcsServerHarness.NowGeneration}", "204 No Content", DisplayName = "the current generation")]
    [DataRow("ifGenerationMatch=5", "412 Precondition Failed", DisplayName = "another generation")]
    [DataRow("ifGenerationNotMatch=" + GcsServerHarness.NowGeneration, "412 Precondition Failed", DisplayName = "a not-match that matches")]
    [DataRow("ifMetagenerationMatch=1", "204 No Content", DisplayName = "the current metageneration")]
    [DataRow("alt=media", "400 Bad Request", DisplayName = "alt media")]
    [DataRow("ifGenerationMatch=abc", "400 Bad Request", DisplayName = "a condition that is no number")]
    public async Task ServeAsync_ObjectsDeleteWithAQuery_DeletesOnlyWhenItHolds(string query, string status)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("DELETE", $"{Bucket}/o/a.txt?{query}");

        StringAssert.StartsWith(response, $"HTTP/1.1 {status}\r\n");
        Assert.AreEqual(status == "204 No Content" ? ContentEntryKind.None : ContentEntryKind.File, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    [DataRow("POST", "/storage/v1/b/photos/o/a.txt", DisplayName = "POST of an object")]
    [DataRow("PUT", "/upload/storage/v1/b/photos/o?uploadType=media&name=b.txt", DisplayName = "PUT to the upload path")]
    [DataRow("POST", "/upload/storage/v1/b/photos/o/a.txt?uploadType=media", DisplayName = "POST of an object to the upload path")]
    [DataRow("POST", "/download/storage/v1/b/photos/o/a.txt", DisplayName = "POST to the download path")]
    [DataRow("PATCH", "/download/storage/v1/b/photos/o/a.txt", DisplayName = "PATCH to the download path")]
    [DataRow("PATCH", "/storage/v1/b/photos/o", DisplayName = "PATCH of a listing")]
    [DataRow("POST", "/storage/v1/b?project=p", DisplayName = "bucket creation")]
    [DataRow("DELETE", "/storage/v1/b/photos", DisplayName = "bucket deletion")]
    [DataRow("PUT", "/storage/v1/b/photos", DisplayName = "bucket update")]
    [DataRow("PATCH", "/storage/v1/b/photos", DisplayName = "bucket patch")]
    [DataRow("GET", "/storage/v1/b/photos/acl", DisplayName = "bucket ACLs")]
    [DataRow("GET", "/storage/v1/b/photos/iam", DisplayName = "bucket IAM")]
    [DataRow("GET", "/storage/v1/b/photos/notificationConfigs", DisplayName = "notifications")]
    [DataRow("GET", "/storage/v1/projects/p/hmacKeys", DisplayName = "HMAC keys")]
    [DataRow("POST", "/storage/v1/b/photos/o/watch", DisplayName = "watchAll")]
    [DataRow("POST", "/storage/v1/b/photos/o/a.txt/restore", DisplayName = "restore")]
    [DataRow("POST", "/storage/v1/b/photos/o/a.txt/moveTo/o/b.txt", DisplayName = "moveTo")]
    [DataRow("GET", "/storage/v1/b/photos/o?versions=true", DisplayName = "an unlisted listing parameter")]
    [DataRow("GET", "/storage/v1/b/photos/o/a.txt?softDeleted=true", DisplayName = "an unlisted object parameter")]
    [DataRow("GET", "/storage/v1/b?project=p&userProject=q", DisplayName = "an unlisted bucket-list parameter")]
    [DataRow("GET", "/storage/v1/b/photos?userProject=q", DisplayName = "an unlisted bucket parameter")]
    [DataRow("GET", "/storage/v1/b/photos/o/a.txt?access_token=secret", DisplayName = "a token in the query")]
    [DataRow("GET", "/storage/v1/", DisplayName = "no resource")]
    [DataRow("GET", "/storage/v1/b/", DisplayName = "an empty bucket")]
    [DataRow("GET", "/storage/v1/b/photos/o/", DisplayName = "an empty object name")]
    [DataRow("GET", "/storage/v1/b/photos/x", DisplayName = "an unknown collection")]
    [DataRow("DELETE", "/download/storage/v1/b/photos/o/a.txt", DisplayName = "a delete on the download path")]
    [DataRow("GET", "/download/storage/v1/b/photos", DisplayName = "a bucket on the download path")]
    [DataRow("GET", "/upload/storage/v1/b/photos/o", DisplayName = "the upload path")]
    public async Task ServeAsync_EveryOtherJsonRequest_Is501NotImplemented(string method, string target)
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync(method, target);

        Assert.AreEqual(NotImplemented, response);
        Assert.AreEqual(ContentEntryKind.File, harness.EntryKind("photos/a.txt"));
        Assert.Contains($"{method} is no operation the Cloud Storage JSON API serves here; answered 501 notImplemented.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_HeadToAJsonPath_Is501WithItsBody()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("HEAD", $"{Bucket}/o/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 501 Not Implemented\r\n");
        StringAssert.Contains(response, "Content-Type: application/json; charset=UTF-8\r\n");
        StringAssert.Contains(response, "Content-Length: 137\r\n");
        StringAssert.EndsWith(response, "\"reason\":\"notImplemented\"}]}}");
    }

    [TestMethod]
    public async Task ServeAsync_ResourceLinksOverTlsWithNoHost_AreHttpsOnTheListenHost()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendRawOverTlsAsync($"GET {Bucket}/o/a.txt HTTP/1.0\r\n{GcsServerHarness.BearerField}\r\n\r\n");

        StringAssert.Contains(response, "\"selfLink\":\"https://127.0.0.1/storage/v1/b/photos/o/a.txt\"");
    }

    [TestMethod]
    public async Task ServeAsync_ObjectPutOverXml_ReadsBackOverJsonWithTheSameGenerationAndDigests()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var put = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world", "Content-Type: text/plain", "x-goog-meta-colour: blue");
        var json = await harness.SendWithBearerAsync("GET", $"{Bucket}/o/a.txt");

        StringAssert.StartsWith(put, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(put, $"x-goog-generation: {GcsServerHarness.NowGeneration}\r\n");
        StringAssert.Contains(put, "x-goog-metageneration: 1\r\n");
        Assert.AreEqual(Json(HelloObject(extraMembers: ",\"metadata\":{\"colour\":\"blue\"}")), json);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedMetageneration_IsReportedByTheXmlApi()
    {
        var harness = HarnessWithHello();
        await harness.Index.StoreAsync("photos/a.txt", new ObjectMetadata { Metageneration = 3 }, CancellationToken.None);

        var head = await harness.SendWithBearerAsync("HEAD", "/photos/a.txt");
        var list = await harness.SendWithBearerAsync("GET", "/photos");

        StringAssert.Contains(head, "x-goog-metageneration: 3\r\n");
        StringAssert.Contains(list, "<MetaGeneration>3</MetaGeneration>");
    }

    [TestMethod]
    public async Task ServeAsync_XmlMetagenerationCondition_IsJudgedAgainstTheRecordedMetageneration()
    {
        var harness = HarnessWithHello();
        await harness.Index.StoreAsync("photos/a.txt", new ObjectMetadata { Metageneration = 3 }, CancellationToken.None);

        var holds = await harness.SendWithBearerAsync("GET", "/photos/a.txt", "x-goog-if-metageneration-match: 3");
        var fails = await harness.SendWithBearerAsync("GET", "/photos/a.txt", "x-goog-if-metageneration-match: 1");

        StringAssert.StartsWith(holds, "HTTP/1.1 200 OK\r\n");
        StringAssert.StartsWith(fails, "HTTP/1.1 412 Precondition Failed\r\n");
    }
}
