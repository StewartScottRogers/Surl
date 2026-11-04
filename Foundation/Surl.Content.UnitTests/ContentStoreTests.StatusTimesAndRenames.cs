namespace Surl.Content;

public sealed partial class ContentStoreTests
{
    private static readonly DateTimeOffset SetTime = new(2025, 1, 2, 5, 4, 5, TimeSpan.FromHours(2));

    [TestMethod]
    public void GetEntryStatus_File_ReportsItsKindLengthAndLastWriteTime()
    {
        (ContentStore store, _) = ChangeStore();

        ContentEntryStatus? status = store.GetEntryStatus(store.MapRequestPath("/file.txt"));

        Assert.AreEqual(new ContentEntryStatus(ContentEntryKind.File, 4, Modified), status);
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/dir")]
    [DataRow("/dir/")]
    public void GetEntryStatus_Directory_ReportsItsKindAndLastWriteTimeWithNoLength(string requestPath)
    {
        (ContentStore store, _) = ChangeStore();

        ContentEntryStatus? status = store.GetEntryStatus(store.MapRequestPath(requestPath));

        Assert.AreEqual(new ContentEntryStatus(ContentEntryKind.Directory, null, DateTimeOffset.UnixEpoch), status);
    }

    [TestMethod]
    [DataRow("/missing")]
    [DataRow("/file.txt/")]
    [DataRow("/.hidden")]
    [DataRow("/.hidden-dir")]
    [DataRow("/.surl")]
    [DataRow("/.surl/lock")]
    [DataRow("/link")]
    public void GetEntryStatus_NothingVisible_IsNull(string requestPath)
    {
        (ContentStore store, _) = ChangeStore();

        Assert.IsNull(store.GetEntryStatus(store.MapRequestPath(requestPath)));
    }

    [TestMethod]
    [DataRow("/file.txt")]
    [DataRow("/dir")]
    [DataRow("/dir/")]
    [DataRow("/")]
    public void SetLastWriteTime_FileOrDirectory_SetsItInUtc(string requestPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        ContentChangeResult result = store.SetLastWriteTime(mapping, SetTime);

        Assert.AreEqual(ContentChangeResult.Done, result);
        CollectionAssert.Contains(fileSystem.Calls, $"SetLastWriteTimeUtc({mapping.Location}, {SetTime.ToUniversalTime():O})");
        Assert.AreEqual(SetTime, store.GetEntryStatus(mapping)!.LastModifiedUtc);
    }

    [TestMethod]
    [DataRow("/missing")]
    [DataRow("/file.txt/")]
    [DataRow("/.hidden")]
    [DataRow("/.hidden-dir")]
    [DataRow("/.surl/lock")]
    [DataRow("/link")]
    public void SetLastWriteTime_NothingVisible_IsAbsentAndChangesNothing(string requestPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.SetLastWriteTime(store.MapRequestPath(requestPath), SetTime);

        Assert.AreEqual(ContentChangeResult.Absent, result);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    public void RenameEntryWithoutReplacing_File_MovesItWithoutReplacing()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        string renamed = Path.Join(ChangedDirectory, "renamed.txt");

        ContentChangeResult result = store.RenameEntryWithoutReplacing(store.MapRequestPath("/file.txt"), store.MapRequestPath("/dir/renamed.txt"));

        Assert.AreEqual(ContentChangeResult.Done, result);
        CollectionAssert.Contains(fileSystem.Calls, $"MoveFileWithoutReplacing({ChangedFile}, {renamed})");
        CollectionAssert.AreEqual("file"u8.ToArray(), fileSystem.ReadFile(renamed));
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(ChangedFile));
    }

    [TestMethod]
    public void RenameEntryWithoutReplacing_Directory_MovesItWithEverythingInside()
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        string moved = Path.Join(Root, "moved");

        ContentChangeResult result = store.RenameEntryWithoutReplacing(store.MapRequestPath("/dir/"), store.MapRequestPath("/moved"));

        Assert.AreEqual(ContentChangeResult.Done, result);
        CollectionAssert.Contains(fileSystem.Calls, $"MoveDirectory({ChangedDirectory}, {moved})");
        Assert.AreEqual(ContentEntryKind.File, fileSystem.GetEntryKind(Path.Join(moved, "inner.txt")));
    }

    [TestMethod]
    [DataRow("/file.txt", "/dir/inner.txt")]
    [DataRow("/file.txt", "/dir")]
    [DataRow("/dir", "/file.txt")]
    [DataRow("/dir", "/empty")]
    [DataRow("/file.txt", "/file.txt")]
    [DataRow("/dir", "/dir/")]
    public void RenameEntryWithoutReplacing_AnyEntryAtTheDestination_ExistsAndChangesNothing(string sourcePath, string destinationPath)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();

        ContentChangeResult result = store.RenameEntryWithoutReplacing(store.MapRequestPath(sourcePath), store.MapRequestPath(destinationPath));

        Assert.AreEqual(ContentChangeResult.Exists, result);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    [DataRow("/missing", "/new", ContentChangeResult.Absent)]
    [DataRow("/link", "/new", ContentChangeResult.Absent)]
    [DataRow("/file.txt", "/.hidden-new", ContentChangeResult.NotPermitted)]
    [DataRow("/file.txt", "/.surl/new", ContentChangeResult.NotPermitted)]
    [DataRow("/file.txt", "/new/", ContentChangeResult.NotPermitted)]
    [DataRow("/", "/new", ContentChangeResult.NotPermitted)]
    [DataRow("/dir", "/dir/sub", ContentChangeResult.NotPermitted)]
    [DataRow("/file.txt", "/missing/new", ContentChangeResult.NoSuchDirectory)]
    public void RenameEntryWithoutReplacing_RenameEntrysOtherRefusals_AnswerTheSame(string sourcePath, string destinationPath, ContentChangeResult expected)
    {
        (ContentStore store, UnitTestInMemoryContentFileSystem fileSystem) = ChangeStore();
        ContentPathMapping source = store.MapRequestPath(sourcePath);
        ContentPathMapping destination = store.MapRequestPath(destinationPath);

        ContentChangeResult result = store.RenameEntryWithoutReplacing(source, destination);

        Assert.AreEqual(expected, result);
        Assert.AreEqual(expected, store.RenameEntry(source, destination));
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    public void StatusTimeAndRenameWithoutReplacing_UploadsOff_ReadButChangeNothing()
    {
        UnitTestInMemoryContentFileSystem fileSystem = ChangeFileSystem();
        var store = new ContentStore(Root, fileSystem, new ContentExposureOptions());

        Assert.AreEqual(ContentChangeResult.NotPermitted, store.SetLastWriteTime(store.MapRequestPath("/file.txt"), SetTime));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.SetLastWriteTime(store.MapRequestPath("/missing"), SetTime));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.RenameEntryWithoutReplacing(store.MapRequestPath("/file.txt"), store.MapRequestPath("/new")));
        Assert.AreEqual(ContentEntryKind.File, store.GetEntryStatus(store.MapRequestPath("/file.txt"))!.Kind);
        AssertNothingChanged(fileSystem);
    }

    [TestMethod]
    public void StatusTimeAndRenameWithoutReplacing_RefusedOrNullMapping_IsRejected()
    {
        (ContentStore store, _) = ChangeStore();
        ContentPathMapping refused = store.MapRequestPath("/../x");
        ContentPathMapping mapped = store.MapRequestPath("/file.txt");

        Assert.ThrowsExactly<ArgumentException>(() => store.GetEntryStatus(refused));
        Assert.ThrowsExactly<ArgumentException>(() => store.SetLastWriteTime(refused, SetTime));
        Assert.ThrowsExactly<ArgumentException>(() => store.RenameEntryWithoutReplacing(refused, mapped));
        Assert.ThrowsExactly<ArgumentException>(() => store.RenameEntryWithoutReplacing(mapped, refused));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.GetEntryStatus(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.SetLastWriteTime(null!, SetTime));
    }

    [TestMethod]
    public void MoveFileWithoutReplacingAndSetLastWriteTimeUtc_ReadOnlyFileSystem_ThrowNotSupported()
    {
        IContentFileSystem fileSystem = new UnitTestReadOnlyContentFileSystem();

        Assert.ThrowsExactly<NotSupportedException>(() => fileSystem.MoveFileWithoutReplacing(ChangedFile, Path.Join(Root, "new")));
        Assert.ThrowsExactly<NotSupportedException>(() => fileSystem.SetLastWriteTimeUtc(ChangedFile, SetTime));
    }

    [TestMethod]
    public async Task StatusTimeAndRenameWithoutReplacing_InMemoryFileSystem_WorkAndReadBack()
    {
        var fileSystem = new InMemoryContentFileSystem(new SettableTimeProvider(Modified));
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions { AllowUploads = true });
        Assert.AreEqual(ContentChangeResult.Done, store.CreateDirectory(store.MapRequestPath("/dir")));
        Assert.AreEqual(ContentUploadResult.Written, await store.WriteUploadAsync(store.MapRequestPath("/a.txt"), new MemoryStream("abc"u8.ToArray()), CancellationToken.None));
        Assert.AreEqual(ContentUploadResult.Written, await store.WriteUploadAsync(store.MapRequestPath("/b.txt"), new MemoryStream("b"u8.ToArray()), CancellationToken.None));

        Assert.AreEqual(new ContentEntryStatus(ContentEntryKind.File, 3, Modified), store.GetEntryStatus(store.MapRequestPath("/a.txt")));
        Assert.AreEqual(new ContentEntryStatus(ContentEntryKind.Directory, null, Modified), store.GetEntryStatus(store.MapRequestPath("/dir")));
        Assert.AreEqual(ContentChangeResult.Done, store.SetLastWriteTime(store.MapRequestPath("/a.txt"), SetTime));
        Assert.AreEqual(ContentChangeResult.Done, store.SetLastWriteTime(store.MapRequestPath("/dir/"), SetTime.AddDays(1)));
        Assert.AreEqual(ContentChangeResult.Exists, store.RenameEntryWithoutReplacing(store.MapRequestPath("/a.txt"), store.MapRequestPath("/b.txt")));
        Assert.AreEqual(ContentChangeResult.Done, store.RenameEntryWithoutReplacing(store.MapRequestPath("/a.txt"), store.MapRequestPath("/dir/c.txt")));
        Assert.AreEqual(ContentChangeResult.Done, store.RenameEntryWithoutReplacing(store.MapRequestPath("/dir"), store.MapRequestPath("/moved")));

        ContentEntryStatus? file = store.GetEntryStatus(store.MapRequestPath("/moved/c.txt"));
        Assert.AreEqual(new ContentEntryStatus(ContentEntryKind.File, 3, SetTime), file);
        Assert.AreEqual(TimeSpan.Zero, file!.LastModifiedUtc.Offset);
        Assert.AreEqual(SetTime.AddDays(1), store.GetEntryStatus(store.MapRequestPath("/moved"))!.LastModifiedUtc);
        Assert.IsNull(store.GetEntryStatus(store.MapRequestPath("/a.txt")));
        Assert.AreEqual(1, store.GetEntryStatus(store.MapRequestPath("/b.txt"))!.Length);
    }
}
