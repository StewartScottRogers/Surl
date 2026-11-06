using Surl.Content;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The S3 server's HeadBucket (the S3 API Reference's "HeadBucket"; ADR-0108, decision 1).
/// </summary>
[TestClass]
public sealed class S3HeadBucketTests
{
    [TestMethod]
    public async Task ServeAsync_HeadBucket_Is200WithNoBody()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendAsync("HEAD", "/photos");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\nDate: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_HeadBucketVirtualHosted_Is200()
    {
        var harness = new S3ServerHarness(listenHost: "s3.test");
        harness.CreateFolder("photos");

        var response = await harness.SendRawAsync("HEAD / HTTP/1.1\r\nHost: photos.s3.test\r\nConnection: close\r\n\r\n");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_HeadBucketOfAMissingBucket_Is404WithNoBody()
    {
        var harness = new S3ServerHarness();

        var response = await harness.SendAsync("HEAD", "/photos");

        Assert.AreEqual(
            $"HTTP/1.1 404 Not Found\r\nDate: {S3ServerHarness.NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {S3ServerHarness.RequestIdOf(response)}\r\nContent-Type: application/xml\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task ServeAsync_HeadBucketOfATopLevelFile_Is404()
    {
        var harness = new S3ServerHarness();
        harness.WriteFile("photos", "not a bucket");

        var response = await harness.SendAsync("HEAD", "/photos");

        StringAssert.StartsWith(response, "HTTP/1.1 404 Not Found\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_HeadBucketOfAShareTheCallerMayNotRead_Is404WithNoBody()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(S3ServerHarness.Now));
        var share = new ContentShare("shared", S3ServerHarness.Root, shareFileSystem)
        {
            Grants = new Dictionary<string, ContentShareAccess> { ["someone-else"] = ContentShareAccess.Read },
        };
        var harness = new S3ServerHarness(shares: [share]);

        var response = await harness.SendAsync("HEAD", "/shared");

        StringAssert.StartsWith(response, "HTTP/1.1 404 Not Found\r\n");
        StringAssert.EndsWith(response, "\r\n\r\n");
    }
}
