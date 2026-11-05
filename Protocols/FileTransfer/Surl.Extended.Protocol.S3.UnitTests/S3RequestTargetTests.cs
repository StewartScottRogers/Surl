namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Path-style addressing, from the user guide's "Virtual hosting of buckets".
/// </summary>
[TestClass]
public sealed class S3RequestTargetTests
{
    [TestMethod]
    [DataRow("/photos/2026/a.jpg", "/photos/2026/a.jpg", null, "photos", "2026/a.jpg", true)]
    [DataRow("/photos/a.jpg?acl", "/photos/a.jpg", "acl", "photos", "a.jpg", true)]
    [DataRow("/photos/", "/photos/", null, "photos", "", false)]
    [DataRow("/photos?list-type=2", "/photos", "list-type=2", "photos", "", false)]
    [DataRow("/", "/", null, "", "", false)]
    [DataRow("*", "*", null, "", "", false)]
    public void Parse_Target_SplitsBucketKeyAndQuery(string target, string path, string? query, string bucket, string key, bool namesAnObject)
    {
        var parsed = S3RequestTarget.Parse(target);

        Assert.AreEqual(new S3RequestTarget(path, query, bucket, key), parsed);
        Assert.AreEqual(namesAnObject, parsed.NamesAnObject);
    }
}
