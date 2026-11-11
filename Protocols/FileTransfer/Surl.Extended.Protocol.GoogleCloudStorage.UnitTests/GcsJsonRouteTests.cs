namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Which JSON API operation a method and path name (ADR-0148, decisions 1 and 2), and which Google method id each is.
/// </summary>
[TestClass]
public sealed class GcsJsonRouteTests
{
    private static GcsJsonRoute Recognise(string method, string target) =>
        GcsJsonRoute.Recognise(method, GcsRequestTarget.Parse(target, hostBucket: null));

    [TestMethod]
    [DataRow("/storage/v1/b?project=p", "GET", "ListBuckets", "", "", "storage.buckets.list")]
    [DataRow("/storage/v1/b/photos", "GET", "GetBucket", "photos", "", "storage.buckets.get")]
    [DataRow("/storage/v1/b/photos/o", "GET", "ListObjects", "photos", "", "storage.objects.list")]
    [DataRow("/storage/v1/b/photos/o/a/b%2Fc%2fd.txt", "GET", "GetObject", "photos", "a/b/c/d.txt", "storage.objects.get")]
    [DataRow("/storage/v1/b/photos/o/a.txt", "DELETE", "DeleteObject", "photos", "a.txt", "storage.objects.delete")]
    [DataRow("/download/storage/v1/b/photos/o/a.txt", "GET", "GetObject", "photos", "a.txt", "storage.objects.get")]
    [DataRow("/upload/storage/v1/b/photos/o?uploadType=resumable", "POST", "InsertObject", "photos", "", "storage.objects.insert")]
    [DataRow("/upload/storage/v1/b/photos/o?uploadType=resumable&upload_id=x", "PUT", "ResumeUpload", "photos", "", "storage.objects.insert")]
    [DataRow("/upload/storage/v1/b/photos/o?uploadType=resumable&upload_id=x", "DELETE", "CancelUpload", "photos", "", "storage.objects.insert")]
    public void Recognise_AListedRequest_IsItsOperationBucketAndName(string target, string method, string operation, string bucket, string name, string methodId)
    {
        var route = Recognise(method, target);

        Assert.AreEqual(operation, route.Operation.ToString());
        Assert.AreEqual(bucket, route.RawBucket);
        Assert.AreEqual(name, route.RawName);
        Assert.AreEqual(methodId, route.MethodId);
        Assert.IsTrue(route.IsListed);
    }

    [TestMethod]
    [DataRow("/storage/v1/b/photos/x/y", "GET", DisplayName = "four parts with no o")]
    [DataRow("/storage/v1/b/photos/o/", "GET", DisplayName = "an empty name")]
    [DataRow("/storage/v1/b/photos", "DELETE", DisplayName = "a delete of a bucket")]
    [DataRow("/storage/v1/b/photos/o/a.txt", "HEAD", DisplayName = "a head")]
    [DataRow("/storage/v1/o/photos", "GET", DisplayName = "no b")]
    [DataRow("/storage/v1/b//o", "GET", DisplayName = "an empty bucket")]
    [DataRow("/upload/storage/v1/b/photos/o", "GET", DisplayName = "the upload path")]
    [DataRow("/upload/storage/v1/b/photos/o", "PATCH", DisplayName = "a patch of the upload path")]
    [DataRow("/upload/storage/v1/b/photos/o?upload_id=x&name=y&versions=1", "PUT", DisplayName = "an unlisted parameter on a session request")]
    [DataRow("/download/storage/v1/b/photos", "GET", DisplayName = "a bucket on the download path")]
    [DataRow("/storage/v1/b/photos/o?versions=true", "GET", DisplayName = "an unlisted parameter")]
    public void Recognise_AnythingElse_IsUnlisted(string target, string method)
    {
        var route = Recognise(method, target);

        Assert.AreEqual(GcsJsonOperation.Unlisted, route.Operation);
        Assert.IsFalse(route.IsListed);
        Assert.AreEqual("storage.unlisted", route.MethodId);
    }

    [TestMethod]
    [DataRow("/storage/v1/b", true)]
    [DataRow("/download/storage/v1/b/x/o/y", true)]
    [DataRow("/upload/storage/v1/b/x/o", true)]
    [DataRow("/storage/v1", false)]
    [DataRow("/storage/v2/b", false)]
    [DataRow("/photos/storage/v1/b", false)]
    public void IsJsonPath_IsThePrefixOfDecisionOne(string path, bool expected)
    {
        Assert.AreEqual(expected, GcsJsonRoute.IsJsonPath(path));
    }
}
