namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Path-style and virtual-hosted addressing, from the "XML API overview" (ADR-0110, decision 2).
/// </summary>
[TestClass]
public sealed class GcsRequestTargetTests
{
    [TestMethod]
    [DataRow("/photos/2026/a.jpg", null, "/photos/2026/a.jpg", null, "photos", "2026/a.jpg")]
    [DataRow("/photos/a.jpg?acl", null, "/photos/a.jpg", "acl", "photos", "a.jpg")]
    [DataRow("/photos", null, "/photos", null, "photos", "")]
    [DataRow("/", null, "/", null, "", "")]
    [DataRow("/2026/a.jpg", "photos", "/2026/a.jpg", null, "photos", "2026/a.jpg")]
    [DataRow("*", null, "*", null, "", "")]
    public void Parse_Target_SplitsBucketNameAndQuery(string target, string? hostBucket, string path, string? query, string bucket, string name)
    {
        var parsed = GcsRequestTarget.Parse(target, hostBucket);

        Assert.AreEqual(path, parsed.Path);
        Assert.AreEqual(query, parsed.Query);
        Assert.AreEqual(bucket, parsed.RawBucket);
        Assert.AreEqual(name, parsed.RawName);
    }

    [TestMethod]
    public void Parameters_NoQuery_IsEmpty()
    {
        Assert.IsEmpty(GcsRequestTarget.Parse("/photos", null).Parameters);
    }
}
