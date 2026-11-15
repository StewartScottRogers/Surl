using static Surl.Extended.Protocol.S3.S3MultipartRequests;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Aborting every open multipart upload at once, as a reset or restore does (ADR-0167, decision 2; BL-1229).
/// </summary>
[TestClass]
public sealed class S3AbortOpenUploadsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AbortOpenUploadsAsync_AnOpenUpload_AnswersNoSuchUploadToPartAndCompleteAndLeavesNoTemporaryFile()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        var uploadId = await CreateUploadIdAsync(harness, "/photos/a.txt");
        await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "hello");

        await harness.Server.AbortOpenUploadsAsync(TestContext.CancellationToken);
        var part = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 2, "world");
        var complete = await CompleteAsync(harness, "/photos/a.txt", uploadId, CompletionBody((1, ETagOf("hello"))));

        StringAssert.Contains(part, "<Code>NoSuchUpload</Code>");
        StringAssert.Contains(complete, "<Code>NoSuchUpload</Code>");
        Assert.IsEmpty(harness.FileSystem.EnumerateDirectoryEntryNames(Path.Join(S3ServerHarness.Root, "photos")));
    }

    [TestMethod]
    public async Task AbortOpenUploadsAsync_With1000UploadsOpen_FreesEverySlot()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        for (var count = 0; count < S3MultipartUploads.MaxOpenUploads; count++)
        {
            await CreateUploadIdAsync(harness, $"/photos/{count}.bin");
        }

        await harness.Server.AbortOpenUploadsAsync(TestContext.CancellationToken);
        var response = await CreateAsync(harness, "/photos/one-more.bin");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task AbortOpenUploadsAsync_WithNoUploadOpen_Completes()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);

        await harness.Server.AbortOpenUploadsAsync(TestContext.CancellationToken);
    }
}
