namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Which <c>Host</c> fields address a bucket ("XML API overview"; ADR-0110, decision 2).
/// </summary>
[TestClass]
public sealed class GcsVirtualHostTests
{
    [TestMethod]
    [DataRow("photos.gcs.test:4443", "gcs.test", "photos", DisplayName = "the listen host, with a port")]
    [DataRow("My.Photos.GCS.Test.", "gcs.test", "my.photos", DisplayName = "a dotted bucket, case folded, trailing dot")]
    [DataRow("photos.localhost", "127.0.0.1", "photos", DisplayName = "localhost")]
    [DataRow("photos.storage.googleapis.com", "127.0.0.1", "photos", DisplayName = "Google's endpoint")]
    public void BucketOf_VirtualHostedName_IsTheBucket(string authority, string listenHost, string bucket)
    {
        Assert.AreEqual(bucket, GcsVirtualHost.BucketOf(authority, listenHost));
    }

    [TestMethod]
    [DataRow(null, "gcs.test", DisplayName = "no host")]
    [DataRow("[::1]:4443", "gcs.test", DisplayName = "an IPv6 literal")]
    [DataRow("127.0.0.1:4443", "127.0.0.1", DisplayName = "an IPv4 literal")]
    [DataRow("gcs.test", "gcs.test", DisplayName = "the listen host itself")]
    [DataRow("photos.gcs.test", "0.0.0.0", DisplayName = "a name when listening on an address")]
    [DataRow("storage.googleapis.com", "127.0.0.1", DisplayName = "Google's endpoint itself")]
    public void BucketOf_OtherHost_IsPathStyle(string? authority, string listenHost)
    {
        Assert.IsNull(GcsVirtualHost.BucketOf(authority, listenHost));
    }
}
