using Surl.Content;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Where the S3 answerer finds an object's properties, and how it leaves a request body.
/// </summary>
[TestClass]
public sealed class S3RequestAnswererTests
{
    [TestMethod]
    public async Task AnswerAsync_ObjectInAShare_AnswersTheIndexDefaults()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(S3ServerHarness.Now));
        S3ServerHarness.WriteFile(shareFileSystem, "a.txt", "hello world");
        var harness = new S3ServerHarness(shares: [new ContentShare("shared", S3ServerHarness.Root, shareFileSystem)]);

        var response = await harness.SendAsync("GET", "/shared/a.txt");

        StringAssert.Contains(response, "ETag: \"5eb63bbbe01eeed093cb22bb8f5acdc3\"\r\nAccept-Ranges: bytes\r\nContent-Type: application/octet-stream\r\nContent-Length: 11\r\n");
        StringAssert.EndsWith(response, "hello world");
    }

    [TestMethod]
    public async Task AnswerAsync_ObjectBehindABucketName_IsServedFromTheBucketsFolder()
    {
        var harness = new S3ServerHarness(buckets: [new ContentBucket("media", "/store/photos")]);
        harness.CreateFolder("store");
        harness.CreateFolder("store/photos");
        harness.WriteFile("store/photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/media/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "hello world");
    }

    [TestMethod]
    public async Task AnswerAsync_IndexThatCannotBeWritten_ServesTheObjectAndNotesTheFailure()
    {
        var harness = new S3ServerHarness(maxTotalBytes: 11, persistsIndex: true);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/photos/a.txt");

        StringAssert.EndsWith(response, "hello world");
        Assert.IsTrue(harness.Log.Notes.Any(note => note.StartsWith("The object metadata index could not be written: ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task AnswerAsync_RequestWithABody_IsAnsweredAndTheConnectionClosed()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendRawAsync("GET /photos/a.txt HTTP/1.1\r\nHost: s3.test\r\nContent-Length: 3\r\n\r\nabc");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(response, "Connection: close\r\n");
    }

    [TestMethod]
    public async Task AnswerAsync_UnsupportedOperation_IsNoted()
    {
        var harness = new S3ServerHarness();

        await harness.SendAsync("DELETE", "/photos/a.txt");

        CollectionAssert.Contains(harness.Log.Notes.ToArray(), "DELETE is no operation the S3 server serves; answered 501 NotImplemented.");
    }
}
