namespace Surl.Content;

public sealed partial class ContentStoreTests
{
    private static readonly string TrailingSlashFile = Path.Join(Root, "file.txt");

    [TestMethod]
    public void MapRequestPath_TrailingSlashAfterAFile_MapsAsNothingThere()
    {
        ContentStore store = TrailingSlashStore(new ContentExposureOptions());

        ContentPathMapping mapping = store.MapRequestPath("/file.txt/");

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(ContentEntryKind.None, mapping.EntryKind);
        Assert.AreEqual(TrailingSlashFile, mapping.Location);
    }

    [TestMethod]
    public void MapRequestPath_TrailingSlashAfterADirectory_MapsTheDirectory()
    {
        ContentStore store = TrailingSlashStore(new ContentExposureOptions());

        ContentPathMapping mapping = store.MapRequestPath("/dir/");

        Assert.AreEqual(ContentEntryKind.Directory, mapping.EntryKind);
    }

    [TestMethod]
    public void GetEntryKindAndGetFileStatus_TrailingSlashAfterAFile_AnswerNothingThere()
    {
        ContentStore store = TrailingSlashStore(new ContentExposureOptions());
        ContentPathMapping mapping = store.MapRequestPath("/file.txt/");

        ContentEntryKind kind = store.GetEntryKind(mapping);
        ContentFileStatus? status = store.GetFileStatus(mapping);

        Assert.AreEqual(ContentEntryKind.None, kind);
        Assert.IsNull(status);
    }

    [TestMethod]
    public void ListDirectory_TrailingSlashAfterAFile_ReportsNothingThere()
    {
        ContentStore store = TrailingSlashStore(new ContentExposureOptions());

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/file.txt/"), CancellationToken.None);

        Assert.IsFalse(listing.IsListed);
        Assert.AreEqual(ContentEntryKind.None, listing.LocationKind);
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_TrailingSlashAfterAFile_ThrowsFileNotFound()
    {
        ContentStore store = TrailingSlashStore(new ContentExposureOptions());
        ContentPathMapping mapping = store.MapRequestPath("/file.txt/");
        using var destination = new MemoryStream();

        await Assert.ThrowsExactlyAsync<FileNotFoundException>(
            () => store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(4), destination, CancellationToken.None));
    }

    [TestMethod]
    [DataRow("/file.txt/")]
    [DataRow("/new.txt/")]
    public async Task WriteUploadAsync_TrailingSlash_IsNotPermitted(string requestPath)
    {
        ContentStore store = TrailingSlashStore(new ContentExposureOptions { AllowUploads = true });
        using var source = new MemoryStream([1, 2, 3]);

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath(requestPath), source, CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.NotPermitted, result);
    }

    [TestMethod]
    [DataRow("srv")]
    [DataRow("srv/www")]
    [DataRow("C:")]
    [DataRow("C:srv")]
    public void Constructor_ServedRootRelativeToTheCurrentDirectory_Throws(string servedRoot)
    {
        var fileSystem = new InMemoryContentFileSystem();

        Assert.ThrowsExactly<ArgumentException>(() => new ContentStore(servedRoot, fileSystem));
    }

    [TestMethod]
    [DataRow("/srv/www")]
    [DataRow("/")]
    public void Constructor_ServedRootStartingWithASlash_IsKeptAsGiven(string servedRoot)
    {
        var store = new ContentStore(servedRoot, new InMemoryContentFileSystem());

        Assert.AreEqual(servedRoot, store.ServedRoot);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(@"\srv\www")]
    [DataRow(@"C:\srv\www")]
    [DataRow(@"\\server\share")]
    public void Constructor_WindowsServedRootIndependentOfTheCurrentDirectory_IsKeptAsGiven(string servedRoot)
    {
        var store = new ContentStore(servedRoot, new InMemoryContentFileSystem());

        Assert.AreEqual(servedRoot, store.ServedRoot);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Constructor_BackslashServedRootOffWindows_ThrowsAsRelative()
    {
        var fileSystem = new InMemoryContentFileSystem();

        Assert.ThrowsExactly<ArgumentException>(() => new ContentStore(@"\srv\www", fileSystem));
    }

    private static ContentStore TrailingSlashStore(ContentExposureOptions options)
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddDirectory(Path.Join(Root, "dir"))
            .AddFile(TrailingSlashFile, [1, 2, 3, 4], Modified);
        return new ContentStore(Root, fileSystem, options);
    }
}
