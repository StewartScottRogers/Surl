using System.Text.RegularExpressions;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.GoogleCloudStorage.GcsJsonServer;
using static Surl.Extended.Protocol.GoogleCloudStorage.GcsUploadRequests;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// <c>storage.objects.insert</c> with <c>uploadType=resumable</c> (ADR-0148, decision 7), written from the "Perform resumable
/// uploads" page, JSON API tab, and ADR-0110 amendment 2's chunk rules: the request bytes are the page's own shapes.
/// </summary>
[TestClass]
public sealed partial class GcsJsonResumableUploadTests
{
    private const string Upload = "/upload/storage/v1/b/photos/o";

    private static readonly string BadRequestError = JsonError("400 Bad Request", "Bad request.", "badRequest");

    private static readonly string InvalidError = JsonError("400 Bad Request", "Invalid request.", "invalid");

    private static readonly string TooLargeError = JsonError("413 Payload Too Large", "Payload too large.", "uploadTooLarge");

    private static readonly string ForbiddenError = JsonError("403 Forbidden", "Access denied.", "forbidden");

    [GeneratedRegex("upload_id=([0-9a-f]{32})\r\n")]
    private static partial Regex SessionUri();

    [TestMethod]
    public async Task ServeAsync_Open_Is200WithTheSessionUriInLocation()
    {
        var harness = HarnessWithHello();

        var response = await OpenAsync(harness, "uploadType=resumable&name=b.txt");
        var uploadId = IdOf(response);

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{GcsServerHarness.CommonFields}Location: http://127.0.0.1:4443{Upload}?uploadType=resumable&upload_id={uploadId}\r\n"
                + "Content-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_TwoChunks_AreAnswered308WithRangeThen200WithTheObjectAndReadBackOverXml()
    {
        var harness = HarnessWithHello();
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt", string.Empty, "X-Upload-Content-Type: text/plain", "X-Upload-Content-Length: 11"));

        var first = await ChunkAsync(harness, uploadId, "hello ", "Content-Range: bytes 0-5/*");
        var invisible = await harness.SendWithBearerAsync("GET", "/photos/b.txt");
        var last = await ChunkAsync(harness, uploadId, "world", "Content-Range: bytes 6-10/11");
        var read = await harness.SendWithBearerAsync("GET", "/photos/b.txt");

        Assert.AreEqual($"HTTP/1.1 308 Resume Incomplete\r\n{GcsServerHarness.CommonFields}Range: bytes=0-5\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", first);
        StringAssert.Contains(invisible, "<Code>NoSuchKey</Code>");
        Assert.AreEqual(Json(HelloObject("b.txt", "b.txt")), last);
        StringAssert.Contains(read, "Content-Type: text/plain\r\n");
        StringAssert.EndsWith(read, "hello world");
        Assert.HasCount(2, harness.FileSystem.EnumerateDirectoryEntryNames(Path.Join(GcsServerHarness.Root, "photos")));
    }

    [TestMethod]
    public async Task ServeAsync_OpenWithMetadata_KeepsItsNameTypeAndCustomMetadata()
    {
        var harness = HarnessWithHello();
        var metadata = "{\"name\":\"dir/c.txt\",\"contentType\":\"text/csv\",\"contentEncoding\":\"gzip\",\"metadata\":{\"colour\":\"blue\"}}";
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=ignored.txt", metadata, "X-Upload-Content-Type: text/plain"));

        var response = await ChunkAsync(harness, uploadId, "hello world");
        var read = await harness.SendWithBearerAsync("GET", "/photos/dir/c.txt");

        StringAssert.Contains(response, "\"name\":\"dir/c.txt\"");
        StringAssert.Contains(response, "\"metadata\":{\"colour\":\"blue\"}");
        StringAssert.Contains(read, "Content-Type: text/csv\r\n");
        StringAssert.Contains(read, "Content-Encoding: gzip\r\n");
        StringAssert.Contains(read, "x-goog-meta-colour: blue\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_OpenWithNeitherTypeNorMetadata_StoresAnOctetStreamWithTheQueryEncoding()
    {
        var harness = HarnessWithHello();
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt&contentEncoding=gzip"));

        var response = await ChunkAsync(harness, uploadId, "hello world");

        StringAssert.Contains(response, "\"contentType\":\"application/octet-stream\"");
        StringAssert.Contains(response, "\"contentEncoding\":\"gzip\"");
    }

    [TestMethod]
    public async Task ServeAsync_AStatusCheck_ReportsWhatIsKeptAndCompletesWhenTheTotalIsIn()
    {
        var harness = HarnessWithHello();
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt"));

        var none = await StatusAsync(harness, uploadId, "*");
        await ChunkAsync(harness, uploadId, "hello ", "Content-Range: bytes 0-5/*");
        var kept = await StatusAsync(harness, uploadId, "11");
        var misplaced = await ChunkAsync(harness, uploadId, "world", "Content-Range: bytes 7-11/12");
        var done = await ChunkAsync(harness, uploadId, "world", "Content-Range: bytes 6-10/11");

        Assert.AreEqual($"HTTP/1.1 308 Resume Incomplete\r\n{GcsServerHarness.CommonFields}Content-Length: 0\r\nConnection: close\r\n\r\n", none);
        StringAssert.Contains(kept, "Range: bytes=0-5\r\n");
        StringAssert.Contains(misplaced, "Range: bytes=0-5\r\n");
        Assert.AreEqual(Json(HelloObject("b.txt", "b.txt", contentType: "application/octet-stream")), done);
    }

    [TestMethod]
    public async Task ServeAsync_AStatusCheckWithTheWholeTotalAlreadyIn_CompletesTheUpload()
    {
        var harness = HarnessWithHello();
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt"));
        await ChunkAsync(harness, uploadId, "hello world", "Content-Range: bytes 0-10/*");

        var response = await StatusAsync(harness, uploadId, "11");

        Assert.AreEqual(Json(HelloObject("b.txt", "b.txt", contentType: "application/octet-stream")), response);
    }

    [TestMethod]
    public async Task ServeAsync_AnEmptyObject_IsCompletedByAStatusCheckOfZero()
    {
        var harness = HarnessWithHello();
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=empty.txt"));

        var response = await StatusAsync(harness, uploadId, "0");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual(string.Empty, harness.ReadFile("photos/empty.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ATotalBelowTheBytesAlreadyIn_Is400BadRequest()
    {
        var harness = HarnessWithHello();
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt"));
        await ChunkAsync(harness, uploadId, "hello ", "Content-Range: bytes 0-5/*");

        var response = await StatusAsync(harness, uploadId, "3");

        Assert.AreEqual(BadRequestError, response);
    }

    [TestMethod]
    public async Task ServeAsync_Cancel_Is499AndTheSessionIsGone()
    {
        var harness = HarnessWithHello();
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt"));
        await ChunkAsync(harness, uploadId, "hello ", "Content-Range: bytes 0-5/*");

        var cancelled = await CancelAsync(harness, uploadId);
        var later = await ChunkAsync(harness, uploadId, "world", "Content-Range: bytes 6-10/11");
        var again = await CancelAsync(harness, uploadId);

        Assert.AreEqual($"HTTP/1.1 499 Client Closed Request\r\n{GcsServerHarness.CommonFields}Content-Length: 0\r\nConnection: close\r\n\r\n", cancelled);
        Assert.AreEqual(NotFound, later);
        Assert.AreEqual(NotFound, again);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ASessionOfTheOtherGrammar_Is404InBothDirections()
    {
        var harness = HarnessWithHello();
        var xmlId = await StartSessionAsync(harness, "/photos/b.txt");
        var jsonId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt"));

        var xmlOnJson = await ChunkAsync(harness, xmlId, "hello world");
        var jsonOnXml = await PutChunkAsync(harness, "/photos/b.txt", jsonId, "hello world");
        var cancelXmlOnJson = await CancelAsync(harness, xmlId);

        Assert.AreEqual(NotFound, xmlOnJson);
        StringAssert.Contains(jsonOnXml, "<Code>NoSuchUpload</Code>");
        Assert.AreEqual(NotFound, cancelXmlOnJson);
        StringAssert.StartsWith(await ChunkAsync(harness, jsonId, "hello world"), "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_ASessionOfAnotherBucketOrNone_Is404()
    {
        var harness = HarnessWithHello();
        harness.CreateFolder("other");
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt"));

        var otherBucket = await harness.SendWithBearerAndBodyAsync("PUT", $"/upload/storage/v1/b/other/o?uploadType=resumable&upload_id={uploadId}", "hello world");
        var noId = await harness.SendWithBearerAndBodyAsync("PUT", $"{Upload}?uploadType=resumable", "hello world");
        var noIdCancel = await harness.SendWithBearerAsync("DELETE", $"{Upload}?uploadType=resumable");

        Assert.AreEqual(NotFound, otherBucket);
        Assert.AreEqual(NotFound, noId);
        Assert.AreEqual(NotFound, noIdCancel);
    }

    [TestMethod]
    public async Task ServeAsync_TheThousandAndFirstOpenUpload_Is503BackendErrorSharedWithTheXmlApi()
    {
        var harness = HarnessWithHello();
        for (var count = 0; count < GcsStagedUploads.MaxOpenUploads - 1; count++)
        {
            await OpenAsync(harness, "uploadType=resumable&name=b.txt");
        }

        await StartSessionAsync(harness, "/photos/c.txt");
        var response = await OpenAsync(harness, "uploadType=resumable&name=b.txt");

        Assert.AreEqual(JsonError("503 Service Unavailable", "Please reduce your request rate.", "backendError", domain: "usageLimits"), response);
    }

    [TestMethod]
    public async Task ServeAsync_AnUploadIdlePast24Hours_IsDiscardedOnTheInjectedClock()
    {
        var harness = HarnessWithHello();
        var idle = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt"));
        harness.TimeProvider.UtcNow = GcsServerHarness.Now.AddHours(23);
        var touched = IdOf(await OpenAsync(harness, "uploadType=resumable&name=c.txt"));
        harness.TimeProvider.UtcNow = GcsServerHarness.Now.AddHours(24).AddSeconds(1);

        var discarded = await ChunkAsync(harness, idle, "hello world");
        var kept = await ChunkAsync(harness, touched, "hello world");

        StringAssert.StartsWith(discarded, "HTTP/1.1 404 Not Found\r\n");
        StringAssert.StartsWith(kept, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    [DataRow("Content-Range: bytes abc", DisplayName = "a Content-Range that does not parse")]
    [DataRow("Content-Range: bytes 0-4/11", DisplayName = "a range whose length is not the chunk's")]
    [DataRow("Content-Range: bytes 5-4/11", DisplayName = "a range that ends before it starts")]
    public async Task ServeAsync_AMalformedContentRangeOrAWrongChunkLength_Is400BadRequest(string field)
    {
        var harness = HarnessWithHello();
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt"));

        var response = await ChunkAsync(harness, uploadId, "hel", field);

        Assert.AreEqual(BadRequestError, response);
    }

    [TestMethod]
    public async Task ServeAsync_AChunkWithoutContentLength_Is411LengthRequired()
    {
        var harness = HarnessWithHello();
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt"));

        var response = await harness.SendWithBearerAsync("PUT", $"{Upload}?uploadType=resumable&upload_id={uploadId}");

        Assert.AreEqual(JsonError("411 Length Required", "Length required.", "lengthRequired"), response);
    }

    [TestMethod]
    public async Task ServeAsync_AChunkThatEndsEarly_Is400BadRequestAndKeepsTheBytesBefore()
    {
        var harness = HarnessWithHello();
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt"));
        await ChunkAsync(harness, uploadId, "hello", "Content-Range: bytes 0-4/*");

        var response = await harness.SendRawAsync(
            $"PUT {Upload}?uploadType=resumable&upload_id={uploadId} HTTP/1.1\r\nHost: x\r\n{GcsServerHarness.BearerField}\r\nContent-Range: bytes 5-24/*\r\nContent-Length: 20\r\n\r\n wor");
        var status = await StatusAsync(harness, uploadId, "*");

        Assert.AreEqual(BadRequestError, response);
        StringAssert.Contains(status, "Range: bytes=0-4\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_ATotalPastMaxFilesize_Is413AndEndsTheUpload()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable with { MaxUploadBytes = 8 });
        harness.CreateFolder("photos");
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt"));

        var response = await ChunkAsync(harness, uploadId, "hello", "Content-Range: bytes 0-4/11");
        var again = await ChunkAsync(harness, uploadId, "hello", "Content-Range: bytes 0-4/*");

        Assert.AreEqual(TooLargeError, response);
        Assert.AreEqual(NotFound, again);
    }

    [TestMethod]
    public async Task ServeAsync_ChunksPastMaxFilesize_Are413AndEndTheUpload()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable with { MaxUploadBytes = 8 });
        harness.CreateFolder("photos");
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt"));
        await ChunkAsync(harness, uploadId, "hello", "Content-Range: bytes 0-4/*");

        var response = await ChunkAsync(harness, uploadId, "world", "Content-Range: bytes 5-9/*");
        var again = await StatusAsync(harness, uploadId, "*");

        Assert.AreEqual(TooLargeError, response);
        Assert.AreEqual(NotFound, again);
        Assert.IsEmpty(harness.FileSystem.EnumerateDirectoryEntryNames(Path.Join(GcsServerHarness.Root, "photos")));
    }

    [TestMethod]
    public async Task ServeAsync_OpenDeclaringASizePastMaxFilesize_Is413()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable with { MaxUploadBytes = 8 });
        harness.CreateFolder("photos");

        var response = await OpenAsync(harness, "uploadType=resumable&name=b.txt", string.Empty, "X-Upload-Content-Length: 9");

        Assert.AreEqual(TooLargeError, response);
    }

    [TestMethod]
    public async Task ServeAsync_WithoutAllowUploads_EveryRequestIs403Forbidden()
    {
        var harness = HarnessWithHello(new ContentExposureOptions { ListDirectories = true });

        Assert.AreEqual(ForbiddenError, await OpenAsync(harness, "uploadType=resumable&name=b.txt"));
        Assert.AreEqual(ForbiddenError, await ChunkAsync(harness, "0123456789abcdef0123456789abcdef", "hello"));
        Assert.AreEqual(ForbiddenError, await CancelAsync(harness, "0123456789abcdef0123456789abcdef"));
    }

    [TestMethod]
    [DataRow("uploadType=resumable", "", "400 Bad Request", "required", DisplayName = "no name")]
    [DataRow("uploadType=resumable&name=dir%2F", "", "400 Bad Request", "invalid", DisplayName = "a name ending in a slash")]
    [DataRow("uploadType=resumable&name=b.txt", "[1]", "400 Bad Request", "invalid", DisplayName = "metadata that is no object")]
    [DataRow("uploadType=resumable&name=b.txt&ifGenerationMatch=abc", "", "400 Bad Request", "invalidParameter", DisplayName = "a condition that is no number")]
    [DataRow("uploadType=resumable&name=b.txt&ifGenerationMatch=5", "", "412 Precondition Failed", "conditionNotMet", DisplayName = "a failing condition on a new name")]
    [DataRow("uploadType=resumable&name=a.txt&ifGenerationMatch=0", "", "412 Precondition Failed", "conditionNotMet", DisplayName = "a none condition on an object that exists")]
    [DataRow("uploadType=resumable&name=a.txt%2Fx", "", "409 Conflict", "conflict", DisplayName = "a file in the way of a folder")]
    public async Task ServeAsync_OpenOfABadRequest_IsRefusedAndOpensNothing(string query, string body, string status, string reason)
    {
        var harness = HarnessWithHello();

        var response = await OpenAsync(harness, query, body);

        StringAssert.StartsWith(response, $"HTTP/1.1 {status}\r\n");
        StringAssert.Contains(response, $"\"reason\":\"{reason}\"");
    }

    [TestMethod]
    public async Task ServeAsync_OpenWithAMetResourceCondition_IsAccepted()
    {
        var harness = HarnessWithHello();

        var response = await OpenAsync(harness, $"uploadType=resumable&name=a.txt&ifGenerationMatch={GcsServerHarness.NowGeneration}");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_OpenWithoutContentLengthOrWithABadDeclaredLength_IsRefused()
    {
        var harness = HarnessWithHello();

        var noLength = await harness.SendWithBearerAsync("POST", $"{Upload}?uploadType=resumable&name=b.txt");
        var badLength = await OpenAsync(harness, "uploadType=resumable&name=b.txt", string.Empty, "X-Upload-Content-Length: abc");

        Assert.AreEqual(JsonError("411 Length Required", "Length required.", "lengthRequired"), noLength);
        Assert.AreEqual(InvalidError, badLength);
    }

    [TestMethod]
    public async Task ServeAsync_OpenWithMetadataPast8KiBOr1MiB_Is400Invalid()
    {
        var harness = HarnessWithHello();
        var custom = $"{{\"name\":\"b.txt\",\"metadata\":{{\"k\":\"{new string('v', 8200)}\"}}}}";
        var huge = $"{{\"name\":\"b.txt\",\"x\":\"{new string('v', GcsJsonObjectUpdate.MaxBodyBytes)}\"}}";

        Assert.AreEqual(InvalidError, await OpenAsync(harness, "uploadType=resumable", custom));
        Assert.AreEqual(InvalidError, await OpenAsync(harness, "uploadType=resumable", huge));
    }

    [TestMethod]
    public async Task ServeAsync_OpenWithXGoogMetaFieldsPast8KiB_Is400Invalid()
    {
        var harness = HarnessWithHello();

        var response = await OpenAsync(harness, "uploadType=resumable&name=b.txt", string.Empty, $"x-goog-meta-big: {new string('v', 8200)}");

        Assert.AreEqual(InvalidError, response);
    }

    [TestMethod]
    public async Task ServeAsync_OpenWithoutAChunkedLengthBody_UsesTheAnnouncedLength()
    {
        var harness = HarnessWithHello();

        var response = await harness.SendRawAsync(
            $"POST {Upload}?uploadType=resumable&name=b.txt HTTP/1.1\r\nHost: x\r\n{GcsServerHarness.BearerField}\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n\r\n");

        Assert.AreEqual(JsonError("411 Length Required", "Length required.", "lengthRequired"), response);
    }

    [TestMethod]
    public async Task ServeAsync_OpenInAReadOnlyShare_Is403Forbidden()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        var share = new ContentShare("shared", GcsServerHarness.Root, shareFileSystem) { IsReadOnly = true };
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, shares: [share]);

        var response = await harness.SendWithBearerAndBodyAsync("POST", "/upload/storage/v1/b/shared/o?uploadType=resumable&name=b.txt", string.Empty);

        Assert.AreEqual(ForbiddenError, response);
    }

    [TestMethod]
    public async Task ServeAsync_ACommitTheStoreRefuses_Is403ForbiddenAndStoresNothing()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, storeFaults: new ScriptedFaultPlan(new FaultRule("full", FaultKind.OutOfSpace)));
        harness.CreateFolder("photos");
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt"));

        var response = await ChunkAsync(harness, uploadId, "hello world");

        Assert.AreEqual(ForbiddenError, response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ACompletionWhenTheMetadataIndexIsFull_Is403QuotaExceeded()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, maxIndexEntries: 1);
        harness.CreateFolder("photos");
        await harness.SendWithBearerAndBodyAsync("POST", $"{Upload}?uploadType=media&name=b.txt", "hello world");
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=c.txt"));

        var response = await ChunkAsync(harness, uploadId, "hello world");

        Assert.AreEqual(JsonError("403 Forbidden", "The object metadata index is full.", "quotaExceeded"), response);
        Assert.AreEqual(ContentEntryKind.None, harness.EntryKind("photos/c.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ChosenErrorOnAChunk_IsAskedWithTheInsertMethodId()
    {
        var harness = HarnessWithHello();
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=b.txt"));
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "gs", Reply = "503" });

        var response = await ChunkAsync(harness, uploadId, "hello world");

        StringAssert.StartsWith(response, "HTTP/1.1 503 Service Unavailable\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_ADeletedOpeningTarget_IsAnObjectOverwrittenAtCompletion()
    {
        var harness = HarnessWithHello();
        var uploadId = IdOf(await OpenAsync(harness, "uploadType=resumable&name=a.txt"));

        var response = await ChunkAsync(harness, uploadId, "HELLO WORLD");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("HELLO WORLD", harness.ReadFile("photos/a.txt"));
    }

    private static Task<string> OpenAsync(GcsServerHarness harness, string query, string body = "", params string[] fields) =>
        harness.SendWithBearerAndBodyAsync("POST", $"{Upload}?{query}", body, fields);

    private static Task<string> ChunkAsync(GcsServerHarness harness, string uploadId, string body, params string[] fields) =>
        harness.SendWithBearerAndBodyAsync("PUT", $"{Upload}?uploadType=resumable&upload_id={uploadId}", body, fields);

    private static Task<string> StatusAsync(GcsServerHarness harness, string uploadId, string total) =>
        harness.SendWithBearerAndBodyAsync("PUT", $"{Upload}?uploadType=resumable&upload_id={uploadId}", string.Empty, $"Content-Range: bytes */{total}");

    private static Task<string> CancelAsync(GcsServerHarness harness, string uploadId) =>
        harness.SendWithBearerAsync("DELETE", $"{Upload}?uploadType=resumable&upload_id={uploadId}", "Content-Length: 0");

    private static string IdOf(string response)
    {
        var match = SessionUri().Match(response);
        Assert.IsTrue(match.Success, response);

        return match.Groups[1].Value;
    }
}
