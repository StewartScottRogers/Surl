using Surl.Content;
using static Surl.Extended.Protocol.S3.S3MultipartRequests;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The S3 server's UploadPart, whose bytes are written from the S3 API Reference's "UploadPart"
/// page (ADR-0108, decisions 1 and 7).
/// </summary>
[TestClass]
public sealed class S3UploadPartTests
{
    [TestMethod]
    public async Task ServeAsync_UploadPart_Answers200WithThePartsMd5AsItsETag()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello world");

        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n"
                + $"Date: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\n"
                + "ETag: \"5eb63bbbe01eeed093cb22bb8f5acdc3\"\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_AStagedPart_IsNotVisibleThroughTheContentStoreBeforeCompletion()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello world");

        var mapping = harness.Store.MapRequestPath("/photos/a.txt");
        var listing = harness.Store.ListDirectory(harness.Store.MapRequestPath("/photos/"), CancellationToken.None);
        var get = await harness.SendAsync("GET", "/photos/a.txt");

        Assert.AreEqual(ContentEntryKind.None, harness.Store.GetEntryKind(mapping));
        Assert.IsEmpty(listing.Entries);
        StringAssert.StartsWith(get, "HTTP/1.1 404 Not Found\r\n");
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("10001")]
    [DataRow("-1")]
    [DataRow("one")]
    public async Task ServeAsync_APartNumberOutside1To10000_Is400InvalidArgument(string partNumber)
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await harness.SendWithBodyAsync("PUT", $"/photos/a.txt?partNumber={partNumber}&uploadId={uploadId}", "hello");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "400 Bad Request",
                $"<Code>InvalidArgument</Code><Message>Invalid Argument</Message><ArgumentName>partNumber</ArgumentName><ArgumentValue>{partNumber}</ArgumentValue>"
                    + "<Resource>/photos/a.txt</Resource><RequestId>{RequestId}</RequestId>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_PartNumber10000_IsStored()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 10000, "hello");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_AnUnknownUploadId_Is404NoSuchUpload()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await UploadPartAsync(harness, "/photos/a.txt", "0123456789abcdef0123456789abcdef", 1, "hello");

        StringAssert.StartsWith(response, "HTTP/1.1 404 Not Found\r\n");
        StringAssert.Contains(response, "<Code>NoSuchUpload</Code>");
        StringAssert.Contains(response, "<UploadId>0123456789abcdef0123456789abcdef</UploadId>");
    }

    [TestMethod]
    public async Task ServeAsync_TheUploadIdOfAnotherKey_Is404NoSuchUpload()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await UploadPartAsync(harness, "/photos/b.txt", uploadId, 1, "hello");

        StringAssert.Contains(response, "<Code>NoSuchUpload</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_TheUploadIdOfAnotherBucket_Is404NoSuchUpload()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.CreateFolder("other");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await UploadPartAsync(harness, "/other/a.txt", uploadId, 1, "hello");

        StringAssert.Contains(response, "<Code>NoSuchUpload</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_AnEmptyPart_IsStoredWithTheEmptyBodysETag()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, string.Empty);

        StringAssert.Contains(response, "ETag: \"d41d8cd98f00b204e9800998ecf8427e\"\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_UploadsOff_Is403AccessDenied()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");

        var response = await UploadPartAsync(harness, "/photos/a.txt", "0123456789abcdef0123456789abcdef", 1, "hello");

        StringAssert.Contains(response, "<Code>AccessDenied</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_AMalformedContentMd5_Is400InvalidDigest()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello", "Content-MD5: nope");

        StringAssert.Contains(response, "<Code>InvalidDigest</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_AMismatchedContentMd5_Is400BadDigestAndThePartIsNotRecorded()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello", "Content-MD5: XrY7u+Ae7tCTyyK7j1rNww==");
        var complete = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("hello"))));

        StringAssert.Contains(response, "<Code>BadDigest</Code>");
        StringAssert.Contains(complete, "<Code>InvalidPart</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_ABodyCutShort_Is400IncompleteBodyAndTheUploadStaysOpen()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await harness.SendRawAsync($"PUT /photos/a.txt?partNumber=1&uploadId={uploadId} HTTP/1.1\r\nHost: s3.test\r\nContent-Length: 100\r\n\r\nhello");
        var retried = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");

        StringAssert.Contains(response, "<Code>IncompleteBody</Code>");
        StringAssert.StartsWith(retried, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_APartAnnouncedPastMaxFilesize_Is400EntityTooLarge()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable with { MaxUploadBytes = 4 });
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "400 Bad Request",
                "<Code>EntityTooLarge</Code><Message>Your proposed upload exceeds the maximum allowed object size.</Message><Resource>/photos/a.txt</Resource><RequestId>{RequestId}</RequestId>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_PartsTogetherPastMaxFilesize_Are400EntityTooLargeAndAbortTheUpload()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable with { MaxUploadBytes = 8 });
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 2, "world");
        var after = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 3, "!");

        StringAssert.Contains(response, "<Code>EntityTooLarge</Code>");
        StringAssert.Contains(after, "<Code>NoSuchUpload</Code>");
        Assert.IsEmpty(harness.FileSystem.EnumerateDirectoryEntryNames(Path.Join(S3ServerHarness.Root, "photos")));
    }

    [TestMethod]
    public async Task ServeAsync_ASignedBodyNotTheSignedHash_Is400XAmzContentSha256Mismatch()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello", "Authorization: AWS4-HMAC-SHA256 signed", $"x-amz-content-sha256: {UnitTestS3AuthenticationPolicy.ContentSha256Of("other")}");

        StringAssert.Contains(response, "<Code>XAmzContentSHA256Mismatch</Code>");
    }
}
