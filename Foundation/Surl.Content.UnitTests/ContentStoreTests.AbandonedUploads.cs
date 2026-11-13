namespace Surl.Content;

public sealed partial class ContentStoreTests
{
    private static readonly DateTimeOffset SweepNow = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset LongAgo = SweepNow - ContentStore.AbandonedUploadAge - TimeSpan.FromSeconds(1);

    private static readonly DateTimeOffset JustInsideTheBound = SweepNow - ContentStore.AbandonedUploadAge;

    private static string UploadFileName(int number) => $".surl-upload-{number:x32}";

    private static ContentStore SweepStore(IContentFileSystem fileSystem, params ContentShare[] shares) =>
        new(Root, fileSystem, new ContentExposureOptions(), shares, []);

    [TestMethod]
    public void RemoveAbandonedUploadFiles_AFileOlderThanTheBoundAndAYoungerOne_RemovesOnlyTheOlder()
    {
        string old = Path.Join(Root, UploadFileName(1));
        string young = Path.Join(Root, UploadFileName(2));
        string edge = Path.Join(Root, UploadFileName(3));
        UnitTestInMemoryContentFileSystem fileSystem = new UnitTestInMemoryContentFileSystem()
            .AddFile(old, [1], LongAgo)
            .AddFile(young, [1], SweepNow)
            .AddFile(edge, [1], JustInsideTheBound);
        ContentStore store = SweepStore(fileSystem);

        int removed = store.RemoveAbandonedUploadFiles(new SettableTimeProvider(SweepNow));

        Assert.AreEqual(1, removed);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(old));
        Assert.AreEqual(ContentEntryKind.File, fileSystem.GetEntryKind(young));
        Assert.AreEqual(ContentEntryKind.File, fileSystem.GetEntryKind(edge));
    }

    [TestMethod]
    public void RemoveAbandonedUploadFiles_OldFilesOfOtherNames_KeepsThemAll()
    {
        string[] names =
        [
            "report.txt",
            ".surl-upload-",
            ".surl-upload-xyz",
            ".surl-upload-" + new string('g', 32),
            UploadFileName(4) + "0",
            ".SURL-UPLOAD-" + new string('0', 32),
        ];
        UnitTestInMemoryContentFileSystem fileSystem = new();
        foreach (string name in names)
        {
            fileSystem.AddFile(Path.Join(Root, name), [1], LongAgo);
        }

        int removed = SweepStore(fileSystem).RemoveAbandonedUploadFiles(new SettableTimeProvider(SweepNow));

        Assert.AreEqual(0, removed);
        Assert.IsFalse(fileSystem.Calls.Any(call => call.StartsWith(nameof(IContentFileSystem.DeleteFile), StringComparison.Ordinal)));
    }

    [TestMethod]
    public void RemoveAbandonedUploadFiles_FilesInSubfolders_RemovesThemAndKeepsTheFolders()
    {
        string folder = Path.Join(Root, "bucket");
        string nestedFolder = Path.Join(folder, "blobs");
        string inFolder = Path.Join(folder, UploadFileName(5));
        string inNested = Path.Join(nestedFolder, UploadFileName(6));
        string folderNamedLikeAnUpload = Path.Join(Root, UploadFileName(7));
        UnitTestInMemoryContentFileSystem fileSystem = new UnitTestInMemoryContentFileSystem()
            .AddDirectory(folder)
            .AddDirectory(nestedFolder)
            .AddDirectory(folderNamedLikeAnUpload, LongAgo)
            .AddFile(inFolder, [1], LongAgo)
            .AddFile(inNested, [1], LongAgo);

        int removed = SweepStore(fileSystem).RemoveAbandonedUploadFiles(new SettableTimeProvider(SweepNow));

        Assert.AreEqual(2, removed);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(inFolder));
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(inNested));
        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(nestedFolder));
        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(folderNamedLikeAnUpload));
    }

    [TestMethod]
    public void RemoveAbandonedUploadFiles_TheServiceStateFolderAtTheTop_IsLeftAlone()
    {
        string inState = Path.Join(Root, ".SURL", UploadFileName(8));
        string inDeeperState = Path.Join(Root, "sub", ".surl", UploadFileName(9));
        UnitTestInMemoryContentFileSystem fileSystem = new UnitTestInMemoryContentFileSystem()
            .AddDirectory(Path.Join(Root, ".SURL"))
            .AddDirectory(Path.Join(Root, "sub"))
            .AddDirectory(Path.Join(Root, "sub", ".surl"))
            .AddFile(inState, [1], LongAgo)
            .AddFile(inDeeperState, [1], LongAgo);

        int removed = SweepStore(fileSystem).RemoveAbandonedUploadFiles(new SettableTimeProvider(SweepNow));

        Assert.AreEqual(1, removed);
        Assert.AreEqual(ContentEntryKind.File, fileSystem.GetEntryKind(inState));
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(inDeeperState));
    }

    [TestMethod]
    public void RemoveAbandonedUploadFiles_AFolderThatIsASymbolicLink_IsNotEntered()
    {
        string outside = Path.Join("/", "elsewhere");
        string link = Path.Join(Root, "link");
        string behindLink = Path.Join(outside, UploadFileName(10));
        UnitTestInMemoryContentFileSystem fileSystem = new UnitTestInMemoryContentFileSystem()
            .AddDirectory(outside)
            .AddDirectory(link)
            .AddSymbolicLink(link, outside)
            .AddFile(behindLink, [1], LongAgo);

        int removed = SweepStore(fileSystem).RemoveAbandonedUploadFiles(new SettableTimeProvider(SweepNow));

        Assert.AreEqual(0, removed);
        Assert.AreEqual(ContentEntryKind.File, fileSystem.GetEntryKind(behindLink));
    }

    [TestMethod]
    public void RemoveAbandonedUploadFiles_SharesThatTakeWritesAndAReadOnlyShare_SweepsOnlyTheFormer()
    {
        string shareRoot = Path.Join("/", "shares", "team");
        string readOnlyRoot = Path.Join("/", "shares", "ro");
        UnitTestInMemoryContentFileSystem shareFiles = new UnitTestInMemoryContentFileSystem()
            .AddFile(Path.Join(shareRoot, UploadFileName(11)), [1], LongAgo);
        UnitTestInMemoryContentFileSystem readOnlyFiles = new UnitTestInMemoryContentFileSystem()
            .AddFile(Path.Join(readOnlyRoot, UploadFileName(12)), [1], LongAgo);
        ContentStore store = SweepStore(
            new UnitTestInMemoryContentFileSystem(),
            new ContentShare("team", shareRoot, shareFiles),
            new ContentShare("ro", readOnlyRoot, readOnlyFiles) { IsReadOnly = true });

        int removed = store.RemoveAbandonedUploadFiles(new SettableTimeProvider(SweepNow));

        Assert.AreEqual(1, removed);
        Assert.AreEqual(ContentEntryKind.None, shareFiles.GetEntryKind(Path.Join(shareRoot, UploadFileName(11))));
        Assert.AreEqual(ContentEntryKind.File, readOnlyFiles.GetEntryKind(Path.Join(readOnlyRoot, UploadFileName(12))));
    }

    [TestMethod]
    public void RemoveAbandonedUploadFiles_AFileTheFileSystemRefusesToDelete_IsSkippedAndNotCounted()
    {
        string path = Path.Join(Root, UploadFileName(13));
        UnitTestInMemoryContentFileSystem inner = new UnitTestInMemoryContentFileSystem().AddFile(path, [1], LongAgo);
        UnitTestFaultInjectingContentFileSystem fileSystem = new(inner) { FailDelete = true };

        int removed = SweepStore(fileSystem).RemoveAbandonedUploadFiles(new SettableTimeProvider(SweepNow));

        Assert.AreEqual(0, removed);
        CollectionAssert.AreEqual(new[] { path }, fileSystem.DeletedFiles);
    }

    [TestMethod]
    public void RemoveAbandonedUploadFiles_ARootThatIsNotThere_RemovesNothingAndThrowsNothing()
    {
        SettableTimeProvider clock = new(SweepNow);
        InMemoryContentFileSystem fileSystem = new(clock);
        string missingRoot = Path.Join(InMemoryContentFileSystem.RootPath, "missing");
        ContentStore store = new(missingRoot, fileSystem, new ContentExposureOptions(), [], []);

        Assert.AreEqual(0, store.RemoveAbandonedUploadFiles(clock));
    }

    [TestMethod]
    public void RemoveAbandonedUploadFiles_NoClock_Throws()
    {
        ContentStore store = SweepStore(new UnitTestInMemoryContentFileSystem());

        Assert.ThrowsExactly<ArgumentNullException>(() => store.RemoveAbandonedUploadFiles(null!));
    }

    [TestMethod]
    public void Constructor_WithASweepClock_RemovesAbandonedUploadFilesBeforeServing()
    {
        string old = Path.Join(Root, UploadFileName(14));
        UnitTestInMemoryContentFileSystem fileSystem = new UnitTestInMemoryContentFileSystem().AddFile(old, [1], LongAgo);

        _ = new ContentStore(Root, fileSystem, new ContentExposureOptions(), [], [], sweepClock: new SettableTimeProvider(SweepNow));

        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(old));
    }

    [TestMethod]
    public void Constructor_WithoutASweepClock_RemovesNothing()
    {
        string old = Path.Join(Root, UploadFileName(15));
        UnitTestInMemoryContentFileSystem fileSystem = new UnitTestInMemoryContentFileSystem().AddFile(old, [1], LongAgo);

        _ = SweepStore(fileSystem);

        Assert.AreEqual(ContentEntryKind.File, fileSystem.GetEntryKind(old));
    }
}
