namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Which <c>Host</c> fields address a bucket ("Virtual hosting of buckets"; ADR-0108, decision 2).
/// </summary>
[TestClass]
public sealed class S3VirtualHostTests
{
    [TestMethod]
    [DataRow("photos.s3.test:9000", "s3.test", "photos", DisplayName = "the listen host, with a port")]
    [DataRow("My.Photos.S3.Test.", "s3.test", "my.photos", DisplayName = "a dotted bucket, case folded, trailing dot")]
    [DataRow("photos.localhost", "127.0.0.1", "photos", DisplayName = "localhost")]
    [DataRow("photos.s3.amazonaws.com", "127.0.0.1", "photos", DisplayName = "the global AWS endpoint")]
    [DataRow("photos.s3.eu-west-2.amazonaws.com", "127.0.0.1", "photos", DisplayName = "a regional AWS endpoint")]
    public void BucketOf_VirtualHostedName_IsTheBucket(string authority, string listenHost, string bucket)
    {
        Assert.AreEqual(bucket, S3VirtualHost.BucketOf(authority, listenHost));
    }

    [TestMethod]
    [DataRow(null, "s3.test", DisplayName = "no host")]
    [DataRow("[::1]:9000", "s3.test", DisplayName = "an IPv6 literal")]
    [DataRow("127.0.0.1:9000", "127.0.0.1", DisplayName = "an IPv4 literal")]
    [DataRow("s3.test", "s3.test", DisplayName = "the listen host itself")]
    [DataRow("photos.s3.test", "0.0.0.0", DisplayName = "a name when listening on an address")]
    [DataRow("s3.us-east-1.amazonaws.com", "127.0.0.1", DisplayName = "a regional AWS endpoint itself")]
    [DataRow("localhost", "127.0.0.1", DisplayName = "localhost itself")]
    [DataRow("photos.s3.a.b.amazonaws.com", "127.0.0.1", DisplayName = "an AWS name whose region is no one label")]
    public void BucketOf_OtherHost_IsPathStyle(string? authority, string listenHost)
    {
        Assert.IsNull(S3VirtualHost.BucketOf(authority, listenHost));
    }

    [TestMethod]
    public async Task ServeAsync_HostNamingABucketOfTheListenHost_AddressesTheBucket()
    {
        var harness = new S3ServerHarness(listenHost: "s3.test");
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendRawAsync("GET /a.txt HTTP/1.1\r\nHost: photos.s3.test:9000\r\nConnection: close\r\n\r\n");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "hello world");
    }

    [TestMethod]
    public async Task ServeAsync_VirtualHostedListing_ListsTheHostsBucket()
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendRawAsync("GET /?list-type=2 HTTP/1.1\r\nHost: photos.localhost\r\nConnection: close\r\n\r\n");

        StringAssert.Contains(response, "<Name>photos</Name>");
        StringAssert.Contains(response, "<Key>a.txt</Key>");
    }

    [TestMethod]
    public async Task ServeAsync_VirtualHostedMissingKey_EchoesThePathAsSent()
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendRawAsync("GET /2026/a.jpg HTTP/1.1\r\nHost: photos.localhost\r\nConnection: close\r\n\r\n");

        StringAssert.Contains(response, "<Code>NoSuchKey</Code><Message>The specified key does not exist.</Message><Key>2026/a.jpg</Key><Resource>/2026/a.jpg</Resource>");
    }
}
