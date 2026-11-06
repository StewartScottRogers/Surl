namespace Surl.Content;

public sealed partial class ContentStoreTests
{
    private static readonly string UploadPath = Path.Join(Root, "upload.bin");

    [TestMethod]
    public async Task WriteUploadAsync_DefaultOptions_IsNotPermittedWithoutCreatingAFile()
    {
        var fileSystem = new UnitTestInMemoryContentFileSystem().AddDirectory(Root);
        var store = new ContentStore(Root, fileSystem, new ContentExposureOptions());
        ContentPathMapping mapping = store.MapRequestPath("/upload.bin");
        using var source = new MemoryStream([1, 2, 3]);

        ContentUploadResult result = await store.WriteUploadAsync(mapping, source, CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.NotPermitted, result);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(UploadPath));
        Assert.IsFalse(fileSystem.Calls.Exists(call => call.StartsWith("CreateFileForAsyncWrite(", StringComparison.Ordinal)));
        Assert.AreEqual(0, source.Position);
    }

    [TestMethod]
    public async Task WriteUploadAsync_ExactlyMaxUploadBytes_IsWritten()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = UploadStore(maxUploadBytes: 10);
        byte[] upload = "0123456789"u8.ToArray();

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), new MemoryStream(upload), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, result);
        CollectionAssert.AreEqual(upload, fileSystem.ReadWrittenFile(UploadPath));
        Assert.IsFalse(fileSystem.Calls.Exists(call => call.StartsWith("DeleteFile(", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task WriteUploadAsync_OneByteOverMaxUploadBytes_IsTooLargeAndLeavesNoFile()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = UploadStore(maxUploadBytes: 10);
        using var source = new MemoryStream(new byte[11]);

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), source, CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.TooLarge, result);
        Assert.AreEqual(0, fileSystem.EntriesDirectlyInside(Root).Count);
    }

    [TestMethod]
    public async Task WriteUploadAsync_FarOverMaxUploadBytes_StopsReadingOneBytePastTheLimit()
    {
        (ContentStore store, _) = UploadStore(maxUploadBytes: 10);
        using var source = new MemoryStream(new byte[1_000_000]);

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), source, CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.TooLarge, result);
        Assert.AreEqual(11, source.Position);
    }

    [TestMethod]
    public async Task WriteUploadAsync_MaxUploadBytesZero_AcceptsAnySize()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = UploadStore(maxUploadBytes: 0);
        byte[] upload = new byte[300_000];
        upload[^1] = 7;

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), new MemoryStream(upload), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, result);
        CollectionAssert.AreEqual(upload, fileSystem.ReadWrittenFile(UploadPath));
    }

    [TestMethod]
    public async Task WriteUploadAsync_ExistingFile_IsReplaced()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = UploadStore(maxUploadBytes: 10);
        fileSystem.AddFile(UploadPath, "old contents"u8.ToArray(), Modified);

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), new MemoryStream("new"u8.ToArray()), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, result);
        CollectionAssert.AreEqual("new"u8.ToArray(), fileSystem.ReadFile(UploadPath));
        CollectionAssert.AreEqual(new[] { UploadPath }, fileSystem.EntriesDirectlyInside(Root));
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/dir")]
    [DataRow("/missing/upload.bin")]
    [DataRow("/.hidden")]
    [DataRow("/link/upload.bin")]
    public async Task WriteUploadAsync_DirectoryMissingParentOrHiddenPath_IsNotPermitted(string requestPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = UploadStore(maxUploadBytes: 10);
        fileSystem
            .AddDirectory(Path.Join(Root, "dir"))
            .AddSymbolicLink(Path.Join(Root, "link"), Path.Join(Root, "dir"));

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath(requestPath), new MemoryStream([1]), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.NotPermitted, result);
        Assert.IsFalse(fileSystem.Calls.Exists(call => call.StartsWith("CreateFileForAsyncWrite(", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task WriteUploadAsync_ServedRootIsTheFileSystemRootAndMissing_IsNotPermitted()
    {
        string root = Path.DirectorySeparatorChar.ToString();
        var store = new ContentStore(root, new UnitTestInMemoryContentFileSystem(), new ContentExposureOptions { AllowUploads = true });

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath("/"), new MemoryStream([1]), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.NotPermitted, result);
    }

    [TestMethod]
    public async Task WriteUploadAsync_LinkResolvingToAMissingFileSystemRoot_IsNotPermitted()
    {
        string root = Path.DirectorySeparatorChar.ToString();
        var fileSystem = new UnitTestInMemoryContentFileSystem().AddSymbolicLink(Path.Join(root, "link"), root);
        var options = new ContentExposureOptions { AllowUploads = true, FollowSymbolicLinks = true };
        var store = new ContentStore(root, fileSystem, options);

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath("/link"), new MemoryStream([1]), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.NotPermitted, result);
    }

    [TestMethod]
    public async Task WriteUploadAsync_SourceFailsMidUpload_DeletesTheTemporaryFileAndRethrows()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = UploadStore(maxUploadBytes: 0);

        await Assert.ThrowsExactlyAsync<IOException>(
            () => store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), new FailingOnSecondReadStream([1, 2, 3]), CancellationToken.None));

        Assert.AreEqual(0, fileSystem.EntriesDirectlyInside(Root).Count);
    }

    [TestMethod]
    public async Task WriteUploadAsync_SourceFailsMidUploadOverAnExistingFile_KeepsItsBytesAndLeavesNoTemporaryFile()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = UploadStore(maxUploadBytes: 0);
        fileSystem.AddFile(UploadPath, "old contents"u8.ToArray(), Modified);

        await Assert.ThrowsExactlyAsync<IOException>(
            () => store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), new FailingOnSecondReadStream([1, 2, 3]), CancellationToken.None));

        CollectionAssert.AreEqual("old contents"u8.ToArray(), fileSystem.ReadFile(UploadPath));
        CollectionAssert.AreEqual(new[] { UploadPath }, fileSystem.EntriesDirectlyInside(Root));
    }

    [TestMethod]
    public async Task WriteUploadAsync_TooLargeOverAnExistingFile_KeepsItsBytesAndLeavesNoTemporaryFile()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = UploadStore(maxUploadBytes: 10);
        fileSystem.AddFile(UploadPath, "old contents"u8.ToArray(), Modified);

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), new MemoryStream(new byte[11]), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.TooLarge, result);
        CollectionAssert.AreEqual("old contents"u8.ToArray(), fileSystem.ReadFile(UploadPath));
        CollectionAssert.AreEqual(new[] { UploadPath }, fileSystem.EntriesDirectlyInside(Root));
    }

    [TestMethod]
    public async Task WriteUploadAsync_RenameFails_KeepsTheExistingFileDeletesTheTemporaryFileAndRethrows()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = UploadStore(maxUploadBytes: 10);
        fileSystem.AddFile(UploadPath, "old contents"u8.ToArray(), Modified);
        fileSystem.FailMoves = true;

        await Assert.ThrowsExactlyAsync<IOException>(
            () => store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), new MemoryStream("new"u8.ToArray()), CancellationToken.None));

        CollectionAssert.AreEqual("old contents"u8.ToArray(), fileSystem.ReadFile(UploadPath));
        CollectionAssert.AreEqual(new[] { UploadPath }, fileSystem.EntriesDirectlyInside(Root));
    }

    [TestMethod]
    public async Task WriteUploadAsync_Written_WritesADotFileBesideTheTargetAndRenamesItOverTheTarget()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = UploadStore(maxUploadBytes: 10);
        string temporaryPrefix = Path.Join(Root, ".surl-upload-");

        await store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), new MemoryStream([1]), CancellationToken.None);

        string created = fileSystem.Calls.Single(call => call.StartsWith("CreateFileForAsyncWrite(", StringComparison.Ordinal));
        string temporaryLocation = created["CreateFileForAsyncWrite(".Length..^1];
        StringAssert.StartsWith(temporaryLocation, temporaryPrefix, StringComparison.Ordinal);
        CollectionAssert.Contains(fileSystem.Calls, $"MoveFileReplacing({temporaryLocation}, {UploadPath})");
    }

    [TestMethod]
    public async Task WriteUploadAsync_Cancelled_ThrowsWithoutCreatingAFile()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = UploadStore(maxUploadBytes: 10);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), new MemoryStream([1]), cancellation.Token));

        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(UploadPath));
    }

    [TestMethod]
    public async Task WriteUploadAsync_RefusedMappingOrMissingSource_IsRejected()
    {
        (ContentStore store, _) = UploadStore(maxUploadBytes: 10);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => store.WriteUploadAsync(store.MapRequestPath("/../x"), new MemoryStream([1]), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => store.WriteUploadAsync(null!, new MemoryStream([1]), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task WriteUploadAsync_ReadOnlyFileSystem_ThrowsNotSupported()
    {
        var store = new ContentStore(Root, new UnitTestReadOnlyContentFileSystem(), new ContentExposureOptions { AllowUploads = true });

        await Assert.ThrowsExactlyAsync<NotSupportedException>(
            () => store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), new MemoryStream([1]), CancellationToken.None));
    }

    [TestMethod]
    public void DeleteFile_ReadOnlyFileSystem_ThrowsNotSupported()
    {
        IContentFileSystem fileSystem = new UnitTestReadOnlyContentFileSystem();

        Assert.ThrowsExactly<NotSupportedException>(() => fileSystem.DeleteFile(UploadPath));
    }

    [TestMethod]
    public void MoveFileReplacing_ReadOnlyFileSystem_ThrowsNotSupported()
    {
        IContentFileSystem fileSystem = new UnitTestReadOnlyContentFileSystem();

        Assert.ThrowsExactly<NotSupportedException>(() => fileSystem.MoveFileReplacing(Path.Join(Root, ".temporary"), UploadPath));
    }

    [TestMethod]
    public void CreateDirectory_ReadOnlyFileSystem_ThrowsNotSupported()
    {
        IContentFileSystem fileSystem = new UnitTestReadOnlyContentFileSystem();

        Assert.ThrowsExactly<NotSupportedException>(() => fileSystem.CreateDirectory(Path.Join(Root, "sub")));
    }

    [TestMethod]
    public async Task WriteUploadAsync_InMemoryFileSystemAllowingUploads_IsWrittenAndReadsBack()
    {
        var fileSystem = new InMemoryContentFileSystem(new SettableTimeProvider(Modified));
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions { AllowUploads = true });
        byte[] upload = "uploaded into memory"u8.ToArray();

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), new MemoryStream(upload), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, result);
        ContentPathMapping mapping = store.MapRequestPath("/upload.bin");
        Assert.AreEqual(new ContentFileStatus(upload.Length, Modified), store.GetFileStatus(mapping));
        using var readBack = new MemoryStream();
        await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(upload.Length), readBack, CancellationToken.None);
        CollectionAssert.AreEqual(upload, readBack.ToArray());
        CollectionAssert.AreEqual(new[] { "upload.bin" }, fileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath).ToArray());
    }

    [TestMethod]
    public async Task WriteUploadAsync_InMemoryFileSystemWithoutAllowUploads_IsNotPermitted()
    {
        var fileSystem = new InMemoryContentFileSystem(new SettableTimeProvider(Modified));
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions());

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), new MemoryStream([1, 2, 3]), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.NotPermitted, result);
        Assert.AreEqual(0, fileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath).Count());
        Assert.AreEqual(0, fileSystem.TotalBytes);
    }

    private static (ContentStore Store, UnitTestInMemoryContentFileSystem FileSystem) UploadStore(long maxUploadBytes)
    {
        var fileSystem = new UnitTestInMemoryContentFileSystem().AddDirectory(Root);
        var options = new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = maxUploadBytes };
        return (new ContentStore(Root, fileSystem, options), fileSystem);
    }

    /// <summary>
    /// A seam that implements only the reads, so its writes are the interface's defaults.
    /// </summary>
    private sealed class UnitTestReadOnlyContentFileSystem : IContentFileSystem
    {
        public ContentEntryKind GetEntryKind(string path) =>
            path == Root ? ContentEntryKind.Directory : ContentEntryKind.None;

        public string ResolveFinalPath(string path) => path;

        public long GetFileLength(string path) => 0;

        public DateTimeOffset GetLastWriteTimeUtc(string path) => DateTimeOffset.UnixEpoch;

        public Stream OpenFileForAsyncRead(string path) => Stream.Null;

        public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => [];
    }
}
