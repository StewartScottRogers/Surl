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

    [TestMethod]
    public void AccessFor_NoGrants_IsReadWriteForEveryCaller()
    {
        var share = new ContentShare("team", SharePath, new UnitTestInMemoryContentFileSystem()) { Grants = null };

        Assert.IsNull(share.Grants);
        Assert.AreEqual(ContentShareAccess.ReadWrite, share.AccessFor(ContentCaller.Anonymous("tftp")));
        Assert.AreEqual(ContentShareAccess.ReadWrite, share.AccessFor(ContentCaller.ForAccount("alice", "ftp")));
    }

    [TestMethod]
    public void AccessFor_EmptyGrants_ClosesTheShareToEveryCaller()
    {
        var share = new ContentShare("team", SharePath, new UnitTestInMemoryContentFileSystem()) { Grants = new Dictionary<string, ContentShareAccess>() };

        Assert.IsNull(share.AccessFor(ContentCaller.Anonymous("tftp")));
        Assert.IsNull(share.AccessFor(ContentCaller.ForAccount("alice", "ftp")));
    }

    [TestMethod]
    [DataRow(null, null, null)]
    [DataRow(ContentShareAccess.Read, null, ContentShareAccess.Read)]
    [DataRow(null, ContentShareAccess.Read, ContentShareAccess.Read)]
    [DataRow(ContentShareAccess.ReadWrite, ContentShareAccess.Read, ContentShareAccess.ReadWrite)]
    [DataRow(ContentShareAccess.Read, ContentShareAccess.ReadWrite, ContentShareAccess.ReadWrite)]
    [DataRow(ContentShareAccess.Read, ContentShareAccess.Read, ContentShareAccess.Read)]
    public void AccessFor_AnAccount_IsTheWiderOfItsOwnAndEveryonesGrant(ContentShareAccess? own, ContentShareAccess? everyone, ContentShareAccess? expected)
    {
        var grants = new Dictionary<string, ContentShareAccess> { ["bob"] = ContentShareAccess.ReadWrite };
        if (own is not null)
        {
            grants["alice"] = own.Value;
        }

        if (everyone is not null)
        {
            grants[ContentShare.EveryoneMember] = everyone.Value;
        }

        var share = new ContentShare("team", SharePath, new UnitTestInMemoryContentFileSystem()) { Grants = grants };

        Assert.AreEqual(expected, share.AccessFor(ContentCaller.ForAccount("alice", "ftp")));
        Assert.AreEqual(everyone, share.AccessFor(ContentCaller.Anonymous("tftp")));
    }

    [TestMethod]
    public void AccessFor_AccountNames_CompareOrdinally()
    {
        var share = new ContentShare("team", SharePath, new UnitTestInMemoryContentFileSystem())
        {
            Grants = new Dictionary<string, ContentShareAccess>(StringComparer.OrdinalIgnoreCase) { ["alice"] = ContentShareAccess.Read },
        };

        Assert.IsNull(share.AccessFor(ContentCaller.ForAccount("ALICE", "ftp")));
    }

    [TestMethod]
    public void AccessFor_ANullCaller_Throws()
    {
        var share = new ContentShare("team", SharePath, new UnitTestInMemoryContentFileSystem());

        Assert.ThrowsExactly<ArgumentNullException>(() => share.AccessFor(null!));
    }
}
