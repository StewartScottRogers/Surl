namespace Surl.Content;

[TestClass]
public sealed class ContentExposureOptionsTests
{
    [TestMethod]
    public void Default_RefusesUploadsAndListingsHidesDotFilesAndLinks()
    {
        var options = new ContentExposureOptions();

        Assert.IsFalse(options.AllowUploads);
        Assert.IsFalse(options.ListDirectories);
        Assert.IsFalse(options.FollowSymbolicLinks);
        Assert.IsFalse(options.ServeDotFiles);
        Assert.AreEqual(104857600L, options.MaxUploadBytes);
    }

    [TestMethod]
    public void ServeEverythingInsideTheRoot_ListsAndServesDotFilesAndLinksButRefusesUploads()
    {
        ContentExposureOptions options = ContentExposureOptions.ServeEverythingInsideTheRoot;

        Assert.IsFalse(options.AllowUploads);
        Assert.IsTrue(options.ListDirectories);
        Assert.IsTrue(options.FollowSymbolicLinks);
        Assert.IsTrue(options.ServeDotFiles);
        Assert.AreEqual(ContentExposureOptions.DefaultMaxUploadBytes, options.MaxUploadBytes);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(1L)]
    [DataRow(long.MaxValue)]
    public void MaxUploadBytes_ZeroOrPositive_IsKept(long maxUploadBytes)
    {
        var options = new ContentExposureOptions { MaxUploadBytes = maxUploadBytes };

        Assert.AreEqual(maxUploadBytes, options.MaxUploadBytes);
    }

    [TestMethod]
    public void MaxUploadBytes_Negative_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ContentExposureOptions { MaxUploadBytes = -1 });
    }
}
