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
    [DataRow("/photos/a.jpg?", "/photos/a.jpg", "", "photos", "a.jpg", true)]
    [DataRow("/photos/a.jpg?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=k%2F20130524%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-Date=20130524T000000Z&X-Amz-Expires=86400&X-Amz-SignedHeaders=host&X-Amz-Signature=00&X-Amz-Security-Token=t", "/photos/a.jpg", null, "photos", "a.jpg", true)]
    [DataRow("/photos/a.jpg?partNumber=1&X-Amz-Signature=00&uploadId=u", "/photos/a.jpg", "partNumber=1&uploadId=u", "photos", "a.jpg", true)]
    [DataRow("/photos/a.jpg?X%2DAmz%2DDate=1&x-amz-date=2", "/photos/a.jpg", "x-amz-date=2", "photos", "a.jpg", true)]
    public void Parse_Target_SplitsBucketKeyAndQuery(string target, string path, string? query, string bucket, string key, bool namesAnObject)
    {
        var parsed = S3RequestTarget.Parse(target);

        Assert.AreEqual(new S3RequestTarget(path, query, bucket, key), parsed);
        Assert.AreEqual(namesAnObject, parsed.NamesAnObject);
    }
}
