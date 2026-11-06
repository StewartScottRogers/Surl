using Surl.Content;
using static Surl.Extended.Protocol.GoogleCloudStorage.GcsUploadRequests;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Resumable uploads, from the page "Perform resumable uploads" (XML API) and "Resumable uploads"
/// (ADR-0110, decision 9).
/// </summary>
[TestClass]
public sealed class GcsResumableUploadTests
{
    private const string HelloWorldETag = "\"5eb63bbbe01eeed093cb22bb8f5acdc3\"";
    private const string HelloWorldHash = "crc32c=yZRlqg==,md5=XrY7u+Ae7tCTyyK7j1rNww==";

    [TestMethod]
    public async Task ServeAsync_Start_Is201WithTheSessionUriInLocation()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync("POST", "/photos/a.txt", "x-goog-resumable: start", "Content-Length: 0");
        var uploadId = response[(response.IndexOf("upload_id=", StringComparison.Ordinal) + 10)..][..32];

        Assert.AreEqual(
            $"HTTP/1.1 201 Created\r\n{GcsServerHarness.CommonFields}Location: http://127.0.0.1:4443/photos/a.txt?upload_id={uploadId}\r\n"
                + "Content-Length: 0\r\nConnection: close\r\n\r\n",
            response);
        Assert.AreEqual(32, uploadId.Count(Uri.IsHexDigit));
    }

    [TestMethod]
    public async Task ServeAsync_TwoChunks_AreAnswered308WithRangeThen200AndReadBack()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await StartSessionAsync(harness, "/photos/2026/a.txt", "Content-Type: text/plain", "x-goog-meta-colour: blue");

        var first = await PutChunkAsync(harness, "/photos/2026/a.txt", uploadId, "hello ", "Content-Range: bytes 0-5/*");
        var invisible = await harness.SendWithBearerAsync("GET", "/photos/2026/a.txt");
        harness.TimeProvider.UtcNow = GcsServerHarness.Now.AddSeconds(1);
        var last = await PutChunkAsync(harness, "/photos/2026/a.txt", uploadId, "world", "Content-Range: bytes 6-10/11");
        var read = await harness.SendWithBearerAsync("GET", "/photos/2026/a.txt");

        Assert.AreEqual(
            $"HTTP/1.1 308 Resume Incomplete\r\n{GcsServerHarness.CommonFields}Range: bytes=0-5\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            first);
        StringAssert.Contains(invisible, "<Code>NoSuchKey</Code>");
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\nDate: Thu, 05 Nov 2026 12:00:01 GMT\r\nServer: surl\r\nETag: {HelloWorldETag}\r\nx-goog-generation: 1793880001000000\r\n"
                + $"x-goog-metageneration: 1\r\nx-goog-hash: {HelloWorldHash}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            last);
        StringAssert.Contains(read, "Content-Type: text/plain\r\n");
        StringAssert.Contains(read, "x-goog-meta-colour: blue\r\n");
        StringAssert.EndsWith(read, "hello world");
        Assert.HasCount(1, harness.FileSystem.EnumerateDirectoryEntryNames(Path.Join(GcsServerHarness.Root, "photos/2026")));
    }

    [TestMethod]
    public async Task ServeAsync_OnePutWithoutContentRange_IsTheWholeObject()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await StartSessionAsync(harness, "/photos/a.txt");

        var response = await PutChunkAsync(harness, "/photos/a.txt", uploadId, "hello world");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_AnEmptyPutWithoutContentRange_StoresAnEmptyObject()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await StartSessionAsync(harness, "/photos/a.txt");

        var response = await harness.SendWithBearerAsync("PUT", $"/photos/a.txt?upload_id={uploadId}", "Content-Length: 0");

        StringAssert.Contains(response, "x-goog-hash: crc32c=AAAAAA==,md5=1B2M2Y8AsgTpgAmY7PhCfg==\r\n");
        Assert.AreEqual(string.Empty, harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_AStatusCheck_Is308WithTheBytesPersistedOrNoRangeWhenNone()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await StartSessionAsync(harness, "/photos/a.txt");

        var none = await harness.SendWithBearerAsync("PUT", $"/photos/a.txt?upload_id={uploadId}", "Content-Range: bytes */*", "Content-Length: 0");
        await PutChunkAsync(harness, "/photos/a.txt", uploadId, "hello", "Content-Range: bytes 0-4/*");
        var some = await harness.SendWithBearerAsync("PUT", $"/photos/a.txt?upload_id={uploadId}", "Content-Range: bytes */11", "Content-Length: 0");

        Assert.AreEqual($"HTTP/1.1 308 Resume Incomplete\r\n{GcsServerHarness.CommonFields}Content-Length: 0\r\nConnection: close\r\n\r\n", none);
        StringAssert.Contains(some, "Range: bytes=0-4\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_AStatusCheckNamingTheBytesInAsTheTotal_FinishesTheUpload()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await StartSessionAsync(harness, "/photos/a.txt");
        await PutChunkAsync(harness, "/photos/a.txt", uploadId, "hello", "Content-Range: bytes 0-4/*");

        var response = await harness.SendWithBearerAsync("PUT", $"/photos/a.txt?upload_id={uploadId}", "Content-Range: bytes */5", "Content-Length: 0");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("hello", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_AChunkNotStartingAtTheNextByte_IsNotReadAndSaysWhereToResume()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await StartSessionAsync(harness, "/photos/a.txt");
        await PutChunkAsync(harness, "/photos/a.txt", uploadId, "hello", "Content-Range: bytes 0-4/*");

        var gap = await PutChunkAsync(harness, "/photos/a.txt", uploadId, "world", "Content-Range: bytes 6-10/*");
        var finished = await PutChunkAsync(harness, "/photos/a.txt", uploadId, " world", "Content-Range: bytes 5-10/11");

        StringAssert.StartsWith(gap, "HTTP/1.1 308 Resume Incomplete\r\n");
        StringAssert.Contains(gap, "Range: bytes=0-4\r\n");
        StringAssert.StartsWith(finished, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ATotalBelowTheBytesIn_Is400InvalidArgument()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await StartSessionAsync(harness, "/photos/a.txt");
        await PutChunkAsync(harness, "/photos/a.txt", uploadId, "hello", "Content-Range: bytes 0-4/*");

        var response = await harness.SendWithBearerAsync("PUT", $"/photos/a.txt?upload_id={uploadId}", "Content-Range: bytes */3", "Content-Length: 0");

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "InvalidArgument", "Invalid argument."), response);
    }

    [TestMethod]
    [DataRow("Content-Range: bytes 0-9/*", DisplayName = "a chunk longer than the body")]
    [DataRow("Content-Range: pages 0-4/*", DisplayName = "a range that does not parse")]
    public async Task ServeAsync_AContentRangeThatIsNotTheBody_Is400InvalidArgument(string field)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await StartSessionAsync(harness, "/photos/a.txt");

        var response = await PutChunkAsync(harness, "/photos/a.txt", uploadId, "hello", field);

        StringAssert.Contains(response, "<Code>InvalidArgument</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_ASessionDeclaredPastMaxFilesize_Is400EntityTooLargeAndEndsTheUpload()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable with { MaxUploadBytes = 8 });
        harness.CreateFolder("photos");
        var uploadId = await StartSessionAsync(harness, "/photos/a.txt");

        var response = await PutChunkAsync(harness, "/photos/a.txt", uploadId, "hello", "Content-Range: bytes 0-4/11");
        var again = await PutChunkAsync(harness, "/photos/a.txt", uploadId, "hello", "Content-Range: bytes 0-4/*");

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "EntityTooLarge", "Your proposed upload exceeds the maximum allowed object size."), response);
        StringAssert.Contains(again, "<Code>NoSuchUpload</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_ChunksPastMaxFilesize_Are400EntityTooLargeAndEndTheUpload()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable with { MaxUploadBytes = 8 });
        harness.CreateFolder("photos");
        var uploadId = await StartSessionAsync(harness, "/photos/a.txt");
        await PutChunkAsync(harness, "/photos/a.txt", uploadId, "hello", "Content-Range: bytes 0-4/*");

        var response = await PutChunkAsync(harness, "/photos/a.txt", uploadId, "world", "Content-Range: bytes 5-9/*");
        var again = await harness.SendWithBearerAsync("PUT", $"/photos/a.txt?upload_id={uploadId}", "Content-Range: bytes */*", "Content-Length: 0");

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "EntityTooLarge", "Your proposed upload exceeds the maximum allowed object size."), response);
        StringAssert.Contains(again, "<Code>NoSuchUpload</Code>");
        Assert.IsEmpty(harness.FileSystem.EnumerateDirectoryEntryNames(Path.Join(GcsServerHarness.Root, "photos")));
    }

    [TestMethod]
    public async Task ServeAsync_AChunkThatEndsEarly_Is400IncompleteBodyAndKeepsTheBytesBefore()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await StartSessionAsync(harness, "/photos/a.txt");
        await PutChunkAsync(harness, "/photos/a.txt", uploadId, "hello", "Content-Range: bytes 0-4/*");

        var response = await harness.SendRawAsync(
            $"PUT /photos/a.txt?upload_id={uploadId} HTTP/1.1\r\nHost: x\r\n{GcsServerHarness.BearerField}\r\nContent-Range: bytes 5-24/*\r\nContent-Length: 20\r\n\r\n wor");
        var status = await harness.SendWithBearerAsync("PUT", $"/photos/a.txt?upload_id={uploadId}", "Content-Range: bytes */*", "Content-Length: 0");

        StringAssert.Contains(response, "<Code>IncompleteBody</Code>");
        StringAssert.Contains(status, "Range: bytes=0-4\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_AChunkWithoutContentLength_Is411MissingContentLength()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await StartSessionAsync(harness, "/photos/a.txt");

        var response = await harness.SendWithBearerAsync("PUT", $"/photos/a.txt?upload_id={uploadId}");

        StringAssert.StartsWith(response, "HTTP/1.1 411 Length Required\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_Cancel_Is204AndEndsTheUpload()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await StartSessionAsync(harness, "/photos/a.txt");
        await PutChunkAsync(harness, "/photos/a.txt", uploadId, "hello", "Content-Range: bytes 0-4/*");

        var response = await harness.SendWithBearerAsync("DELETE", $"/photos/a.txt?upload_id={uploadId}", "Content-Length: 0");
        var again = await harness.SendWithBearerAsync("DELETE", $"/photos/a.txt?upload_id={uploadId}", "Content-Length: 0");

        Assert.AreEqual($"HTTP/1.1 204 No Content\r\n{GcsServerHarness.CommonFields}Connection: close\r\n\r\n", response);
        StringAssert.Contains(again, "<Code>NoSuchUpload</Code>");
        Assert.IsEmpty(harness.FileSystem.EnumerateDirectoryEntryNames(Path.Join(GcsServerHarness.Root, "photos")));
    }

    [TestMethod]
    [DataRow("PUT", DisplayName = "a chunk")]
    [DataRow("DELETE", DisplayName = "a cancel")]
    public async Task ServeAsync_AnUnknownUploadId_Is404NoSuchUpload(string method)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var multipartId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        var response = await harness.SendWithBearerAndBodyAsync(method, $"/photos/a.txt?upload_id={multipartId}", "hello");

        Assert.AreEqual(GcsServerHarness.ExpectedError("404 Not Found", "NoSuchUpload", "The specified upload does not exist."), response);
    }

    [TestMethod]
    [DataRow("POST", "", "x-goog-resumable: start", DisplayName = "a start")]
    [DataRow("PUT", "?upload_id=0123456789abcdef0123456789abcdef", "Content-Length: 0", DisplayName = "a chunk")]
    [DataRow("DELETE", "?upload_id=0123456789abcdef0123456789abcdef", "Content-Length: 0", DisplayName = "a cancel")]
    public async Task ServeAsync_WithoutAllowUploads_Is403AccessDenied(string method, string query, string field)
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAsync(method, $"/photos/a.txt{query}", field);

        Assert.AreEqual(GcsServerHarness.ExpectedError("403 Forbidden", "AccessDenied", "Access denied."), response);
    }

    [TestMethod]
    public async Task ServeAsync_AFolderAtTheNameByTheLastChunk_Is403AccessDeniedAndEndsTheUpload()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await StartSessionAsync(harness, "/photos/a.txt");
        harness.CreateFolder("photos/a.txt");

        var response = await PutChunkAsync(harness, "/photos/a.txt", uploadId, "hello");
        var again = await PutChunkAsync(harness, "/photos/a.txt", uploadId, "hello");

        StringAssert.Contains(response, "<Code>AccessDenied</Code>");
        StringAssert.Contains(again, "<Code>NoSuchUpload</Code>");
        Assert.AreEqual(ContentEntryKind.Directory, harness.EntryKind("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_StartOverTls_GivesAnHttpsSessionUri()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendRawOverTlsAsync($"POST /photos/a.txt HTTP/1.1\r\nHost: gcs.test\r\n{GcsServerHarness.BearerField}\r\nx-goog-resumable: Start\r\nContent-Length: 0\r\n\r\n");

        StringAssert.Contains(response, "\r\nLocation: https://gcs.test/photos/a.txt?upload_id=");
    }
}
