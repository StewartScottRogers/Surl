namespace Surl.Content;

[TestClass]
[TestCategory("Integration")]
public sealed class DiskContentFileSystemTests
{
    private static readonly byte[] Contents = "0123456789abcdef"u8.ToArray();

    private string temporaryFolder = string.Empty;

    private string servedRoot = string.Empty;

    [TestInitialize]
    public void CreateTemporaryTree()
    {
        temporaryFolder = Path.Join(Path.GetTempPath(), "surl-content-" + Guid.NewGuid().ToString("N"));
        servedRoot = Path.Join(temporaryFolder, "root");
        Directory.CreateDirectory(Path.Join(servedRoot, "docs"));
        File.WriteAllBytes(Path.Join(servedRoot, "docs", "file.bin"), Contents);
        Directory.CreateDirectory(Path.Join(temporaryFolder, "outside"));
        File.WriteAllBytes(Path.Join(temporaryFolder, "outside", "secret.txt"), Contents);
    }

    [TestCleanup]
    public void DeleteTemporaryTree() => Directory.Delete(temporaryFolder, recursive: true);

    [TestMethod]
    public void GetFileStatus_File_ReportsLengthAndLastWriteTimeUtc()
    {
        var modified = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path.Join(servedRoot, "docs", "file.bin"), modified);
        var store = new ContentStore(servedRoot, new DiskContentFileSystem());
        ContentPathMapping mapping = store.MapRequestPath("/docs/file.bin");

        ContentFileStatus? status = store.GetFileStatus(mapping);

        Assert.AreEqual(ContentEntryKind.File, mapping.EntryKind);
        Assert.AreEqual(new ContentFileStatus(Contents.Length, new DateTimeOffset(modified)), status);
        Assert.AreEqual(TimeSpan.Zero, status!.LastModifiedUtc.Offset);
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_WholeFile_CopiesEveryByte()
    {
        var store = new ContentStore(servedRoot, new DiskContentFileSystem());
        ContentPathMapping mapping = store.MapRequestPath("/docs/file.bin");
        using var destination = new MemoryStream();

        long copied = await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(Contents.Length), destination, CancellationToken.None);

        Assert.AreEqual(Contents.Length, copied);
        CollectionAssert.AreEqual(Contents, destination.ToArray());
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_Range_CopiesFirstThroughLast()
    {
        var store = new ContentStore(servedRoot, new DiskContentFileSystem());
        ContentPathMapping mapping = store.MapRequestPath("/docs/file.bin");
        using var destination = new MemoryStream();

        long copied = await store.CopyFileBytesAsync(mapping, ContentByteRange.Select(Contents.Length, 3, 9), destination, CancellationToken.None);

        Assert.AreEqual(7, copied);
        CollectionAssert.AreEqual(Contents[3..10], destination.ToArray());
    }

    [TestMethod]
    [DataRow("/docs")]
    [DataRow("/docs/")]
    [DataRow("/")]
    public void MapRequestPath_Directory_ReportsDirectory(string requestPath)
    {
        var store = new ContentStore(servedRoot, new DiskContentFileSystem());

        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(ContentEntryKind.Directory, mapping.EntryKind);
        Assert.IsNull(store.GetFileStatus(mapping));
    }

    [TestMethod]
    [DataRow("/missing.txt")]
    [DataRow("/missing/deeper/file.txt")]
    [DataRow("/docs/file.bin/under-a-file")]
    public void MapRequestPath_MissingPath_ReportsNothing(string requestPath)
    {
        var store = new ContentStore(servedRoot, new DiskContentFileSystem());

        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(ContentEntryKind.None, mapping.EntryKind);
        Assert.IsNull(store.GetFileStatus(mapping));
    }

    [TestMethod]
    public void MapRequestPath_MissingPath_KeepsItsLocationUnderTheRoot()
    {
        var fileSystem = new DiskContentFileSystem();
        var store = new ContentStore(servedRoot, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/missing/deeper/file.txt");

        Assert.AreEqual(Path.Join(fileSystem.ResolveFinalPath(servedRoot), "missing", "deeper", "file.txt"), mapping.Location);
    }

    // Creating a symbolic link on Windows needs a privilege a test run cannot count on, so
    // the symbolic-link tests run on Linux and macOS only.
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("/escape/secret.txt")]
    [DataRow("/escape")]
    [DataRow("/escape/missing.txt")]
    public void MapRequestPath_SymbolicLinkPointingOutsideTheRoot_IsRefused(string requestPath)
    {
        Directory.CreateSymbolicLink(Path.Join(servedRoot, "escape"), Path.Join(temporaryFolder, "outside"));
        var store = new ContentStore(servedRoot, new DiskContentFileSystem());

        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        Assert.AreEqual(ContentPathRefusal.ResolvesOutsideRoot, mapping.Refusal);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void MapRequestPath_SymbolicLinkToAFileOutsideTheRoot_IsRefused()
    {
        File.CreateSymbolicLink(Path.Join(servedRoot, "secret.txt"), Path.Join(temporaryFolder, "outside", "secret.txt"));
        var store = new ContentStore(servedRoot, new DiskContentFileSystem());

        ContentPathMapping mapping = store.MapRequestPath("/secret.txt");

        Assert.AreEqual(ContentPathRefusal.ResolvesOutsideRoot, mapping.Refusal);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void MapRequestPath_SymbolicLinkStayingInsideTheRoot_MapsToItsTarget()
    {
        File.CreateSymbolicLink(Path.Join(servedRoot, "alias.bin"), Path.Join(servedRoot, "docs", "file.bin"));
        var fileSystem = new DiskContentFileSystem();
        var store = new ContentStore(servedRoot, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/alias.bin");

        Assert.AreEqual(ContentEntryKind.File, mapping.EntryKind);
        Assert.AreEqual(Path.Join(fileSystem.ResolveFinalPath(servedRoot), "docs", "file.bin"), mapping.Location);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void MapRequestPath_ServedRootReachedThroughASymbolicLink_MapsInsideIt()
    {
        string linkedRoot = Path.Join(temporaryFolder, "linked-root");
        Directory.CreateSymbolicLink(linkedRoot, servedRoot);
        var store = new ContentStore(linkedRoot, new DiskContentFileSystem());

        ContentPathMapping mapping = store.MapRequestPath("/docs/file.bin");

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(ContentEntryKind.File, mapping.EntryKind);
    }
}
