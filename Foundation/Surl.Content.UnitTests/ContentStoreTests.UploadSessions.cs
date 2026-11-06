namespace Surl.Content;

public sealed partial class ContentStoreTests
{
    private static readonly ContentUploadOpening StartFromExistingBytes = new(StartsFromExistingBytes: true, CreatesMissingFile: true, RefusesExistingFile: false);

    private static readonly ContentUploadOpening Truncate = new(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false);

    private static readonly ContentUploadOpening CreateExclusively = new(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: true);

    private static readonly ContentUploadOpening OpenExistingOnly = new(StartsFromExistingBytes: true, CreatesMissingFile: false, RefusesExistingFile: false);

    [TestMethod]
    public async Task OpenUploadAsync_UploadsOff_IsNotPermittedAndChangesNothing()
    {
        UnitTestInMemoryContentFileSystem fileSystem = ChangeFileSystem();
        var store = new ContentStore(Root, fileSystem, new ContentExposureOptions());

        ContentUploadOpeningOutcome outcome = await store.OpenUploadAsync(store.MapRequestPath("/file.txt"), StartFromExistingBytes, CancellationToken.None);

        Assert.AreEqual(new ContentUploadOpeningOutcome(ContentUploadOpeningResult.NotPermitted, null), outcome);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    [DataRow("/", ContentUploadOpeningResult.IsADirectory)]
    [DataRow("/dir", ContentUploadOpeningResult.IsADirectory)]
    [DataRow("/missing/new.txt", ContentUploadOpeningResult.NoSuchDirectory)]
    [DataRow("/file.txt/new.txt", ContentUploadOpeningResult.NoSuchDirectory)]
    [DataRow("/.hidden", ContentUploadOpeningResult.NotPermitted)]
    [DataRow("/.surl/lock", ContentUploadOpeningResult.NotPermitted)]
    [DataRow("/new.txt/", ContentUploadOpeningResult.NotPermitted)]
    public async Task OpenUploadAsync_LocationThatCannotTakeAnUpload_AnswersWhyAndChangesNothing(string requestPath, ContentUploadOpeningResult expected)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentUploadOpeningOutcome outcome = await store.OpenUploadAsync(store.MapRequestPath(requestPath), StartFromExistingBytes, CancellationToken.None);

        Assert.AreEqual(new ContentUploadOpeningOutcome(expected, null), outcome);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    public async Task OpenUploadAsync_FileThereAndTheOpeningRefusesOne_IsExists()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentUploadOpeningOutcome outcome = await store.OpenUploadAsync(store.MapRequestPath("/file.txt"), CreateExclusively, CancellationToken.None);

        Assert.AreEqual(new ContentUploadOpeningOutcome(ContentUploadOpeningResult.Exists, null), outcome);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    public async Task OpenUploadAsync_NothingThereAndTheOpeningDoesNotCreate_IsAbsent()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentUploadOpeningOutcome outcome = await store.OpenUploadAsync(store.MapRequestPath("/new.txt"), OpenExistingOnly, CancellationToken.None);

        Assert.AreEqual(new ContentUploadOpeningOutcome(ContentUploadOpeningResult.Absent, null), outcome);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    public async Task OpenUploadAsync_StartingFromAnExistingFile_HoldsItsBytesInATemporaryDotFile()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentUploadOpeningOutcome outcome = await store.OpenUploadAsync(store.MapRequestPath("/file.txt"), OpenExistingOnly, CancellationToken.None);

        Assert.AreEqual(ContentUploadOpeningResult.Opened, outcome.Result);
        await using ContentUploadSession session = outcome.Session!;
        Assert.AreEqual(4, session.Length);
        string temporaryFile = SingleTemporaryUploadFile(fileSystem);
        CollectionAssert.AreEqual("file"u8.ToArray(), fileSystem.ReadFile(temporaryFile));
        CollectionAssert.AreEqual("file"u8.ToArray(), fileSystem.ReadFile(ChangedFile));
    }

    [TestMethod]
    public async Task OpenUploadAsync_TruncatingAnExistingFile_StartsEmptyWithoutReadingIt()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentUploadOpeningOutcome outcome = await store.OpenUploadAsync(store.MapRequestPath("/file.txt"), Truncate, CancellationToken.None);

        await using ContentUploadSession session = outcome.Session!;
        Assert.AreEqual(ContentUploadOpeningResult.Opened, outcome.Result);
        Assert.AreEqual(0, session.Length);
        Assert.IsFalse(fileSystem.Calls.Exists(call => call.StartsWith("OpenFileForAsyncRead(", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task OpenUploadAsync_NothingThereAndTheOpeningCreates_StartsEmpty(bool startsFromExistingBytes)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        var opening = new ContentUploadOpening(startsFromExistingBytes, CreatesMissingFile: true, RefusesExistingFile: true);

        ContentUploadOpeningOutcome outcome = await store.OpenUploadAsync(store.MapRequestPath("/dir/new.txt"), opening, CancellationToken.None);

        await using ContentUploadSession session = outcome.Session!;
        Assert.AreEqual(ContentUploadOpeningResult.Opened, outcome.Result);
        Assert.AreEqual(0, session.Length);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(ChangedDirectory, "new.txt")));
    }

    [TestMethod]
    public async Task OpenUploadAsync_CopyingTheTargetFails_DeletesTheTemporaryFileAndRethrows()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        fileSystem.FailReads = true;

        await Assert.ThrowsExactlyAsync<IOException>(
            () => store.OpenUploadAsync(store.MapRequestPath("/file.txt"), StartFromExistingBytes, CancellationToken.None));

        Assert.IsEmpty(TemporaryUploadFiles(fileSystem));
    }

    [TestMethod]
    public async Task OpenUploadAsync_CancelledToken_ThrowsBeforeLookingAtAnything()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        ContentPathMapping mapping = store.MapRequestPath("/file.txt");
        fileSystem.Calls.Clear();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => store.OpenUploadAsync(mapping, StartFromExistingBytes, new CancellationToken(canceled: true)));

        Assert.IsEmpty(fileSystem.Calls);
    }

    [TestMethod]
    public async Task OpenUploadAsync_RefusedOrNullArguments_AreRejected()
    {
        (ContentStore store, _) = ChangeStore();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => store.OpenUploadAsync(store.MapRequestPath("/../x"), StartFromExistingBytes, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => store.OpenUploadAsync(store.MapRequestPath("/file.txt"), null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task OpenUploadAsync_ReadOnlyFileSystem_ThrowsNotSupported()
    {
        var store = new ContentStore(Root, new UnitTestReadOnlyContentFileSystem(), new ContentExposureOptions { AllowUploads = true });

        await Assert.ThrowsExactlyAsync<NotSupportedException>(
            () => store.OpenUploadAsync(store.MapRequestPath("/upload.bin"), Truncate, CancellationToken.None));
    }

    [TestMethod]
    public async Task WriteAtAsyncAndReadAtAsync_Offsets_ZeroFillTheGapAndReadBack()
    {
        await using ContentUploadSession session = await OpenedSessionAsync(ChangeStore().Store, "/file.txt", StartFromExistingBytes);

        Assert.AreEqual(ContentUploadResult.Written, await session.WriteAtAsync(1, "IL"u8.ToArray(), CancellationToken.None));
        Assert.AreEqual(ContentUploadResult.Written, await session.WriteAtAsync(7, "!"u8.ToArray(), CancellationToken.None));

        Assert.AreEqual(8, session.Length);
        byte[] buffer = new byte[10];
        Assert.AreEqual(8, await session.ReadAtAsync(0, buffer, CancellationToken.None));
        CollectionAssert.AreEqual(new byte[] { (byte)'f', (byte)'I', (byte)'L', (byte)'e', 0, 0, 0, (byte)'!', 0, 0 }, buffer);
        Assert.AreEqual(3, await session.ReadAtAsync(5, buffer, CancellationToken.None));
        Assert.AreEqual(0, await session.ReadAtAsync(8, buffer, CancellationToken.None));
        Assert.AreEqual(0, await session.ReadAtAsync(100, buffer, CancellationToken.None));
    }

    [TestMethod]
    public async Task SetLengthAsync_ShorterThenLonger_CutsAndGrowsWithZeroBytes()
    {
        await using ContentUploadSession session = await OpenedSessionAsync(ChangeStore().Store, "/file.txt", StartFromExistingBytes);

        Assert.AreEqual(ContentUploadResult.Written, await session.SetLengthAsync(2));
        Assert.AreEqual(ContentUploadResult.Written, await session.SetLengthAsync(5));

        Assert.AreEqual(5, session.Length);
        byte[] buffer = new byte[5];
        Assert.AreEqual(5, await session.ReadAtAsync(0, buffer, CancellationToken.None));
        CollectionAssert.AreEqual(new byte[] { (byte)'f', (byte)'i', 0, 0, 0 }, buffer);
    }

    [TestMethod]
    public async Task CommitAsync_ExistingTarget_RenamesTheTemporaryFileOverIt()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        await using ContentUploadSession session = await OpenedSessionAsync(store, "/file.txt", StartFromExistingBytes);
        await session.WriteAtAsync(4, "-more"u8.ToArray(), CancellationToken.None);
        string temporaryFile = SingleTemporaryUploadFile(fileSystem);

        ContentUploadResult result = await session.CommitAsync(CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, result);
        CollectionAssert.AreEqual("file-more"u8.ToArray(), fileSystem.ReadFile(ChangedFile));
        CollectionAssert.Contains(fileSystem.Calls, $"MoveFileReplacing({temporaryFile}, {ChangedFile})");
        Assert.IsEmpty(TemporaryUploadFiles(fileSystem));
    }

    [TestMethod]
    public async Task CommitAsync_NothingThere_CreatesTheFile()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        string target = Path.Join(ChangedDirectory, "new.txt");
        await using ContentUploadSession session = await OpenedSessionAsync(store, "/dir/new.txt", CreateExclusively);
        await session.WriteAtAsync(0, "new"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, await session.CommitAsync(CancellationToken.None));

        CollectionAssert.AreEqual("new"u8.ToArray(), fileSystem.ReadFile(target));
        CollectionAssert.AreEqual(new[] { Path.Join(ChangedDirectory, "inner.txt"), target }, fileSystem.EntriesDirectlyInside(ChangedDirectory));
    }

    [TestMethod]
    public async Task DisposeAsync_WithoutACommit_DeletesTheTemporaryFileAndKeepsTheTarget()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        ContentUploadSession session = await OpenedSessionAsync(store, "/file.txt", Truncate);
        await session.WriteAtAsync(0, "discarded"u8.ToArray(), CancellationToken.None);

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.IsEmpty(TemporaryUploadFiles(fileSystem));
        CollectionAssert.AreEqual("file"u8.ToArray(), fileSystem.ReadFile(ChangedFile));
        Assert.AreEqual(1, fileSystem.Calls.Count(call => call.StartsWith("DeleteFile(", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task WriteAtAsync_PastMaxUploadBytes_IsTooLargeDeletesTheTemporaryFileAndKeepsTheTarget()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore(maxUploadBytes: 10);
        await using ContentUploadSession session = await OpenedSessionAsync(store, "/file.txt", StartFromExistingBytes);
        Assert.AreEqual(ContentUploadResult.Written, await session.WriteAtAsync(4, "567890"u8.ToArray(), CancellationToken.None));

        ContentUploadResult result = await session.WriteAtAsync(10, "!"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.TooLarge, result);
        Assert.AreEqual(10, session.Length);
        Assert.IsEmpty(TemporaryUploadFiles(fileSystem));
        CollectionAssert.AreEqual("file"u8.ToArray(), fileSystem.ReadFile(ChangedFile));
        Assert.AreEqual(ContentUploadResult.TooLarge, await session.WriteAtAsync(0, "x"u8.ToArray(), CancellationToken.None));
        Assert.AreEqual(ContentUploadResult.TooLarge, await session.SetLengthAsync(0));
        Assert.AreEqual(ContentUploadResult.TooLarge, await session.CommitAsync(CancellationToken.None));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.ReadAtAsync(0, new byte[1], CancellationToken.None));
        Assert.IsFalse(fileSystem.Calls.Exists(call => call.StartsWith("MoveFileReplacing(", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task SetLengthAsync_PastMaxUploadBytes_IsTooLargeAndDeletesTheTemporaryFile()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore(maxUploadBytes: 10);
        await using ContentUploadSession session = await OpenedSessionAsync(store, "/file.txt", StartFromExistingBytes);
        Assert.AreEqual(ContentUploadResult.Written, await session.SetLengthAsync(10));

        ContentUploadResult result = await session.SetLengthAsync(11);

        Assert.AreEqual(ContentUploadResult.TooLarge, result);
        Assert.IsEmpty(TemporaryUploadFiles(fileSystem));
        CollectionAssert.AreEqual("file"u8.ToArray(), fileSystem.ReadFile(ChangedFile));
    }

    [TestMethod]
    public async Task WriteAtAsync_CancelledToken_EndsTheSessionDeletingTheTemporaryFile()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        await using ContentUploadSession session = await OpenedSessionAsync(store, "/file.txt", StartFromExistingBytes);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => session.WriteAtAsync(0, "x"u8.ToArray(), new CancellationToken(canceled: true)));

        Assert.IsEmpty(TemporaryUploadFiles(fileSystem));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.WriteAtAsync(0, "x"u8.ToArray(), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.SetLengthAsync(0));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.CommitAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task CommitAsync_CancelledToken_ThrowsAndLeavesTheUploadOpen()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        await using ContentUploadSession session = await OpenedSessionAsync(store, "/file.txt", Truncate);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => session.CommitAsync(new CancellationToken(canceled: true)));

        Assert.AreEqual(ContentUploadResult.Written, await session.CommitAsync(CancellationToken.None));
        Assert.AreEqual(0, fileSystem.ReadFile(ChangedFile).Length);
    }

    [TestMethod]
    public async Task CommitAsync_DirectoryAppearedAtTheTarget_IsNotPermittedAndDeletesTheTemporaryFile()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        string target = Path.Join(Root, "new.txt");
        await using ContentUploadSession session = await OpenedSessionAsync(store, "/new.txt", Truncate);
        fileSystem.AddDirectory(target);

        ContentUploadResult result = await session.CommitAsync(CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.NotPermitted, result);
        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(target));
        Assert.IsEmpty(TemporaryUploadFiles(fileSystem));
    }

    [TestMethod]
    public async Task CommitAsync_ItsDirectoryWentAway_IsNotPermitted()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        await using ContentUploadSession session = await OpenedSessionAsync(store, "/dir/new.txt", Truncate);
        fileSystem.RemoveEmptyDirectory(ChangedDirectory);

        ContentUploadResult result = await session.CommitAsync(CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.NotPermitted, result);
        Assert.IsFalse(fileSystem.Calls.Exists(call => call.StartsWith("MoveFileReplacing(", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task CommitAsync_RenameFails_DeletesTheTemporaryFileAndRethrows()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        await using ContentUploadSession session = await OpenedSessionAsync(store, "/file.txt", Truncate);
        fileSystem.FailMoves = true;

        await Assert.ThrowsExactlyAsync<IOException>(() => session.CommitAsync(CancellationToken.None));

        Assert.IsEmpty(TemporaryUploadFiles(fileSystem));
        CollectionAssert.AreEqual("file"u8.ToArray(), fileSystem.ReadFile(ChangedFile));
    }

    [TestMethod]
    public async Task EverySessionCall_AfterCommitOrDispose_IsRefused()
    {
        (ContentStore store, _) = ChangeStore();
        ContentUploadSession session = await OpenedSessionAsync(store, "/file.txt", Truncate);
        await session.CommitAsync(CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.ReadAtAsync(0, new byte[1], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.CommitAsync(CancellationToken.None));
        await session.DisposeAsync();
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => session.WriteAtAsync(0, new byte[1], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => session.ReadAtAsync(0, new byte[1], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => session.SetLengthAsync(0));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => session.CommitAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task EverySessionCall_NegativeOffsetOrLength_Throws()
    {
        await using ContentUploadSession session = await OpenedSessionAsync(ChangeStore().Store, "/file.txt", Truncate);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => session.WriteAtAsync(-1, new byte[1], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => session.ReadAtAsync(-1, new byte[1], CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => session.SetLengthAsync(-1));
    }

    [TestMethod]
    public async Task OpenUploadAsync_InMemoryFileSystem_WritesResizesAndCommits()
    {
        var fileSystem = new InMemoryContentFileSystem(new SettableTimeProvider(Modified));
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 8 });
        ContentPathMapping mapping = store.MapRequestPath("/upload.bin");

        await using (ContentUploadSession session = await OpenedSessionAsync(store, "/upload.bin", CreateExclusively))
        {
            await session.WriteAtAsync(4, "tail"u8.ToArray(), CancellationToken.None);
            await session.SetLengthAsync(6);
            await session.WriteAtAsync(0, "he"u8.ToArray(), CancellationToken.None);
            Assert.AreEqual(ContentUploadResult.Written, await session.CommitAsync(CancellationToken.None));
        }

        Assert.AreEqual(new ContentFileStatus(6, Modified), store.GetFileStatus(mapping));
        using var readBack = new MemoryStream();
        await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(6), readBack, CancellationToken.None);
        CollectionAssert.AreEqual(new byte[] { (byte)'h', (byte)'e', 0, 0, (byte)'t', (byte)'a' }, readBack.ToArray());
        Assert.AreEqual(6, fileSystem.TotalBytes);
        Assert.AreEqual(ContentUploadOpeningResult.Exists, (await store.OpenUploadAsync(mapping, CreateExclusively, CancellationToken.None)).Result);
    }

    [TestMethod]
    public async Task OpenUploadAsync_InMemoryFileSystemTooLargeOrDiscarded_LeavesOnlyTheTarget()
    {
        var fileSystem = new InMemoryContentFileSystem(new SettableTimeProvider(Modified));
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions { AllowUploads = true, MaxUploadBytes = 8 });
        await using (ContentUploadSession first = await OpenedSessionAsync(store, "/upload.bin", CreateExclusively))
        {
            await first.WriteAtAsync(0, "kept"u8.ToArray(), CancellationToken.None);
            await first.CommitAsync(CancellationToken.None);
        }

        await using (ContentUploadSession tooLarge = await OpenedSessionAsync(store, "/upload.bin", StartFromExistingBytes))
        {
            Assert.AreEqual(ContentUploadResult.TooLarge, await tooLarge.WriteAtAsync(8, "x"u8.ToArray(), CancellationToken.None));
        }

        await using (ContentUploadSession discarded = await OpenedSessionAsync(store, "/upload.bin", Truncate))
        {
            await discarded.WriteAtAsync(0, "gone"u8.ToArray(), CancellationToken.None);
        }

        CollectionAssert.AreEqual(new[] { "upload.bin" }, fileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath).ToArray());
        Assert.AreEqual(4, fileSystem.TotalBytes);
    }

    [TestMethod]
    public async Task SetLengthAsync_InMemoryFileSystemFull_EndsTheSessionAndRethrows()
    {
        var fileSystem = new InMemoryContentFileSystem(new SettableTimeProvider(Modified), maxTotalBytes: 4);
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions { AllowUploads = true });
        await using ContentUploadSession session = await OpenedSessionAsync(store, "/upload.bin", Truncate);

        await Assert.ThrowsExactlyAsync<IOException>(() => session.SetLengthAsync(5));

        Assert.AreEqual(0, fileSystem.EnumerateDirectoryEntryNames(InMemoryContentFileSystem.RootPath).Count());
        Assert.AreEqual(0, fileSystem.TotalBytes);
    }

    [TestMethod]
    public void OpenFileForAsyncReadWrite_ReadOnlyFileSystem_ThrowsNotSupported()
    {
        IContentFileSystem fileSystem = new UnitTestReadOnlyContentFileSystem();

        Assert.ThrowsExactly<NotSupportedException>(() => fileSystem.OpenFileForAsyncReadWrite(UploadPath));
    }

    private static async Task<ContentUploadSession> OpenedSessionAsync(ContentStore store, string requestPath, ContentUploadOpening opening)
    {
        ContentUploadOpeningOutcome outcome = await store.OpenUploadAsync(store.MapRequestPath(requestPath), opening, CancellationToken.None);
        Assert.AreEqual(ContentUploadOpeningResult.Opened, outcome.Result);
        return outcome.Session!;
    }

    private static List<string> TemporaryUploadFiles(UnitTestInMemoryContentFileSystem fileSystem) =>
        fileSystem.EntriesDirectlyInside(Root)
            .Concat(fileSystem.EntriesDirectlyInside(ChangedDirectory))
            .Where(entry => Path.GetFileName(entry).StartsWith(".surl-upload-", StringComparison.Ordinal))
            .ToList();

    private static string SingleTemporaryUploadFile(UnitTestInMemoryContentFileSystem fileSystem)
    {
        List<string> temporaryFiles = TemporaryUploadFiles(fileSystem);
        Assert.HasCount(1, temporaryFiles);
        return temporaryFiles[0];
    }
}
