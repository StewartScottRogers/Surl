using static Surl.Extended.Protocol.S3.S3MultipartRequests;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The open multipart uploads a server holds: idle ones aborted after 24 hours on the injected
/// clock (ADR-0108, decision 7).
/// </summary>
[TestClass]
public sealed class S3MultipartUploadsTests
{
    [TestMethod]
    public async Task ServeAsync_AnUploadIdleFor24Hours_IsStillOpen()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        harness.TimeProvider.Advance(S3MultipartUploads.IdleLifetime);

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_AnUploadIdlePast24Hours_IsAbortedAndItsPartsRemoved()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");
        harness.TimeProvider.Advance(S3MultipartUploads.IdleLifetime + TimeSpan.FromSeconds(1));

        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 2, "world");

        StringAssert.Contains(response, "<Code>NoSuchUpload</Code>");
        Assert.IsEmpty(harness.FileSystem.EnumerateDirectoryEntryNames(Path.Join(S3ServerHarness.Root, "photos")));
    }

    [TestMethod]
    public async Task ServeAsync_APartUploaded_KeepsTheUploadFromGoingIdle()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        harness.TimeProvider.Advance(TimeSpan.FromHours(20));
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");
        harness.TimeProvider.Advance(TimeSpan.FromHours(20));

        var response = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("hello"))));

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }
}
