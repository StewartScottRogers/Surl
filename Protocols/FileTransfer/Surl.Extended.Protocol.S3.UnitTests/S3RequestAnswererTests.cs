using Surl.Content;
using Surl.Protocol.Abstractions;

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

        StringAssert.Contains(response, "ETag: \"5eb63bbbe01eeed093cb22bb8f5acdc3\"\r\nAccept-Ranges: bytes\r\nContent-Type: text/plain\r\nContent-Length: 11\r\n");
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

        await harness.SendAsync("PATCH", "/photos/a.txt");

        CollectionAssert.Contains(harness.Log.Notes.ToArray(), "PATCH is no operation the S3 server serves; answered 501 NotImplemented.");
    }

    [TestMethod]
    public async Task RequestId_SameSeededIdentifiers_IsTheSameEveryRun()
    {
        var first = new S3ServerHarness { Seed = 7 };
        var second = new S3ServerHarness { Seed = 7 };
        var other = new S3ServerHarness { Seed = 8 };

        var firstResponse = await first.SendAsync("PATCH", "/photos/a.txt");
        var secondResponse = await second.SendAsync("PATCH", "/photos/a.txt");
        var otherResponse = await other.SendAsync("PATCH", "/photos/a.txt");

        Assert.AreEqual(S3ServerHarness.RequestIdOf(firstResponse), S3ServerHarness.RequestIdOf(secondResponse));
        Assert.AreNotEqual(S3ServerHarness.RequestIdOf(firstResponse), S3ServerHarness.RequestIdOf(otherResponse));
    }

    [TestMethod]
    public async Task RefuseHeadNotRead_SameSeededIdentifiers_IsTheSameEveryRun()
    {
        var first = new S3ServerHarness { Seed = 7 };
        var second = new S3ServerHarness { Seed = 7 };
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = 64 };
        var request = $"GET /photos/a.txt HTTP/1.1\r\nHost: s3.test\r\nX-Padding: {new string('p', 100)}\r\n\r\n";

        var firstResponse = await first.SendRawAsync(request, limits);
        var secondResponse = await second.SendRawAsync(request, limits);

        Assert.AreEqual(S3ServerHarness.RequestIdOf(firstResponse), S3ServerHarness.RequestIdOf(secondResponse));
    }
}
