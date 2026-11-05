namespace Surl.Content;

[TestClass]
public sealed class ContentBucketTests
{
    [TestMethod]
    [DataRow(".surl")]
    [DataRow(".SURL")]
    [DataRow("..")]
    [DataRow("a/b")]
    [DataRow("c:d")]
    [DataRow("CON")]
    [DataRow("")]
    public void Constructor_ANameThatIsNotAValidFolderName_Throws(string name)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ContentBucket(name, "/media"));
    }

    [TestMethod]
    public void Constructor_AMissingNameOrContentPath_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ContentBucket(null!, "/media"));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ContentBucket("photos", null!));
    }

    [TestMethod]
    [DataRow("media")]
    [DataRow("/media/../x")]
    [DataRow("/media//x")]
    public void Constructor_AContentPathARequestMayNotName_Throws(string contentPath)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ContentBucket("photos", contentPath));
    }

    [TestMethod]
    public void Constructor_KeepsWhatItWasGiven()
    {
        var bucket = new ContentBucket("photos", "/team/a%20b");

        Assert.AreEqual("photos", bucket.Name);
        Assert.AreEqual("/team/a%20b", bucket.ContentPath);
        CollectionAssert.AreEqual(new[] { "team", "a b" }, bucket.ContentPathSegments);
    }
}
