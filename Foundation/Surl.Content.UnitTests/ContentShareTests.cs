namespace Surl.Content;

[TestClass]
public sealed class ContentShareTests
{
    private static readonly string SharePath = Path.Join("/", "srv", "team");

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
        Assert.ThrowsExactly<ArgumentException>(() => new ContentShare(name, SharePath, new UnitTestInMemoryContentFileSystem()));
    }

    [TestMethod]
    [DataRow("srv")]
    [DataRow("")]
    public void Constructor_APathRelativeToTheCurrentDirectory_Throws(string path)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ContentShare("team", path, new UnitTestInMemoryContentFileSystem()));
    }

    [TestMethod]
    public void Constructor_AMissingFileSystem_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ContentShare("team", SharePath, null!));
    }

    [TestMethod]
    public void Constructor_KeepsWhatItWasGivenAndDefaultsToEveryScheme()
    {
        var files = new UnitTestInMemoryContentFileSystem();

        var share = new ContentShare("team", SharePath, files);

        Assert.AreEqual("team", share.Name);
        Assert.AreEqual(SharePath, share.Path);
        Assert.AreSame(files, share.FileSystem);
        Assert.IsFalse(share.IsReadOnly);
        Assert.IsNull(share.Schemes);
        Assert.IsTrue(share.IsVisibleTo(null));
        Assert.IsTrue(share.IsVisibleTo("tftp"));
    }

    [TestMethod]
    public void IsVisibleTo_LimitedSchemes_ComparesIgnoringCaseAndHidesFromALookupNamingNone()
    {
        var share = new ContentShare("team", SharePath, new UnitTestInMemoryContentFileSystem())
        {
            Schemes = new HashSet<string> { "ftp", "http" },
        };

        Assert.IsTrue(share.Schemes!.Contains("FTP"));
        Assert.IsTrue(share.IsVisibleTo("Http"));
        Assert.IsFalse(share.IsVisibleTo("sftp"));
        Assert.IsFalse(share.IsVisibleTo(null));
    }

    [TestMethod]
    public void Schemes_SetToNull_IsEveryScheme()
    {
        var share = new ContentShare("team", SharePath, new UnitTestInMemoryContentFileSystem()) { Schemes = null };

        Assert.IsNull(share.Schemes);
        Assert.IsTrue(share.IsVisibleTo("sftp"));
    }
}
