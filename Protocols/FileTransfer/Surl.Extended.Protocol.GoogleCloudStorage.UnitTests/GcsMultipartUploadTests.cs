using System.Text;
using Surl.Content;
using static Surl.Extended.Protocol.GoogleCloudStorage.GcsUploadRequests;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Initiating a multipart upload, uploading, listing and aborting its parts, from the XML API pages
/// "POST Object multipart", "PUT Object multipart", "GET Object multipart" and "DELETE Object
/// multipart" (ADR-0110, decision 9).
/// </summary>
[TestClass]
public sealed class GcsMultipartUploadTests
{
    private const string Namespace = "http://s3.amazonaws.com/doc/2006-03-01/";

    [TestMethod]
    public async Task ServeAsync_Initiate_AnswersInitiateMultipartUploadResult()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await InitiateAsync(harness, "/photos/big%20file.bin");
        var uploadId = response[(response.IndexOf("<UploadId>", StringComparison.Ordinal) + 10)..][..32];

        var body = $"<?xml version='1.0' encoding='UTF-8'?><InitiateMultipartUploadResult xmlns=\"{Namespace}\">"
            + $"<Bucket>photos</Bucket><Key>big file.bin</Key><UploadId>{uploadId}</UploadId></InitiateMultipartUploadResult>";
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{GcsServerHarness.CommonFields}Content-Type: application/xml; charset=UTF-8\r\n"
                + $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}",
            response);
        Assert.AreEqual(32, uploadId.Count(Uri.IsHexDigit));
    }

    [TestMethod]
    public async Task ServeAsync_PartsBeforeCompletion_AreInvisibleToAReadAndAListing()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/2026/big.bin");
        await UploadPartAsync(harness, "/photos/2026/big.bin", uploadId, 1, "hello");

        var read = await harness.SendWithBearerAsync("GET", "/photos/2026/big.bin");
        var listing = await harness.SendWithBearerAsync("GET", "/photos?list-type=2");

        Assert.AreEqual(GcsServerHarness.ExpectedError("404 Not Found", "NoSuchKey", "The specified key does not exist."), read);
        StringAssert.Contains(listing, "<KeyCount>0</KeyCount>");
        Assert.DoesNotContain("<Contents>", listing);
    }

    [TestMethod]
    public async Task ServeAsync_UploadPart_AnswersThePartsETagAndHashes()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello world", "Content-MD5: XrY7u+Ae7tCTyyK7j1rNww==");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{GcsServerHarness.CommonFields}ETag: \"5eb63bbbe01eeed093cb22bb8f5acdc3\"\r\n"
                + "x-goog-hash: crc32c=yZRlqg==,md5=XrY7u+Ae7tCTyyK7j1rNww==\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    [DataRow("0", DisplayName = "zero")]
    [DataRow("10001", DisplayName = "past 10000")]
    [DataRow("one", DisplayName = "not a number")]
    public async Task ServeAsync_UploadPartWithABadPartNumber_Is400InvalidArgument(string partNumber)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        var response = await harness.SendWithBearerAndBodyAsync("PUT", $"/photos/a.txt?partNumber={partNumber}&uploadId={uploadId}", "hello");

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "InvalidArgument", "Invalid argument."), response);
    }

    [TestMethod]
    public async Task ServeAsync_UploadPartWithoutContentLength_Is411MissingContentLength()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        var response = await harness.SendWithBearerAsync("PUT", $"/photos/a.txt?partNumber=1&uploadId={uploadId}");

        StringAssert.StartsWith(response, "HTTP/1.1 411 Length Required\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_UploadPartWithAMalformedContentMd5_Is400InvalidDigest()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello", "Content-MD5: AAAA");

        StringAssert.Contains(response, "<Code>InvalidDigest</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_UploadPartWithAMismatchedDigest_Is400BadDigestAndRecordsNoPart()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello", "Content-MD5: AAAAAAAAAAAAAAAAAAAAAA==");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 2, "world");
        var completed = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((2, ETagOf("world"))));

        StringAssert.Contains(response, "<Code>BadDigest</Code>");
        StringAssert.StartsWith(completed, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_UploadPartWhoseBodyEndsEarly_Is400IncompleteBody()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        var response = await harness.SendRawAsync($"PUT /photos/a.txt?partNumber=1&uploadId={uploadId} HTTP/1.1\r\nHost: x\r\n{GcsServerHarness.BearerField}\r\nContent-Length: 20\r\n\r\nhello");
        var parts = await ListPartsAsync(harness, "/photos/a.txt", uploadId);

        StringAssert.Contains(response, "<Code>IncompleteBody</Code>");
        Assert.DoesNotContain("<Part>", parts);
    }

    [TestMethod]
    public async Task ServeAsync_UploadPartAnnouncedPastMaxFilesize_Is400EntityTooLarge()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable with { MaxUploadBytes = 4 });
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");

        StringAssert.Contains(response, "<Code>EntityTooLarge</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_PartsTogetherPastMaxFilesize_Are400EntityTooLargeAndEndTheUpload()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable with { MaxUploadBytes = 8 });
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 2, "world");
        var parts = await ListPartsAsync(harness, "/photos/a.txt", uploadId);

        StringAssert.Contains(response, "<Code>EntityTooLarge</Code>");
        StringAssert.Contains(parts, "<Code>NoSuchUpload</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_UploadPartTwiceWithOneNumber_KeepsTheLaterPart()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "first");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "second");

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("second"))));

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("second", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_ListParts_AnswersListPartsResultInPartNumberOrder()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 3, "three");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "one");

        var response = await ListPartsAsync(harness, "/photos/a.txt", uploadId);

        var body = $"<?xml version='1.0' encoding='UTF-8'?><ListPartsResult xmlns=\"{Namespace}\"><Bucket>photos</Bucket><Key>a.txt</Key>"
            + $"<UploadId>{uploadId}</UploadId><StorageClass>STANDARD</StorageClass><PartNumberMarker>0</PartNumberMarker><MaxParts>1000</MaxParts>"
            + $"<IsTruncated>false</IsTruncated><Part><PartNumber>1</PartNumber><LastModified>{GcsServerHarness.NowListTime}</LastModified>"
            + $"<ETag>{ETagOf("one")}</ETag><Size>3</Size></Part><Part><PartNumber>3</PartNumber><LastModified>{GcsServerHarness.NowListTime}</LastModified>"
            + $"<ETag>{ETagOf("three")}</ETag><Size>5</Size></Part></ListPartsResult>";
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{GcsServerHarness.CommonFields}Content-Type: application/xml; charset=UTF-8\r\n"
                + $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_ListPartsPastMaxParts_IsTruncatedAtTheNextMarker()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        foreach (var number in new[] { 1, 2, 3 })
        {
            await UploadPartAsync(harness, "/photos/a.txt", uploadId, number, $"part {number}");
        }

        var first = await ListPartsAsync(harness, "/photos/a.txt", uploadId, "&max-parts=1&part-number-marker=1");
        var capped = await ListPartsAsync(harness, "/photos/a.txt", uploadId, "&max-parts=5000");

        StringAssert.Contains(first, "<PartNumberMarker>1</PartNumberMarker><NextPartNumberMarker>2</NextPartNumberMarker><MaxParts>1</MaxParts><IsTruncated>true</IsTruncated><Part><PartNumber>2</PartNumber>");
        Assert.DoesNotContain("<PartNumber>3</PartNumber>", first);
        StringAssert.Contains(capped, "<MaxParts>1000</MaxParts><IsTruncated>false</IsTruncated>");
    }

    [TestMethod]
    [DataRow("&max-parts=-1", DisplayName = "a negative max-parts")]
    [DataRow("&part-number-marker=x", DisplayName = "a marker that is not a number")]
    public async Task ServeAsync_ListPartsWithABadParameter_Is400InvalidArgument(string query)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        var response = await ListPartsAsync(harness, "/photos/a.txt", uploadId, query);

        StringAssert.Contains(response, "<Code>InvalidArgument</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_Abort_Is204AndEndsTheUpload()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");

        var response = await AbortAsync(harness, "/photos/a.txt", uploadId);
        var again = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 2, "world");

        Assert.AreEqual($"HTTP/1.1 204 No Content\r\n{GcsServerHarness.CommonFields}Connection: close\r\n\r\n", response);
        StringAssert.Contains(again, "<Code>NoSuchUpload</Code>");
        Assert.IsEmpty(harness.FileSystem.EnumerateDirectoryEntryNames(Path.Join(GcsServerHarness.Root, "photos")));
    }

    [TestMethod]
    [DataRow("PUT", "?partNumber=1&uploadId=0123456789abcdef0123456789abcdef", DisplayName = "upload a part")]
    [DataRow("GET", "?uploadId=0123456789abcdef0123456789abcdef", DisplayName = "list the parts")]
    [DataRow("DELETE", "?uploadId=0123456789abcdef0123456789abcdef", DisplayName = "abort")]
    [DataRow("POST", "?uploadId=0123456789abcdef0123456789abcdef", DisplayName = "complete")]
    public async Task ServeAsync_AnUnknownUploadId_Is404NoSuchUpload(string method, string query)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync(method, $"/photos/a.txt{query}", method == "POST" ? CompletionBody((1, ETagOf("a"))) : "hello");

        Assert.AreEqual(GcsServerHarness.ExpectedError("404 Not Found", "NoSuchUpload", "The specified upload does not exist."), response);
    }

    [TestMethod]
    public async Task ServeAsync_AnUploadIdOfAnotherObjectOrAResumableUpload_Is404NoSuchUpload()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var multipartId = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        var resumableId = await StartSessionAsync(harness, "/photos/b.txt");

        var otherObject = await UploadPartAsync(harness, "/photos/b.txt", multipartId, 1, "hello");
        var resumable = await UploadPartAsync(harness, "/photos/b.txt", resumableId, 1, "hello");

        StringAssert.Contains(otherObject, "<Code>NoSuchUpload</Code>");
        StringAssert.Contains(resumable, "<Code>NoSuchUpload</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_AnUploadIdOfTheSameNameInAnotherBucket_Is404NoSuchUpload()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.CreateFolder("videos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        var response = await UploadPartAsync(harness, "/videos/a.txt", uploadId, 1, "hello");

        StringAssert.Contains(response, "<Code>NoSuchUpload</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_AnEmptyPart_IsRecordedWithTheEmptyMd5()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        var response = await harness.SendWithBearerAsync("PUT", $"/photos/a.txt?partNumber=1&uploadId={uploadId}", "Content-Length: 0");

        StringAssert.Contains(response, "ETag: \"d41d8cd98f00b204e9800998ecf8427e\"\r\n");
    }

    [TestMethod]
    [DataRow("POST", "?uploads", DisplayName = "initiate")]
    [DataRow("PUT", "?partNumber=1&uploadId=0123456789abcdef0123456789abcdef", DisplayName = "upload a part")]
    [DataRow("DELETE", "?uploadId=0123456789abcdef0123456789abcdef", DisplayName = "abort")]
    [DataRow("POST", "?uploadId=0123456789abcdef0123456789abcdef", DisplayName = "complete")]
    public async Task ServeAsync_WithoutAllowUploads_Is403AccessDenied(string method, string query)
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendWithBearerAndBodyAsync(method, $"/photos/a.txt{query}", "hello");

        Assert.AreEqual(GcsServerHarness.ExpectedError("403 Forbidden", "AccessDenied", "Access denied."), response);
    }

    [TestMethod]
    public async Task ServeAsync_InitiateWithMetadataPast8KiB_Is400MetadataTooLarge()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await InitiateAsync(harness, "/photos/a.txt", $"x-goog-meta-big: {new string('x', 8192)}");

        StringAssert.Contains(response, "<Code>MetadataTooLarge</Code>");
    }

    [TestMethod]
    [DataRow("/photos/folder/", DisplayName = "a name ending in /")]
    [DataRow("/photos/a/%2e%2e/b.txt", DisplayName = "a name the store cannot hold")]
    public async Task ServeAsync_InitiateOfANameThatCannotBeAnUpload_Is400InvalidObjectName(string path)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await InitiateAsync(harness, path);

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>InvalidObjectName</Code>");
    }

    [TestMethod]
    [DataRow("/photos/a.txt/b.txt", DisplayName = "a file in the way")]
    [DataRow("/photos/sub", DisplayName = "a folder at the name")]
    public async Task ServeAsync_InitiateConflictingWithAnEntryOfTheOtherKind_Is409InvalidObjectName(string path)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos/sub");
        harness.WriteFile("photos/a.txt", "hello");

        var response = await InitiateAsync(harness, path);

        StringAssert.StartsWith(response, "HTTP/1.1 409 Conflict\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_InitiateInAReadOnlyShare_Is403AccessDenied()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(GcsServerHarness.Now));
        var share = new ContentShare("shared", GcsServerHarness.Root, shareFileSystem) { IsReadOnly = true };
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, shares: [share]);

        var response = await InitiateAsync(harness, "/shared/a.txt");

        StringAssert.Contains(response, "<Code>AccessDenied</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_InitiatePast1000OpenUploads_Is503SlowDown()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        for (var count = 0; count < GcsStagedUploads.MaxOpenUploads; count++)
        {
            await InitiateUploadIdAsync(harness, "/photos/a.txt");
        }

        var response = await InitiateAsync(harness, "/photos/a.txt");

        Assert.AreEqual(GcsServerHarness.ExpectedError("503 Service Unavailable", "SlowDown", "Please reduce your request rate."), response);
    }

    [TestMethod]
    public async Task ServeAsync_AnUploadIdlePast24Hours_IsDiscarded()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        var idle = await InitiateUploadIdAsync(harness, "/photos/a.txt");
        harness.TimeProvider.UtcNow = GcsServerHarness.Now.AddHours(23);
        var touched = await InitiateUploadIdAsync(harness, "/photos/b.txt");
        harness.TimeProvider.UtcNow = GcsServerHarness.Now.AddHours(24).AddSeconds(1);

        var discarded = await ListPartsAsync(harness, "/photos/a.txt", idle);
        var kept = await ListPartsAsync(harness, "/photos/b.txt", touched);

        StringAssert.Contains(discarded, "<Code>NoSuchUpload</Code>");
        StringAssert.StartsWith(kept, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    [DataRow("POST", "/photos/a.txt?uploads&acl", "", DisplayName = "initiate with another parameter")]
    [DataRow("POST", "/photos/a.txt", "x-goog-resumable: resume", DisplayName = "x-goog-resumable other than start")]
    [DataRow("HEAD", "/photos/a.txt?uploadId=0123456789abcdef0123456789abcdef", "", DisplayName = "HEAD on an upload")]
    [DataRow("PUT", "/photos/a.txt?", "", DisplayName = "PUT with an empty query")]
    public async Task ServeAsync_AnotherUploadShape_Is400NotImplemented(string method, string target, string field)
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        string[] fields = field.Length == 0 ? ["Content-Length: 0"] : [field, "Content-Length: 0"];

        var response = await harness.SendWithBearerAsync(method, target, fields);

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_GetWithAnEmptyQuery_ReadsTheObject()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt?");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "hello");
    }
}
