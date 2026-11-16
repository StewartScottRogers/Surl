using static Surl.Extended.Protocol.GoogleCloudStorage.GcsUploadRequests;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Aborting every open upload at once, as a reset or restore does (ADR-0167, decision 2; BL-1231).
/// </summary>
[TestClass]
public sealed class GcsAbortOpenUploadsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AbortOpenUploadsAsync_AResumableUpload_MakesALaterPutAnswer404AndLeavesNoTemporaryFile()
    {
        var harness = Writable();
        var uploadId = await StartSessionAsync(harness, "/photos/a.txt");
        await PutChunkAsync(harness, "/photos/a.txt", uploadId, "hello ", "Content-Range: bytes 0-5/*");

        await harness.Server.AbortOpenUploadsAsync(TestContext.CancellationToken);
        var response = await PutChunkAsync(harness, "/photos/a.txt", uploadId, "world", "Content-Range: bytes 6-10/11");

        StringAssert.StartsWith(response, "HTTP/1.1 404 Not Found\r\n");
        StringAssert.Contains(response, "<Code>NoSuchUpload</Code>");
        Assert.IsEmpty(harness.FileSystem.EnumerateDirectoryEntryNames(Path.Join(GcsServerHarness.Root, "photos")));
    }

    [TestMethod]
    public async Task AbortOpenUploadsAsync_AMultipartUpload_MakesAPartAnswerNoSuchUpload()
    {
        var harness = Writable();
        var uploadId = await InitiateUploadIdAsync(harness, "/photos/a.txt");

        await harness.Server.AbortOpenUploadsAsync(TestContext.CancellationToken);
        var response = await UploadPartAsync(harness, "/photos/a.txt", uploadId, 1, "x");

        StringAssert.Contains(response, "<Code>NoSuchUpload</Code>");
    }

    [TestMethod]
    public async Task AbortOpenUploadsAsync_AThousandOpenUploads_FreesEverySlot()
    {
        var harness = Writable();
        for (var count = 0; count < GcsStagedUploads.MaxOpenUploads; count++)
        {
            await InitiateUploadIdAsync(harness, "/photos/a.txt");
        }

        await harness.Server.AbortOpenUploadsAsync(TestContext.CancellationToken);
        var response = await InitiateAsync(harness, "/photos/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task AbortOpenUploadsAsync_NothingOpen_Completes()
    {
        await Writable().Server.AbortOpenUploadsAsync(TestContext.CancellationToken);
    }

    private static GcsServerHarness Writable()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        return harness;
    }
}
