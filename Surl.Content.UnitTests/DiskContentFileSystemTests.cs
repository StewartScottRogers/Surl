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
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);
        ContentPathMapping mapping = store.MapRequestPath("/docs/file.bin");

        ContentFileStatus? status = store.GetFileStatus(mapping);

        Assert.AreEqual(ContentEntryKind.File, mapping.EntryKind);
        Assert.AreEqual(new ContentFileStatus(Contents.Length, new DateTimeOffset(modified)), status);
        Assert.AreEqual(TimeSpan.Zero, status!.LastModifiedUtc.Offset);
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_WholeFile_CopiesEveryByte()
    {
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);
        ContentPathMapping mapping = store.MapRequestPath("/docs/file.bin");
        using var destination = new MemoryStream();

        long copied = await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(Contents.Length), destination, CancellationToken.None);

        Assert.AreEqual(Contents.Length, copied);
        CollectionAssert.AreEqual(Contents, destination.ToArray());
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_Range_CopiesFirstThroughLast()
    {
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);
        ContentPathMapping mapping = store.MapRequestPath("/docs/file.bin");
        using var destination = new MemoryStream();

        long copied = await store.CopyFileBytesAsync(mapping, ContentByteRange.Select(Contents.Length, 3, 9), destination, CancellationToken.None);

        Assert.AreEqual(7, copied);
        CollectionAssert.AreEqual(Contents[3..10], destination.ToArray());
    }

    [TestMethod]
    public void ListDirectory_TemporaryDirectory_ListsEveryEntryInOrdinalOrder()
    {
        var fileModified = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        var directoryModified = new DateTime(2023, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path.Join(servedRoot, "docs", "file.bin"), fileModified);
        File.WriteAllBytes(Path.Join(servedRoot, "docs", "B.txt"), [1]);
        File.WriteAllBytes(Path.Join(servedRoot, "docs", ".hidden"), []);
        Directory.CreateDirectory(Path.Join(servedRoot, "docs", "a-dir"));
        File.WriteAllBytes(Path.Join(servedRoot, "docs", "a-dir", "nested.txt"), [1, 2]);
        Directory.SetLastWriteTimeUtc(Path.Join(servedRoot, "docs", "a-dir"), directoryModified);
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/docs/"), CancellationToken.None);

        Assert.IsTrue(listing.IsListed);
        CollectionAssert.AreEqual(new[] { ".hidden", "B.txt", "a-dir", "file.bin" }, listing.Entries.Select(entry => entry.Name).ToArray());
        Assert.AreEqual(new ContentDirectoryEntry("a-dir", ContentEntryKind.Directory, null, new DateTimeOffset(directoryModified)), listing.Entries[2]);
        Assert.AreEqual(new ContentDirectoryEntry("file.bin", ContentEntryKind.File, Contents.Length, new DateTimeOffset(fileModified)), listing.Entries[3]);
        Assert.AreEqual(1L, listing.Entries[1].Length);
    }

    [TestMethod]
    public void ListDirectory_EmptyDirectoryAndFile_AreAnsweredWithoutAnException()
    {
        Directory.CreateDirectory(Path.Join(servedRoot, "empty"));
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);

        ContentDirectoryListing empty = store.ListDirectory(store.MapRequestPath("/empty"), CancellationToken.None);
        ContentDirectoryListing file = store.ListDirectory(store.MapRequestPath("/docs/file.bin"), CancellationToken.None);
        ContentDirectoryListing missing = store.ListDirectory(store.MapRequestPath("/missing/"), CancellationToken.None);

        Assert.IsTrue(empty.IsListed);
        Assert.IsEmpty(empty.Entries);
        Assert.AreEqual(ContentEntryKind.File, file.LocationKind);
        Assert.AreEqual(ContentEntryKind.None, missing.LocationKind);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ListDirectory_SymbolicLinks_ListsOnlyThoseWhoseTargetIsInsideTheRoot()
    {
        Directory.CreateSymbolicLink(Path.Join(servedRoot, "escape"), Path.Join(temporaryFolder, "outside"));
        File.CreateSymbolicLink(Path.Join(servedRoot, "alias.bin"), Path.Join(servedRoot, "docs", "file.bin"));
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "alias.bin", "docs" }, listing.Entries.Select(entry => entry.Name).ToArray());
        Assert.AreEqual((long?)Contents.Length, listing.Entries[0].Length);
    }

    [TestMethod]
    [DataRow("/docs")]
    [DataRow("/docs/")]
    [DataRow("/")]
    public void MapRequestPath_Directory_ReportsDirectory(string requestPath)
    {
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);

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
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);

        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(ContentEntryKind.None, mapping.EntryKind);
        Assert.IsNull(store.GetFileStatus(mapping));
    }

    [TestMethod]
    public void MapRequestPath_MissingPath_KeepsItsLocationUnderTheRoot()
    {
        var fileSystem = new DiskContentFileSystem();
        var store = new ContentStore(servedRoot, fileSystem, ContentExposureOptions.ServeEverythingInsideTheRoot);

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
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);

        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        Assert.AreEqual(ContentPathRefusal.ResolvesOutsideRoot, mapping.Refusal);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void MapRequestPath_SymbolicLinkToAFileOutsideTheRoot_IsRefused()
    {
        File.CreateSymbolicLink(Path.Join(servedRoot, "secret.txt"), Path.Join(temporaryFolder, "outside", "secret.txt"));
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);

        ContentPathMapping mapping = store.MapRequestPath("/secret.txt");

        Assert.AreEqual(ContentPathRefusal.ResolvesOutsideRoot, mapping.Refusal);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void MapRequestPath_SymbolicLinkStayingInsideTheRoot_MapsToItsTarget()
    {
        File.CreateSymbolicLink(Path.Join(servedRoot, "alias.bin"), Path.Join(servedRoot, "docs", "file.bin"));
        var fileSystem = new DiskContentFileSystem();
        var store = new ContentStore(servedRoot, fileSystem, ContentExposureOptions.ServeEverythingInsideTheRoot);

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
        var store = new ContentStore(linkedRoot, new DiskContentFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);

        ContentPathMapping mapping = store.MapRequestPath("/docs/file.bin");

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(ContentEntryKind.File, mapping.EntryKind);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void MapRequestPath_DefaultOptionsAndALinkInsideTheRoot_IsAnsweredAsAMissingPath()
    {
        File.CreateSymbolicLink(Path.Join(servedRoot, "alias.bin"), Path.Join(servedRoot, "docs", "file.bin"));
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), new ContentExposureOptions());

        ContentPathMapping mapping = store.MapRequestPath("/alias.bin");

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(ContentEntryKind.None, mapping.EntryKind);
    }

    [TestMethod]
    public void MapRequestPath_DefaultOptionsAndAPlainFile_IsServed()
    {
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), new ContentExposureOptions());

        ContentPathMapping mapping = store.MapRequestPath("/docs/file.bin");

        Assert.AreEqual(ContentEntryKind.File, mapping.EntryKind);
    }

    [TestMethod]
    public async Task WriteUploadAsync_WithinTheLimit_WritesTheFile()
    {
        var options = new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = Contents.Length };
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), options);

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath("/docs/upload.bin"), new MemoryStream(Contents), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, result);
        CollectionAssert.AreEqual(Contents, await File.ReadAllBytesAsync(Path.Join(servedRoot, "docs", "upload.bin")));
    }

    [TestMethod]
    public async Task WriteUploadAsync_OverTheLimit_LeavesNoFile()
    {
        var options = new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = Contents.Length - 1 };
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), options);

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath("/docs/upload.bin"), new MemoryStream(Contents), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.TooLarge, result);
        Assert.IsFalse(File.Exists(Path.Join(servedRoot, "docs", "upload.bin")));
    }

    [TestMethod]
    public async Task WriteUploadAsync_ExistingFile_IsReplacedWithNoTemporaryFileLeft()
    {
        var options = new ContentExposureOptions { AllowUploads = true };
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), options);

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath("/docs/file.bin"), new MemoryStream("new"u8.ToArray()), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, result);
        CollectionAssert.AreEqual("new"u8.ToArray(), await File.ReadAllBytesAsync(Path.Join(servedRoot, "docs", "file.bin")));
        CollectionAssert.AreEqual(new[] { "file.bin" }, Directory.GetFiles(Path.Join(servedRoot, "docs")).Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public async Task WriteUploadAsync_OverTheLimitOverAnExistingFile_KeepsItsBytesWithNoTemporaryFileLeft()
    {
        var options = new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 2 };
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), options);

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath("/docs/file.bin"), new MemoryStream("new"u8.ToArray()), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.TooLarge, result);
        CollectionAssert.AreEqual(Contents, await File.ReadAllBytesAsync(Path.Join(servedRoot, "docs", "file.bin")));
        CollectionAssert.AreEqual(new[] { "file.bin" }, Directory.GetFiles(Path.Join(servedRoot, "docs")).Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public async Task EveryChange_OnDisk_WorksAndLeavesNoTemporaryFile()
    {
        var options = new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = Contents.Length + 3 };
        var store = new ContentStore(servedRoot, new DiskContentFileSystem(), options);

        Assert.AreEqual(ContentChangeResult.Done, store.CreateDirectory(store.MapRequestPath("/made")));
        Assert.AreEqual(ContentChangeResult.Exists, store.CreateDirectory(store.MapRequestPath("/made")));
        Assert.AreEqual(ContentUploadResult.Written, await store.AppendUploadAsync(store.MapRequestPath("/docs/file.bin"), new MemoryStream("abc"u8.ToArray()), CancellationToken.None));
        Assert.AreEqual(ContentUploadResult.TooLarge, await store.AppendUploadAsync(store.MapRequestPath("/docs/file.bin"), new MemoryStream("d"u8.ToArray()), CancellationToken.None));
        Assert.AreEqual(ContentChangeResult.Done, store.RenameEntry(store.MapRequestPath("/docs/file.bin"), store.MapRequestPath("/made/renamed.bin")));
        Assert.AreEqual(ContentChangeResult.NotEmpty, store.RemoveEmptyDirectory(store.MapRequestPath("/made")));
        Assert.AreEqual(ContentChangeResult.Done, store.RenameEntry(store.MapRequestPath("/made"), store.MapRequestPath("/docs/moved")));

        string moved = Path.Join(servedRoot, "docs", "moved", "renamed.bin");
        CollectionAssert.AreEqual(Contents.Concat("abc"u8.ToArray()).ToArray(), await File.ReadAllBytesAsync(moved));
        Assert.AreEqual(ContentChangeResult.Done, store.DeleteFile(store.MapRequestPath("/docs/moved/renamed.bin")));
        Assert.AreEqual(ContentChangeResult.Done, store.RemoveEmptyDirectory(store.MapRequestPath("/docs/moved")));
        Assert.IsEmpty(Directory.GetFileSystemEntries(Path.Join(servedRoot, "docs")));
        CollectionAssert.AreEqual(new[] { "docs" }, Directory.GetFileSystemEntries(servedRoot).Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public void RemoveEmptyDirectory_NotEmpty_ThrowsIOExceptionAndKeepsIt()
    {
        string docs = Path.Join(servedRoot, "docs");

        Assert.ThrowsExactly<IOException>(() => new DiskContentFileSystem().RemoveEmptyDirectory(docs));
        Assert.IsTrue(File.Exists(Path.Join(docs, "file.bin")));
    }

    [TestMethod]
    public void DeleteFile_NoFileThere_DoesNothing()
    {
        string path = Path.Join(servedRoot, "docs", "never-written.bin");

        new DiskContentFileSystem().DeleteFile(path);

        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void CreateDirectory_MissingParent_CreatesBoth()
    {
        string path = Path.Join(temporaryFolder, "a", "b");

        new DiskContentFileSystem().CreateDirectory(path);

        Assert.IsTrue(Directory.Exists(path));
        Assert.AreEqual(ContentEntryKind.Directory, new DiskContentFileSystem().GetEntryKind(path));
    }
}
