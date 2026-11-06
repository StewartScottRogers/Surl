using System.Text;
using static Surl.Extended.Protocol.S3.S3MultipartRequests;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The S3 server's CreateMultipartUpload, whose bytes are written from the S3 API Reference's
/// "CreateMultipartUpload" page (ADR-0108, decisions 1, 3 and 7).
/// </summary>
[TestClass]
public sealed class S3CreateMultipartUploadTests
{
    [TestMethod]
    public async Task ServeAsync_CreateMultipartUpload_AnswersInitiateMultipartUploadResult()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("example-bucket");

        var response = await CreateAsync(harness, "/example-bucket/example%20object");
        var uploadId = await CreateUploadIdAsync(harness, "/example-bucket/other");

        var body = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<InitiateMultipartUploadResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\">"
            + $"<Bucket>example-bucket</Bucket><Key>example object</Key><UploadId>{response[(response.IndexOf("<UploadId>", StringComparison.Ordinal) + 10)..][..32]}</UploadId></InitiateMultipartUploadResult>";
        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n"
                + $"Date: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\n"
                + $"Content-Type: application/xml\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}",
            response);
        StringAssert.DoesNotMatch(response, new System.Text.RegularExpressions.Regex(uploadId));
    }

    [TestMethod]
    public async Task ServeAsync_KeyInNewFolders_CreatesTheFolders()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        await CreateUploadIdAsync(harness, "/photos/2026/11/a.txt");

        Assert.AreEqual(Surl.Content.ContentEntryKind.Directory, harness.FileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "photos", "2026", "11")));
    }

    [TestMethod]
    public async Task ServeAsync_UploadsOff_Is403AccessDenied()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");

        var response = await CreateAsync(harness, "/photos/a.txt");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "403 Forbidden",
                "<Code>AccessDenied</Code><Message>Access Denied</Message><Resource>/photos/a.txt</Resource><RequestId>{RequestId}</RequestId>"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousUnderAllowAnonymous_Is403AccessDenied()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, policy: new UnitTestS3AuthenticationPolicy(UnitTestUnsignedRequests.Anonymous));
        harness.CreateFolder("photos");

        var response = await CreateAsync(harness, "/photos/a.txt");

        StringAssert.Contains(response, "<Code>AccessDenied</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_UserMetadataPast2KiB_Is400MetadataTooLarge()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await CreateAsync(harness, "/photos/a.txt", $"x-amz-meta-big: {new string('x', 2048)}");

        StringAssert.Contains(response, "<Code>MetadataTooLarge</Code>");
    }

    [TestMethod]
    [DataRow("/photos/folder/")]
    [DataRow("/photos/a%3Ab")]
    public async Task ServeAsync_AKeyThatCannotHoldAnObject_Is400InvalidArgument(string path)
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await CreateAsync(harness, path);

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "<Code>InvalidArgument</Code>");
        StringAssert.Contains(response, "<ArgumentName>key</ArgumentName>");
    }

    [TestMethod]
    public async Task ServeAsync_AFileInTheKeysWay_Is409InvalidArgument()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "x");

        var response = await CreateAsync(harness, "/photos/a.txt/b.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 409 Conflict\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_AFolderAtTheKey_Is409InvalidArgument()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos/a");

        var response = await CreateAsync(harness, "/photos/a");

        StringAssert.StartsWith(response, "HTTP/1.1 409 Conflict\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_AKeyTooLong_Is400KeyTooLongError()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await CreateAsync(harness, $"/photos/{new string('k', 1025)}");

        StringAssert.Contains(response, "<Code>KeyTooLongError</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_The1001stOpenUpload_Is503SlowDown()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        for (var count = 0; count < S3MultipartUploads.MaxOpenUploads; count++)
        {
            await CreateUploadIdAsync(harness, $"/photos/{count}.bin");
        }

        var response = await CreateAsync(harness, "/photos/one-more.bin");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "503 Service Unavailable",
                "<Code>SlowDown</Code><Message>Please reduce your request rate.</Message><Resource>/photos/one-more.bin</Resource><RequestId>{RequestId}</RequestId>"),
            response);
    }
}
