namespace Surl.Content;

public sealed partial class ContentStoreTests
{
    private static string ChangedFile => Path.Join(Root, "file.txt");

    private static string ChangedDirectory => Path.Join(Root, "dir");

    [TestMethod]
    public void DeleteFile_File_IsDeleted()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.DeleteFile(store.MapRequestPath("/file.txt"));

        Assert.AreEqual(ContentChangeResult.Done, result);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(ChangedFile));
    }

    [TestMethod]
    [DataRow("/missing")]
    [DataRow("/dir")]
    [DataRow("/file.txt/")]
    [DataRow("/.hidden")]
    [DataRow("/.surl/lock")]
    [DataRow("/.SURL/lock")]
    [DataRow("/link")]
    public void DeleteFile_NoVisibleFile_IsAbsentAndDeletesNothing(string requestPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.DeleteFile(store.MapRequestPath(requestPath));

        Assert.AreEqual(ContentChangeResult.Absent, result);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    public void RenameEntry_File_MovesItsBytesToTheNewName()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.RenameEntry(store.MapRequestPath("/file.txt"), store.MapRequestPath("/dir/renamed.txt"));

        Assert.AreEqual(ContentChangeResult.Done, result);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(ChangedFile));
        CollectionAssert.AreEqual("file"u8.ToArray(), fileSystem.ReadFile(Path.Join(ChangedDirectory, "renamed.txt")));
    }

    [TestMethod]
    public void RenameEntry_OntoAnExistingFile_ReplacesIt()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        string other = Path.Join(Root, "other.txt");
        fileSystem.AddFile(other, "other"u8.ToArray(), Modified);

        ContentChangeResult result = store.RenameEntry(store.MapRequestPath("/file.txt"), store.MapRequestPath("/other.txt"));

        Assert.AreEqual(ContentChangeResult.Done, result);
        CollectionAssert.AreEqual("file"u8.ToArray(), fileSystem.ReadFile(other));
        CollectionAssert.Contains(fileSystem.Calls, $"MoveFileReplacing({ChangedFile}, {other})");
    }

    [TestMethod]
    [DataRow("/dir")]
    [DataRow("/dir/")]
    public void RenameEntry_Directory_MovesItWithEverythingInside(string sourcePath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        string moved = Path.Join(Root, "moved");

        ContentChangeResult result = store.RenameEntry(store.MapRequestPath(sourcePath), store.MapRequestPath("/moved"));

        Assert.AreEqual(ContentChangeResult.Done, result);
        CollectionAssert.Contains(fileSystem.Calls, $"MoveDirectory({ChangedDirectory}, {moved})");
        Assert.AreEqual(ContentEntryKind.File, fileSystem.GetEntryKind(Path.Join(moved, "inner.txt")));
    }

    [TestMethod]
    [DataRow("/file.txt", "/file.txt")]
    [DataRow("/dir", "/dir/")]
    public void RenameEntry_OntoItself_IsDoneAndMovesNothing(string sourcePath, string destinationPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.RenameEntry(store.MapRequestPath(sourcePath), store.MapRequestPath(destinationPath));

        Assert.AreEqual(ContentChangeResult.Done, result);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    [DataRow("/missing")]
    [DataRow("/file.txt/")]
    [DataRow("/.hidden")]
    [DataRow("/.surl/lock")]
    [DataRow("/link")]
    public void RenameEntry_NoVisibleSource_IsAbsent(string sourcePath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.RenameEntry(store.MapRequestPath(sourcePath), store.MapRequestPath("/new"));

        Assert.AreEqual(ContentChangeResult.Absent, result);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    [DataRow("/", "/new")]
    [DataRow("/dir", "/")]
    [DataRow("/file.txt", "/.hidden-new")]
    [DataRow("/file.txt", "/.surl")]
    [DataRow("/file.txt", "/.SURL/new")]
    [DataRow("/file.txt", "/new/")]
    [DataRow("/dir", "/dir/sub")]
    [DataRow("/dir", "/dir/inner.txt")]
    public void RenameEntry_ForbiddenSourceOrDestination_IsNotPermitted(string sourcePath, string destinationPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.RenameEntry(store.MapRequestPath(sourcePath), store.MapRequestPath(destinationPath));

        Assert.AreEqual(ContentChangeResult.NotPermitted, result);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    [DataRow("/file.txt", "/missing/new")]
    [DataRow("/dir", "/file.txt/new")]
    public void RenameEntry_DestinationOutsideAnExistingDirectory_IsNoSuchDirectory(string sourcePath, string destinationPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.RenameEntry(store.MapRequestPath(sourcePath), store.MapRequestPath(destinationPath));

        Assert.AreEqual(ContentChangeResult.NoSuchDirectory, result);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    [DataRow("/file.txt", "/dir")]
    [DataRow("/dir", "/file.txt")]
    [DataRow("/dir", "/empty")]
    public void RenameEntry_DirectoryOrADirectoryOntoAFileInTheWay_Exists(string sourcePath, string destinationPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.RenameEntry(store.MapRequestPath(sourcePath), store.MapRequestPath(destinationPath));

        Assert.AreEqual(ContentChangeResult.Exists, result);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    [DataRow("/new")]
    [DataRow("/new/")]
    [DataRow("/dir/new")]
    public void CreateDirectory_NewName_IsCreated(string requestPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.CreateDirectory(store.MapRequestPath(requestPath));

        Assert.AreEqual(ContentChangeResult.Done, result);
        Assert.AreEqual(ContentEntryKind.Directory, store.GetEntryKind(store.MapRequestPath(requestPath)));
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/dir")]
    [DataRow("/file.txt")]
    [DataRow("/file.txt/")]
    public void CreateDirectory_EntryAlreadyThere_Exists(string requestPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.CreateDirectory(store.MapRequestPath(requestPath));

        Assert.AreEqual(ContentChangeResult.Exists, result);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    [DataRow("/missing/new")]
    [DataRow("/file.txt/new")]
    public void CreateDirectory_NotDirectlyInsideADirectory_IsNoSuchDirectory(string requestPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.CreateDirectory(store.MapRequestPath(requestPath));

        Assert.AreEqual(ContentChangeResult.NoSuchDirectory, result);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    public void CreateDirectory_ServedRootIsTheFileSystemRootAndMissing_IsNoSuchDirectory()
    {
        string root = Path.DirectorySeparatorChar.ToString();
        var store = new ContentStore(root, new UnitTestInMemoryContentFileSystem(), new ContentExposureOptions { AllowUploads = true });

        ContentChangeResult result = store.CreateDirectory(store.MapRequestPath("/"));

        Assert.AreEqual(ContentChangeResult.NoSuchDirectory, result);
    }

    [TestMethod]
    [DataRow("/.hidden-new")]
    [DataRow("/.surl")]
    [DataRow("/.surl/new")]
    [DataRow("/.Surl/new")]
    [DataRow("/link/new")]
    public void CreateDirectory_HiddenName_IsNotPermitted(string requestPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.CreateDirectory(store.MapRequestPath(requestPath));

        Assert.AreEqual(ContentChangeResult.NotPermitted, result);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    [DataRow("/empty")]
    [DataRow("/empty/")]
    public void RemoveEmptyDirectory_EmptyDirectory_IsRemoved(string requestPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.RemoveEmptyDirectory(store.MapRequestPath(requestPath));

        Assert.AreEqual(ContentChangeResult.Done, result);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(Root, "empty")));
    }

    [TestMethod]
    [DataRow("/dir")]
    [DataRow("/holds-hidden")]
    public void RemoveEmptyDirectory_AnyEntryInside_IsNotEmpty(string requestPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.RemoveEmptyDirectory(store.MapRequestPath(requestPath));

        Assert.AreEqual(ContentChangeResult.NotEmpty, result);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    [DataRow("/missing")]
    [DataRow("/file.txt")]
    [DataRow("/.hidden-dir")]
    [DataRow("/.surl")]
    [DataRow("/.SURL/")]
    public void RemoveEmptyDirectory_NoVisibleDirectory_IsAbsent(string requestPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.RemoveEmptyDirectory(store.MapRequestPath(requestPath));

        Assert.AreEqual(ContentChangeResult.Absent, result);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    public void RemoveEmptyDirectory_ServedRoot_IsNotPermitted()
    {
        var fileSystem = new UnitTestInMemoryContentFileSystem().AddDirectory(Root);
        var store = new ContentStore(Root, fileSystem, new ContentExposureOptions { AllowUploads = true });

        ContentChangeResult result = store.RemoveEmptyDirectory(store.MapRequestPath("/"));

        Assert.AreEqual(ContentChangeResult.NotPermitted, result);
        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(Root));
    }

    [TestMethod]
    public void EveryChange_UploadsOff_IsNotPermittedAndChangesNothing()
    {
        UnitTestInMemoryContentFileSystem fileSystem = ChangeFileSystem();
        var store = new ContentStore(Root, fileSystem, new ContentExposureOptions { ListDirectories = true });

        Assert.AreEqual(ContentChangeResult.NotPermitted, store.DeleteFile(store.MapRequestPath("/file.txt")));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.RenameEntry(store.MapRequestPath("/file.txt"), store.MapRequestPath("/new")));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.CreateDirectory(store.MapRequestPath("/new")));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.RemoveEmptyDirectory(store.MapRequestPath("/empty")));
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    public async Task EveryChange_RefusedOrNullMapping_IsRejected()
    {
        (ContentStore store, _) = ChangeStore();
        ContentPathMapping refused = store.MapRequestPath("/../x");
        ContentPathMapping mapped = store.MapRequestPath("/file.txt");

        Assert.ThrowsExactly<ArgumentException>(() => store.DeleteFile(refused));
        Assert.ThrowsExactly<ArgumentException>(() => store.RenameEntry(refused, mapped));
        Assert.ThrowsExactly<ArgumentException>(() => store.RenameEntry(mapped, refused));
        Assert.ThrowsExactly<ArgumentException>(() => store.CreateDirectory(refused));
        Assert.ThrowsExactly<ArgumentException>(() => store.RemoveEmptyDirectory(refused));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.DeleteFile(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.RenameEntry(mapped, null!));
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => store.AppendUploadAsync(refused, new MemoryStream([1]), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => store.AppendUploadAsync(mapped, null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task AppendUploadAsync_ExistingFile_AppendsThroughATemporaryDotFile()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentUploadResult result = await store.AppendUploadAsync(store.MapRequestPath("/file.txt"), new MemoryStream("-more"u8.ToArray()), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, result);
        CollectionAssert.AreEqual("file-more"u8.ToArray(), fileSystem.ReadFile(ChangedFile));
        string created = fileSystem.Calls.Single(call => call.StartsWith("CreateFileForAsyncWrite(", StringComparison.Ordinal));
        StringAssert.StartsWith(created, $"CreateFileForAsyncWrite({Path.Join(Root, ".surl-upload-")}", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task AppendUploadAsync_NothingThere_CreatesTheFile()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentUploadResult result = await store.AppendUploadAsync(store.MapRequestPath("/dir/new.txt"), new MemoryStream("new"u8.ToArray()), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, result);
        CollectionAssert.AreEqual("new"u8.ToArray(), fileSystem.ReadFile(Path.Join(ChangedDirectory, "new.txt")));
    }

    [TestMethod]
    public async Task AppendUploadAsync_ExistingPlusAppendedExactlyTheLimit_IsWritten()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore(maxUploadBytes: 10);

        ContentUploadResult result = await store.AppendUploadAsync(store.MapRequestPath("/file.txt"), new MemoryStream("456789"u8.ToArray()), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, result);
        CollectionAssert.AreEqual("file456789"u8.ToArray(), fileSystem.ReadFile(ChangedFile));
    }

    [TestMethod]
    public async Task AppendUploadAsync_ExistingPlusAppendedPastTheLimit_IsTooLargeAndKeepsTheOriginal()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore(maxUploadBytes: 10);
        using var source = new MemoryStream(new byte[1_000]);

        ContentUploadResult result = await store.AppendUploadAsync(store.MapRequestPath("/file.txt"), source, CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.TooLarge, result);
        Assert.AreEqual(7, source.Position);
        CollectionAssert.AreEqual("file"u8.ToArray(), fileSystem.ReadFile(ChangedFile));
        Assert.IsFalse(fileSystem.EntriesDirectlyInside(Root).Exists(entry => entry.Contains(".surl-upload-", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task AppendUploadAsync_ExistingFileAlreadyPastTheLimit_IsTooLargeWithoutReading()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore(maxUploadBytes: 3);
        using var source = new MemoryStream([]);

        ContentUploadResult result = await store.AppendUploadAsync(store.MapRequestPath("/file.txt"), source, CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.TooLarge, result);
        CollectionAssert.AreEqual("file"u8.ToArray(), fileSystem.ReadFile(ChangedFile));
    }

    [TestMethod]
    public async Task AppendUploadAsync_SourceFailsMidAppend_KeepsTheOriginalAndRethrows()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        await Assert.ThrowsExactlyAsync<IOException>(
            () => store.AppendUploadAsync(store.MapRequestPath("/file.txt"), new FailingOnSecondReadStream([1, 2, 3]), CancellationToken.None));

        CollectionAssert.AreEqual("file"u8.ToArray(), fileSystem.ReadFile(ChangedFile));
        Assert.IsFalse(fileSystem.EntriesDirectlyInside(Root).Exists(entry => entry.Contains(".surl-upload-", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("/dir")]
    [DataRow("/file.txt/")]
    [DataRow("/missing/new.txt")]
    [DataRow("/.hidden")]
    [DataRow("/.surl/lock")]
    [DataRow("/link")]
    public async Task AppendUploadAsync_WhereAnUploadIsNotPermitted_IsNotPermitted(string requestPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentUploadResult result = await store.AppendUploadAsync(store.MapRequestPath(requestPath), new MemoryStream([1]), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.NotPermitted, result);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    public async Task AppendUploadAsync_Cancelled_ThrowsWithoutCreatingAFile()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => store.AppendUploadAsync(store.MapRequestPath("/file.txt"), new MemoryStream([1]), cancellation.Token));

        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    public void MoveDirectoryAndRemoveEmptyDirectory_ReadOnlyFileSystem_ThrowNotSupported()
    {
        IContentFileSystem fileSystem = new UnitTestReadOnlyContentFileSystem();

        Assert.ThrowsExactly<NotSupportedException>(() => fileSystem.MoveDirectory(ChangedDirectory, Path.Join(Root, "moved")));
        Assert.ThrowsExactly<NotSupportedException>(() => fileSystem.RemoveEmptyDirectory(ChangedDirectory));
    }

    [TestMethod]
    public async Task EveryChange_InMemoryFileSystem_WorksAndReadsBack()
    {
        var fileSystem = new InMemoryContentFileSystem(new SettableTimeProvider(Modified));
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 8 });

        Assert.AreEqual(ContentChangeResult.Done, store.CreateDirectory(store.MapRequestPath("/dir")));
        Assert.AreEqual(ContentUploadResult.Written, await store.AppendUploadAsync(store.MapRequestPath("/dir/a.txt"), new MemoryStream("abc"u8.ToArray()), CancellationToken.None));
        Assert.AreEqual(ContentUploadResult.Written, await store.AppendUploadAsync(store.MapRequestPath("/dir/a.txt"), new MemoryStream("def"u8.ToArray()), CancellationToken.None));
        Assert.AreEqual(ContentUploadResult.TooLarge, await store.AppendUploadAsync(store.MapRequestPath("/dir/a.txt"), new MemoryStream("ghi"u8.ToArray()), CancellationToken.None));
        Assert.AreEqual(ContentChangeResult.NotEmpty, store.RemoveEmptyDirectory(store.MapRequestPath("/dir")));
        Assert.AreEqual(ContentChangeResult.Done, store.RenameEntry(store.MapRequestPath("/dir"), store.MapRequestPath("/moved")));
        Assert.AreEqual(ContentChangeResult.Done, store.RenameEntry(store.MapRequestPath("/moved/a.txt"), store.MapRequestPath("/b.txt")));

        ContentPathMapping renamed = store.MapRequestPath("/b.txt");
        using var readBack = new MemoryStream();
        await store.CopyFileBytesAsync(renamed, ContentByteRange.WholeFile(6), readBack, CancellationToken.None);
        CollectionAssert.AreEqual("abcdef"u8.ToArray(), readBack.ToArray());
        Assert.AreEqual(ContentChangeResult.Done, store.DeleteFile(renamed));
        Assert.AreEqual(ContentChangeResult.Done, store.RemoveEmptyDirectory(store.MapRequestPath("/moved")));
        Assert.AreEqual(0, fileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath).Count());
        Assert.AreEqual(0, fileSystem.TotalBytes);
    }

    // The root holds file.txt ("file"), dir/inner.txt, the empty directory empty, holds-hidden
    // with only a dot-file inside, the dot-files .hidden and .hidden-dir, the service-state file
    // .surl/lock, and link, a symbolic link to file.txt that is not followed.
    private static UnitTestInMemoryContentFileSystem ChangeFileSystem() =>
        new UnitTestInMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(ChangedFile, "file"u8.ToArray(), Modified)
            .AddDirectory(ChangedDirectory)
            .AddFile(Path.Join(ChangedDirectory, "inner.txt"), "inner"u8.ToArray(), Modified)
            .AddDirectory(Path.Join(Root, "empty"))
            .AddDirectory(Path.Join(Root, "holds-hidden"))
            .AddFile(Path.Join(Root, "holds-hidden", ".kept"))
            .AddFile(Path.Join(Root, ".hidden"))
            .AddDirectory(Path.Join(Root, ".hidden-dir"))
            .AddDirectory(ServiceStateFolder)
            .AddFile(Path.Join(ServiceStateFolder, "lock"))
            .AddSymbolicLink(Path.Join(Root, "link"), ChangedFile);

    private static (ContentStore Store, UnitTestInMemoryContentFileSystem FileSystem) ChangeStore(long maxUploadBytes = 0)
    {
        UnitTestInMemoryContentFileSystem fileSystem = ChangeFileSystem();
        var options = new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = maxUploadBytes };
        return (new ContentStore(Root, fileSystem, options), fileSystem);
    }

    private static void AssertNothingChanged(UnitTestInMemoryContentFileSystem fileSystem)
    {
        string[] changes = ["CreateFileForAsyncWrite(", "OpenFileForAsyncReadWrite(", "DeleteFile(", "MoveFileReplacing(", "MoveFileWithoutReplacing(", "SetLastWriteTimeUtc(", "MoveDirectory(", "CreateDirectory(", "RemoveEmptyDirectory("];
        Assert.IsFalse(
            fileSystem.Calls.Exists(call => Array.Exists(changes, change => call.StartsWith(change, StringComparison.Ordinal))),
            string.Join(Environment.NewLine, fileSystem.Calls));
    }
}
