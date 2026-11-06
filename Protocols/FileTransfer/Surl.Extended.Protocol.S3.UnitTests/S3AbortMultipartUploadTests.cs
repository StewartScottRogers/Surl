using static Surl.Extended.Protocol.S3.S3MultipartRequests;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The S3 server's AbortMultipartUpload, whose bytes are written from the S3 API Reference's
/// "AbortMultipartUpload" page (ADR-0108, decision 1).
/// </summary>
[TestClass]
public sealed class S3AbortMultipartUploadTests
{
    [TestMethod]
    public async Task ServeAsync_AbortMultipartUpload_Answers204AndRemovesTheParts()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");

        var response = await AbortAsync(harness, "/photos/a.txt", uploadId);
        var after = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 2, "world");

        Assert.AreEqual(
            "HTTP/1.1 204 No Content\r\n"
                + $"Date: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\nConnection: close\r\n\r\n",
            response);
        Assert.IsEmpty(harness.FileSystem.EnumerateDirectoryEntryNames(System.IO.Path.Join(S3ServerHarness.Root, "photos")));
        StringAssert.Contains(after, "<Code>NoSuchUpload</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_AnUnknownUploadId_Is404NoSuchUpload()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");

        var response = await AbortAsync(harness, "/photos/a.txt", "0123456789abcdef0123456789abcdef");

        StringAssert.Contains(response, "<Code>NoSuchUpload</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_UploadsOff_Is403AccessDenied()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");

        var response = await AbortAsync(harness, "/photos/a.txt", "0123456789abcdef0123456789abcdef");

        StringAssert.Contains(response, "<Code>AccessDenied</Code>");
    }
}
