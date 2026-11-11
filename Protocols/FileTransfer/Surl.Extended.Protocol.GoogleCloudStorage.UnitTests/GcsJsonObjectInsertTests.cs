using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.GoogleCloudStorage.GcsJsonServer;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// <c>storage.objects.insert</c> with <c>uploadType=media</c> and <c>uploadType=multipart</c> (ADR-0148, decision 7), written from the
/// "Perform a media upload" and "Perform a multipart upload" pages: the request bytes are the pages' own shapes.
/// </summary>
[TestClass]
public sealed class GcsJsonObjectInsertTests
{
    private const string Boundary = "xyz";

    private const string RelatedType = "Content-Type: multipart/related; boundary=xyz";

    private static readonly string TooLargeError = JsonError("413 Payload Too Large", "Payload too large.", "uploadTooLarge");

    private static readonly string InvalidError = JsonError("400 Bad Request", "Invalid request.", "invalid");

    private static readonly string BadRequestError = JsonError("400 Bad Request", "Bad request.", "badRequest");

    private static readonly string RequiredError = JsonError("400 Bad Request", "Required parameter missing.", "required");

    private static readonly string InvalidParameterError = JsonError("400 Bad Request", "Invalid parameter.", "invalidParameter");

    private static readonly string ConditionError = JsonError("412 Precondition Failed", "Precondition Failed", "conditionNotMet");

    private static readonly string ConflictError = JsonError("409 Conflict", "Conflict.", "conflict");

    private static readonly string ForbiddenError = JsonError("403 Forbidden", "Access denied.", "forbidden");

    [TestMethod]
    public async Task ServeAsync_MediaInsert_StoresTheObjectAndAnswersItsResource()
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, "uploadType=media&name=dir%2Fb.txt", "hello world", "Content-Type: text/plain");

        Assert.AreEqual(Json(HelloObject("dir/b.txt", "dir%2Fb.txt")), response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/dir/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_MediaInsert_IsReadBackOverTheXmlApiWithItsMetageneration()
    {
        var harness = HarnessWithHello();
        await InsertAsync(harness, "uploadType=media&name=b.txt&contentEncoding=gzip", "hello world");

        var xml = await harness.SendWithBearerAsync("GET", "/photos/b.txt");

        StringAssert.Contains(xml, "x-goog-metageneration: 1\r\n");
        StringAssert.Contains(xml, "Content-Type: application/octet-stream\r\n");
        StringAssert.Contains(xml, "Content-Encoding: gzip\r\n");
        StringAssert.EndsWith(xml, "hello world");
    }

    [TestMethod]
    public async Task ServeAsync_MediaInsertOverAnObject_ReplacesItWithMetagenerationOneAndItsConditionsHold()
    {
        var harness = HarnessWithHello();
        await harness.SendWithBearerAndBodyAsync("PATCH", $"{Bucket}/o/a.txt", "{\"contentType\":\"text/csv\"}");

        var response = await InsertAsync(harness, $"uploadType=media&name=a.txt&ifGenerationMatch={GcsServerHarness.NowGeneration}&ifMetagenerationMatch=2", "hello world", "Content-Type: text/plain");

        Assert.AreEqual(Json(HelloObject()), response);
    }

    [TestMethod]
    public async Task ServeAsync_MediaInsertIntoAWritableShare_StoresTheFileAndAnswersTheFilesGeneration()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, shares: [new ContentShare("shared", GcsServerHarness.Root, shareFileSystem)]);

        var response = await harness.SendWithBearerAndBodyAsync("POST", "/upload/storage/v1/b/shared/o?uploadType=media&name=s.txt", "hello world", "Content-Type: text/plain");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(response, "\"bucket\":\"shared\"");
        Assert.AreEqual(ContentEntryKind.File, shareFileSystem.GetEntryKind(Path.Join(GcsServerHarness.Root, "s.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_MediaInsertWithoutAName_Is400Required()
    {
        var harness = HarnessWithHello();

        Assert.AreEqual(RequiredError, await InsertAsync(harness, "uploadType=media", "hello world"));
        Assert.AreEqual(RequiredError, await InsertAsync(harness, "uploadType=media&name=", "hello world"));
    }

    [TestMethod]
    [DataRow("", "400 Bad Request", DisplayName = "no uploadType")]
    [DataRow("uploadType=bogus&name=b.txt", "400 Bad Request", DisplayName = "an unknown uploadType")]
    [DataRow("uploadType=media&name=b.txt&ifGenerationMatch=abc", "400 Bad Request", DisplayName = "a condition that is no number")]
    [DataRow("uploadType=media&name=dir%2F", "400 Bad Request", DisplayName = "a name ending in a slash")]
    [DataRow("uploadType=media&name=a%2F%2e%2e%2Fb.txt", "400 Bad Request", DisplayName = "a name the store cannot hold")]
    public async Task ServeAsync_MediaInsertOfABadRequest_IsRefusedAndStoresNothing(string query, string status)
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, query, "hello world");

        StringAssert.StartsWith(response, $"HTTP/1.1 {status}\r\n");
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_MediaInsertOfANamePast1024Bytes_Is400Invalid()
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, $"uploadType=media&name={new string('n', 1025)}", "hello world");

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_InsertWithoutAllowUploads_Is403Forbidden()
    {
        var harness = HarnessWithHello(new ContentExposureOptions { ListDirectories = true });

        var response = await InsertAsync(harness, "uploadType=media&name=b.txt", "hello world");

        Assert.AreEqual(ForbiddenError, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_InsertWithoutContentLength_Is411LengthRequired()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendWithBearerAsync("POST", "/upload/storage/v1/b/photos/o?uploadType=media&name=b.txt");

        Assert.AreEqual(JsonError("411 Length Required", "Length required.", "lengthRequired"), response);
    }

    [TestMethod]
    public async Task ServeAsync_InsertPastMaxFilesize_Is413UploadTooLargeAndStoresNothing()
    {
        var harness = HarnessWithHello(GcsServerHarness.Writable with { MaxUploadBytes = 4 });

        var media = await InsertAsync(harness, "uploadType=media&name=b.txt", "hello world");
        var multipart = await InsertAsync(harness, "uploadType=multipart", Related("{\"name\":\"c.txt\"}", "hello world"), RelatedType);

        Assert.AreEqual(TooLargeError, media);
        Assert.AreEqual(TooLargeError, multipart);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/c.txt"));
    }

    [TestMethod]
    [DataRow(HttpRequestBodyDiscardOutcome.Discarded, false, null)]
    [DataRow(HttpRequestBodyDiscardOutcome.TooLarge, false, "uploadTooLarge")]
    [DataRow(HttpRequestBodyDiscardOutcome.Discarded, true, "uploadTooLarge")]
    [DataRow(HttpRequestBodyDiscardOutcome.Incomplete, false, "badRequest")]
    public void BodyRefusal_Outcome_IsTheRowItCallsFor(HttpRequestBodyDiscardOutcome outcome, bool isTooLarge, string? reason)
    {
        Assert.AreEqual(reason, GcsJsonObjectInsert.BodyRefusal(outcome, isTooLarge)?.Reason);
    }

    [TestMethod]
    public async Task ServeAsync_MediaInsertCutShort_Is400BadRequestAndStoresNothing()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendRawAsync(
            $"POST /upload/storage/v1/b/photos/o?uploadType=media&name=b.txt HTTP/1.1\r\nHost: 127.0.0.1:4443\r\n{GcsServerHarness.BearerField}\r\nContent-Length: 50\r\nConnection: close\r\n\r\nhello");

        Assert.AreEqual(BadRequestError, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
    }

    [TestMethod]
    [DataRow("Content-MD5: AAAAAAAAAAAAAAAAAAAAAA==", DisplayName = "an MD5 the bytes do not have")]
    [DataRow("x-goog-hash: crc32c=AAAAAA==", DisplayName = "a CRC-32C the bytes do not have")]
    [DataRow("Content-MD5: !!", DisplayName = "an MD5 that is malformed")]
    [DataRow("x-goog-hash: md5=!!", DisplayName = "a hash that is malformed")]
    public async Task ServeAsync_MediaInsertWithAHashItDoesNotHave_Is400InvalidAndStoresNothing(string field)
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, "uploadType=media&name=b.txt", "hello world", field);

        Assert.AreEqual(InvalidError, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_MediaInsertWithTheHashesItHas_IsStored()
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, "uploadType=media&name=b.txt", "hello world", $"Content-MD5: {HelloWorldMd5}", $"x-goog-hash: crc32c={HelloWorldCrc32c}", "Content-Type: text/plain");

        Assert.AreEqual(Json(HelloObject("b.txt", "b.txt")), response);
    }

    [TestMethod]
    [DataRow("ifGenerationMatch=0", "412 Precondition Failed", DisplayName = "none must exist, and one does")]
    [DataRow("ifGenerationMatch=5", "412 Precondition Failed", DisplayName = "another generation")]
    [DataRow("ifMetagenerationMatch=2", "412 Precondition Failed", DisplayName = "another metageneration")]
    [DataRow("ifGenerationNotMatch=" + GcsServerHarness.NowGeneration, "412 Precondition Failed", DisplayName = "a not-match that matches")]
    public async Task ServeAsync_MediaInsertOverAnObjectWhoseConditionFails_Is412AndKeepsTheObject(string condition, string status)
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, $"uploadType=media&name=a.txt&{condition}", "changed");

        StringAssert.StartsWith(response, $"HTTP/1.1 {status}\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    [DataRow("ifGenerationMatch=0", "200 OK", DisplayName = "none must exist, and none does")]
    [DataRow("ifGenerationMatch=5", "412 Precondition Failed", DisplayName = "a generation of an object that is not there")]
    [DataRow("ifMetagenerationMatch=1", "412 Precondition Failed", DisplayName = "a metageneration of an object that is not there")]
    [DataRow("ifGenerationNotMatch=5", "200 OK", DisplayName = "a not-match of an object that is not there")]
    public async Task ServeAsync_MediaInsertOfANewName_JudgesItsConditionsAgainstNoObject(string condition, string status)
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, $"uploadType=media&name=b.txt&{condition}", "hello world");

        StringAssert.StartsWith(response, $"HTTP/1.1 {status}\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_MediaInsertThatConflictsWithAnEntryOfTheOtherKind_Is409Conflict()
    {
        var harness = HarnessWithHello();
        harness.CreateFolder("photos/sub");

        var fileInTheWay = await InsertAsync(harness, "uploadType=media&name=a.txt%2Fx.txt", "hello world");
        var folderInTheWay = await InsertAsync(harness, "uploadType=media&name=sub", "hello world");

        Assert.AreEqual(ConflictError, fileInTheWay);
        Assert.AreEqual(ConflictError, folderInTheWay);
    }

    [TestMethod]
    public async Task ServeAsync_MediaInsertInAFolderOfAReadOnlyShare_Is403Forbidden()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, shares: [new ContentShare("shared", GcsServerHarness.Root, shareFileSystem) { IsReadOnly = true }]);

        var folder = await harness.SendWithBearerAndBodyAsync("POST", "/upload/storage/v1/b/shared/o?uploadType=media&name=dir%2Fs.txt", "hello world");
        var file = await harness.SendWithBearerAndBodyAsync("POST", "/upload/storage/v1/b/shared/o?uploadType=media&name=s.txt", "hello world");

        Assert.AreEqual(ForbiddenError, folder);
        Assert.AreEqual(ForbiddenError, file);
    }

    [TestMethod]
    public async Task ServeAsync_InsertWhenTheMetadataIndexIsFull_Is403QuotaExceededAndStoresNothing()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, maxIndexEntries: 1);
        harness.CreateFolder("photos");
        await InsertAsync(harness, "uploadType=media&name=b.txt", "hello world");

        var response = await InsertAsync(harness, "uploadType=media&name=c.txt", "hello world");

        Assert.AreEqual(JsonError("403 Forbidden", "The object metadata index is full.", "quotaExceeded"), response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/c.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_InsertWithMetadataPast8KiB_Is400InvalidAndKeepsTheObject()
    {
        var harness = HarnessWithHello();
        var metadata = $"{{\"name\":\"a.txt\",\"metadata\":{{\"k\":\"{new string('v', 8200)}\"}}}}";

        var response = await InsertAsync(harness, "uploadType=multipart", Related(metadata, "changed"), RelatedType);

        Assert.AreEqual(InvalidError, response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_InsertWhoseMetadataTheIndexRefusesAsTooLarge_Is400Invalid()
    {
        var harness = HarnessWithHello();
        var metadata = $"{{\"name\":\"b.txt\",\"contentType\":\"text/plain\",\"metadata\":{{\"k\":\"{new string('v', 8190)}\"}}}}";

        var response = await InsertAsync(harness, "uploadType=multipart", Related(metadata, "hello world"), RelatedType);

        Assert.AreEqual(InvalidError, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_MultipartInsert_StoresTheBytesAndTheMetadata()
    {
        var harness = HarnessWithHello();
        var metadata = "{\"name\":\"m.txt\",\"contentType\":\"text/plain\",\"cacheControl\":\"no-cache\",\"contentLanguage\":\"en\","
            + "\"metadata\":{\"k\":\"v\"},\"storageClass\":\"COLDLINE\",\"acl\":[],\"customTime\":\"x\"}";

        var response = await InsertAsync(harness, "uploadType=multipart", Related(metadata, "hello world", "Content-Type: application/octet-stream\r\n"), RelatedType);

        Assert.AreEqual(Json(HelloObject("m.txt", "m.txt", ",\"cacheControl\":\"no-cache\",\"contentLanguage\":\"en\",\"metadata\":{\"k\":\"v\"}")), response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/m.txt"));
    }

    [TestMethod]
    [DataRow("{\"name\":\"m.txt\"}", "Content-Type: text/plain\r\n", "text/plain", DisplayName = "the second part's type when the metadata has none")]
    [DataRow("{\"name\":\"m.txt\"}", "", "application/octet-stream", DisplayName = "the default when neither has one")]
    [DataRow("{\"name\":\"m.txt\",\"contentType\":null}", "X-Other: 1\r\nContent-Type: text/csv\r\n", "text/csv", DisplayName = "the second part's type when the metadata's is null")]
    [DataRow("{\"name\":\"m.txt\",\"contentType\":\"text/html\"}", "Content-Type: text/plain\r\n", "text/html", DisplayName = "the metadata's type over the second part's")]
    public async Task ServeAsync_MultipartInsert_TakesTheContentTypeFromTheMetadataThenTheSecondPart(string metadata, string partFields, string contentType)
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, "uploadType=multipart", Related(metadata, "hello world", partFields), RelatedType);

        Assert.AreEqual(Json(HelloObject("m.txt", "m.txt", contentType: contentType)), response);
    }

    [TestMethod]
    [DataRow("{\"contentType\":\"text/plain\"}", "uploadType=multipart&name=q.txt", "q.txt", DisplayName = "the query's name when the metadata has none")]
    [DataRow("{\"name\":null,\"contentType\":\"text/plain\"}", "uploadType=multipart&name=q.txt", "q.txt", DisplayName = "the query's name when the metadata's is null")]
    [DataRow("{\"name\":\"m.txt\",\"contentType\":\"text/plain\"}", "uploadType=multipart&name=q.txt", "m.txt", DisplayName = "the metadata's name over the query's")]
    public async Task ServeAsync_MultipartInsert_TakesTheNameFromTheMetadataThenTheQuery(string metadata, string query, string name)
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, query, Related(metadata, "hello world"), RelatedType);

        Assert.AreEqual(Json(HelloObject(name, name)), response);
    }

    [TestMethod]
    public async Task ServeAsync_MultipartInsertWithContentEncodingInTheQuery_KeepsIt()
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, "uploadType=multipart&contentEncoding=gzip", Related("{\"name\":\"m.txt\"}", "hello world"), RelatedType);

        StringAssert.Contains(response, "\"contentEncoding\":\"gzip\"");
    }

    [TestMethod]
    public async Task ServeAsync_MultipartInsertWithoutAName_Is400RequiredAndStoresNothing()
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, "uploadType=multipart", Related("{\"contentType\":\"text/plain\"}", "hello world"), RelatedType);

        Assert.AreEqual(RequiredError, response);
    }

    [TestMethod]
    [DataRow("not json", DisplayName = "text")]
    [DataRow("", DisplayName = "no metadata")]
    [DataRow("[1]", DisplayName = "an array")]
    [DataRow("{\"name\":5}", DisplayName = "a name that is no string")]
    [DataRow("{\"name\":\"m.txt\",\"contentType\":5}", DisplayName = "a type that is no string")]
    [DataRow("{\"name\":\"m.txt\",\"metadata\":5}", DisplayName = "metadata that is no object")]
    [DataRow("{\"name\":\"m.txt\",\"metadata\":{\"k\":1}}", DisplayName = "a metadata value that is no string")]
    [DataRow("{\"name\":\"m.txt\",\"md5Hash\":\"!!\"}", DisplayName = "an md5Hash that is malformed")]
    [DataRow("{\"name\":\"m.txt\",\"crc32c\":\"AAAAAA\"}", DisplayName = "a crc32c that is malformed")]
    [DataRow("{\"name\":\"m.txt\",\"md5Hash\":\"AAAAAAAAAAAAAAAAAAAAAA==\"}", DisplayName = "an md5Hash the bytes do not have")]
    [DataRow("{\"name\":\"m.txt\",\"crc32c\":\"AAAAAA==\"}", DisplayName = "a crc32c the bytes do not have")]
    public async Task ServeAsync_MultipartInsertWithABadMetadataPart_Is400InvalidAndStoresNothing(string metadata)
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, "uploadType=multipart", Related(metadata, "hello world"), RelatedType);

        Assert.AreEqual(InvalidError, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/m.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_MultipartInsertWithTheHashesInTheMetadata_IsStored()
    {
        var harness = HarnessWithHello();
        var metadata = $"{{\"name\":\"m.txt\",\"contentType\":\"text/plain\",\"md5Hash\":\"{HelloWorldMd5}\",\"crc32c\":\"{HelloWorldCrc32c}\"}}";

        var response = await InsertAsync(harness, "uploadType=multipart", Related(metadata, "hello world"), RelatedType);

        Assert.AreEqual(Json(HelloObject("m.txt", "m.txt")), response);
    }

    [TestMethod]
    public async Task ServeAsync_MultipartInsertWithAFailingCondition_Is412AndReadsNoMore()
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, "uploadType=multipart&ifGenerationMatch=0", Related("{\"name\":\"a.txt\"}", "changed"), RelatedType);

        Assert.AreEqual(ConditionError, response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    [DataRow("Content-Type: application/json", DisplayName = "another type")]
    [DataRow("Content-Type: multipart/related", DisplayName = "no boundary")]
    [DataRow("Content-Type: multipart/related; boundary=", DisplayName = "an empty boundary")]
    [DataRow("Content-Type: multipart/related; boundary=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", DisplayName = "a boundary past 70 characters")]
    [DataRow("X-No-Type: 1", DisplayName = "no Content-Type")]
    public async Task ServeAsync_MultipartInsertWithoutABoundary_Is400BadRequest(string typeField)
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, "uploadType=multipart", Related("{\"name\":\"m.txt\"}", "hello world"), typeField);

        Assert.AreEqual(BadRequestError, response);
    }

    [TestMethod]
    public async Task ServeAsync_MultipartInsertWithAQuotedBoundary_IsStored()
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, "uploadType=multipart", Related("{\"name\":\"m.txt\"}", "hello world"), "Content-Type: Multipart/Related; charset=x; boundary=\"xyz\"");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    [DataRow("junk--xyz\r\n\r\n{}\r\n--xyz\r\n\r\nhello\r\n--xyz--", DisplayName = "bytes before the first delimiter")]
    [DataRow("--xyz\r\n\r\n{\"name\":\"m.txt\"}\r\n--xyz\r\n\r\nhello\r\n--xyz\r\n\r\nmore\r\n--xyz--", DisplayName = "a third part")]
    [DataRow("--xyz\r\n\r\n{\"name\":\"m.txt\"}\r\n--xyz--", DisplayName = "a close after one part")]
    [DataRow("--xyz\r\n\r\n{\"name\":\"m.txt\"}\r\n--xyz\r\n\r\nhello", DisplayName = "an unterminated body")]
    [DataRow("--xyz\r\n\r\n{\"name\":\"m.txt\"}\r\n--xyzzy", DisplayName = "a delimiter followed by neither a line break nor the close")]
    [DataRow("--xyz\r\n\r\n{\"name\":\"m.txt\"}\r\n--xyz\r\nX: 1", DisplayName = "part fields that never end")]
    [DataRow("--xy", DisplayName = "a body that ends inside the first delimiter")]
    [DataRow("--xyz", DisplayName = "a body that ends after the first delimiter")]
    public async Task ServeAsync_MultipartInsertThatBreaksTheGrammar_Is400BadRequestAndStoresNothing(string body)
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, "uploadType=multipart&name=m.txt", body, RelatedType);

        Assert.AreEqual(BadRequestError, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/m.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_MultipartInsertWithPartFieldsPast8KiB_Is400BadRequest()
    {
        var harness = HarnessWithHello();
        var body = $"--xyz\r\n\r\n{{\"name\":\"m.txt\"}}\r\n--xyz\r\nX-Big: {new string('a', 9000)}\r\n\r\nhello\r\n--xyz--";

        var response = await InsertAsync(harness, "uploadType=multipart", body, RelatedType);

        Assert.AreEqual(BadRequestError, response);
    }

    [TestMethod]
    public async Task ServeAsync_MultipartInsertWithAnEmptyBody_Is400BadRequest()
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, "uploadType=multipart&name=m.txt", string.Empty, RelatedType);

        Assert.AreEqual(BadRequestError, response);
    }

    [TestMethod]
    public async Task ServeAsync_MultipartInsertWithMetadataPast1MiB_Is400Invalid()
    {
        var harness = HarnessWithHello();
        var body = $"--xyz\r\n\r\n{{\"name\":\"m.txt\",\"metadata\":{{\"k\":\"{new string('v', 1024 * 1024)}\"}}}}\r\n--xyz\r\n\r\nhello\r\n--xyz--";

        var response = await InsertAsync(harness, "uploadType=multipart", body, RelatedType);

        Assert.AreEqual(InvalidError, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/m.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_MultipartInsertWithAnEpilogue_IgnoresIt()
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, "uploadType=multipart", Related("{\"name\":\"m.txt\"}", "hello world") + "\r\nthe epilogue", RelatedType);

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/m.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_MultipartInsertOfBytesHoldingThePartOfADelimiter_KeepsThemWhole()
    {
        var harness = HarnessWithHello();
        var content = "a\r\n--xy b\r\n-- c\r\r\n";

        var response = await InsertAsync(harness, "uploadType=multipart", Related("{\"name\":\"m.txt\",\"contentType\":\"text/plain\"}", content), RelatedType);

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual(content, harness.ReadFile("photos/m.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_MediaInsertOfNoBytes_StoresAnEmptyObject()
    {
        var harness = HarnessWithHello();

        var response = await InsertAsync(harness, "uploadType=media&name=empty.txt", string.Empty);

        StringAssert.Contains(response, "\"size\":\"0\"");
        Assert.AreEqual(string.Empty, harness.ReadFile("photos/empty.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_MultipartInsertWithPartFieldsPast8KiBThatNeverEnd_Is400BadRequest()
    {
        var harness = HarnessWithHello();
        var body = $"--xyz\r\n\r\n{{\"name\":\"m.txt\"}}\r\n--xyz\r\nX-Big: {new string('a', 9000)}";

        var response = await InsertAsync(harness, "uploadType=multipart", body, RelatedType);

        Assert.AreEqual(BadRequestError, response);
    }

    [TestMethod]
    public async Task ServeAsync_InsertWhenTheStoreRunsOutOfSpaceAtTheCommit_Is403ForbiddenAndStoresNothing()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, storeFaults: new ScriptedFaultPlan(new FaultRule("full", FaultKind.OutOfSpace)));
        harness.CreateFolder("photos");

        var response = await InsertAsync(harness, "uploadType=media&name=b.txt", "hello world");

        Assert.AreEqual(ForbiddenError, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
    }

    [TestMethod]
    [DataRow(true, HttpRequestBodyDiscardOutcome.Discarded, false, "invalid")]
    [DataRow(false, HttpRequestBodyDiscardOutcome.TooLarge, false, "uploadTooLarge")]
    [DataRow(false, HttpRequestBodyDiscardOutcome.Discarded, true, "uploadTooLarge")]
    [DataRow(false, HttpRequestBodyDiscardOutcome.Discarded, false, "badRequest")]
    public async Task MultipartRefusal_WhatTheReadingFound_IsTheRowItCallsFor(bool isMetadataTooLarge, HttpRequestBodyDiscardOutcome outcome, bool isTooLarge, string reason)
    {
        await using var sink = new GcsMultipartRelatedSink("b", (_, _) => Task.FromResult<Stream?>(null));
        await sink.WriteAsync(isMetadataTooLarge ? [.. "--b\r\n\r\n"u8, .. new byte[GcsMultipartRelatedSink.MaxMetadataBytes + 100]] : "junk"u8.ToArray());

        Assert.AreEqual(reason, GcsJsonObjectInsert.MultipartRefusal(sink, outcome, isTooLarge)?.Reason);
    }

    [TestMethod]
    public async Task MultipartRefusal_AWholeBody_IsNone()
    {
        await using var sink = new GcsMultipartRelatedSink("b", (_, _) => Task.FromResult<Stream?>(new MemoryStream()));
        await sink.WriteAsync("--b\r\n\r\n{}\r\n--b\r\n\r\nx\r\n--b--"u8.ToArray());

        Assert.IsNull(GcsJsonObjectInsert.MultipartRefusal(sink, HttpRequestBodyDiscardOutcome.Discarded, false));
    }

    [TestMethod]
    public async Task ServeAsync_ChosenErrorOnInsert_IsAskedWithTheInsertMethodId()
    {
        var harness = HarnessWithHello();
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "gs", Reply = "503" });
        harness.Faults = faults;

        var response = await InsertAsync(harness, "uploadType=media&name=b.txt", "hello world");

        StringAssert.StartsWith(response, "HTTP/1.1 503 Service Unavailable\r\n");
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
    }

    private const string Bucket = "/storage/v1/b/photos";

    private static Task<string> InsertAsync(GcsServerHarness harness, string query, string body, params string[] fields) =>
        harness.SendWithBearerAndBodyAsync("POST", $"/upload/storage/v1/b/photos/o?{query}", body, fields);

    private static string Related(string metadata, string content, string partFields = "Content-Type: text/plain\r\n") =>
        $"--{Boundary}\r\nContent-Type: application/json; charset=UTF-8\r\n\r\n{metadata}\r\n--{Boundary}\r\n{partFields}\r\n{content}\r\n--{Boundary}--";
}
