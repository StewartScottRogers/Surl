namespace Surl.Content;

[TestClass]
public sealed class InMemoryContentFileSystemTests
{
    private static readonly DateTimeOffset Started = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Later = Started.AddMinutes(5);

    private static readonly string Root = InMemoryContentFileSystem.RootPath;

    private static readonly string FilePath = Path.Join(Root, "file.bin");

    private static readonly string DriveRoot = Path.GetPathRoot(Root)!;

    private readonly SettableTimeProvider clock = new(Started);

    private InMemoryContentFileSystem NewFileSystem(long maxTotalBytes = InMemoryContentFileSystem.DefaultMaxTotalBytes) =>
        new(clock, maxTotalBytes);

    private static void WriteFile(InMemoryContentFileSystem fileSystem, string path, byte[] contents)
    {
        using Stream stream = fileSystem.CreateFileForAsyncWrite(path);
        stream.Write(contents);
    }

    private static byte[] ReadFile(InMemoryContentFileSystem fileSystem, string path)
    {
        using Stream stream = fileSystem.OpenFileForAsyncRead(path);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    [TestMethod]
    [DataRow(true, @"C:\surl")]
    [DataRow(false, "/surl")]
    public void RootPathFor_EachPlatform_IsTheFullPathAdr0031Names(bool isWindows, string expected) =>
        Assert.AreEqual(expected, InMemoryContentFileSystem.RootPathFor(isWindows));

    [TestMethod]
    public void RootPath_IsAFullPathThatIsNotAFileSystemRoot()
    {
        Assert.IsTrue(Path.IsPathFullyQualified(Root));
        Assert.AreNotEqual(DriveRoot, Root);
    }

    [TestMethod]
    public void Constructor_NullTimeProvider_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new InMemoryContentFileSystem(null!));

    [TestMethod]
    public void Constructor_NegativeBound_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new InMemoryContentFileSystem(TimeProvider.System, -1));

    [TestMethod]
    public void Constructor_Default_HoldsOnlyTheEmptyRootWithTheDefaultBound()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();

        Assert.AreEqual(268_435_456, fileSystem.MaxTotalBytes);
        Assert.AreEqual(0, fileSystem.TotalBytes);
        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(Root));
        Assert.AreEqual(Started, fileSystem.GetLastWriteTimeUtc(Root));
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(DriveRoot));
    }

    [TestMethod]
    public void EnumerateDirectoryEntryNames_EmptyRoot_ListsNothing() =>
        Assert.AreEqual(0, NewFileSystem().EnumerateDirectoryEntryNames(Root).Count());

    [TestMethod]
    public void EnumerateDirectoryEntryNames_FilesAndDirectories_ListsOnlyThoseDirectlyInside()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        fileSystem.CreateDirectory(Path.Join(Root, "sub", "deeper"));
        WriteFile(fileSystem, FilePath, [1]);
        WriteFile(fileSystem, Path.Join(Root, "sub", "inner.txt"), [2]);

        CollectionAssert.AreEquivalent(new[] { "file.bin", "sub" }, fileSystem.EnumerateDirectoryEntryNames(Root).ToArray());
        CollectionAssert.AreEquivalent(new[] { "deeper", "inner.txt" }, fileSystem.EnumerateDirectoryEntryNames(Root + Path.DirectorySeparatorChar + "sub" + Path.DirectorySeparatorChar).ToArray());
    }

    [TestMethod]
    public void EnumerateDirectoryEntryNames_MissingOrAFile_ThrowsDirectoryNotFound()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        WriteFile(fileSystem, FilePath, [1]);

        Assert.ThrowsExactly<DirectoryNotFoundException>(() => fileSystem.EnumerateDirectoryEntryNames(Path.Join(Root, "missing")));
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => fileSystem.EnumerateDirectoryEntryNames(FilePath));
    }

    [TestMethod]
    public void CreateFileForAsyncWrite_WrittenFile_ReadsBackItsBytesLengthAndLastWriteTime()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        Stream stream = fileSystem.CreateFileForAsyncWrite(FilePath);
        stream.Write("0123"u8);
        Assert.AreEqual(ContentEntryKind.File, fileSystem.GetEntryKind(FilePath));
        Assert.AreEqual(0, fileSystem.GetFileLength(FilePath));
        clock.Now = Later;

        stream.Dispose();

        CollectionAssert.AreEqual("0123"u8.ToArray(), ReadFile(fileSystem, FilePath));
        Assert.AreEqual(4, fileSystem.GetFileLength(FilePath));
        Assert.AreEqual(Later, fileSystem.GetLastWriteTimeUtc(FilePath));
        Assert.AreEqual(4, fileSystem.TotalBytes);
    }

    [TestMethod]
    public void CreateFileForAsyncWrite_ExistingFile_IsReplacedAndItsBytesReleased()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        WriteFile(fileSystem, FilePath, new byte[10]);

        WriteFile(fileSystem, FilePath, [7]);

        CollectionAssert.AreEqual(new byte[] { 7 }, ReadFile(fileSystem, FilePath));
        Assert.AreEqual(1, fileSystem.TotalBytes);
    }

    [TestMethod]
    public void CreateFileForAsyncWrite_MissingParent_ThrowsDirectoryNotFound()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();

        Assert.ThrowsExactly<DirectoryNotFoundException>(() => fileSystem.CreateFileForAsyncWrite(Path.Join(Root, "missing", "x")));
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => fileSystem.CreateFileForAsyncWrite(DriveRoot));
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(Root, "missing", "x")));
    }

    [TestMethod]
    public void CreateFileForAsyncWrite_ParentIsAFile_ThrowsDirectoryNotFound()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        WriteFile(fileSystem, FilePath, [1]);

        Assert.ThrowsExactly<DirectoryNotFoundException>(() => fileSystem.CreateFileForAsyncWrite(Path.Join(FilePath, "x")));
    }

    [TestMethod]
    public void CreateFileForAsyncWrite_DirectoryThere_ThrowsUnauthorizedAccessAndKeepsIt()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        string directory = Path.Join(Root, "sub");
        fileSystem.CreateDirectory(directory);

        Assert.ThrowsExactly<UnauthorizedAccessException>(() => fileSystem.CreateFileForAsyncWrite(directory));
        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(directory));
    }

    [TestMethod]
    public void OpenFileForAsyncRead_FileReplacedAfterOpening_KeepsReadingTheOldBytes()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        WriteFile(fileSystem, FilePath, "old"u8.ToArray());
        using Stream reader = fileSystem.OpenFileForAsyncRead(FilePath);

        WriteFile(fileSystem, Path.Join(Root, ".temporary"), "new"u8.ToArray());
        fileSystem.MoveFileReplacing(Path.Join(Root, ".temporary"), FilePath);

        using var copy = new MemoryStream();
        reader.CopyTo(copy);
        CollectionAssert.AreEqual("old"u8.ToArray(), copy.ToArray());
        CollectionAssert.AreEqual("new"u8.ToArray(), ReadFile(fileSystem, FilePath));
        Assert.IsFalse(reader.CanWrite);
    }

    [TestMethod]
    public void FileMembers_MissingPathOrADirectory_ThrowFileNotFound()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        string missing = Path.Join(Root, "missing");

        Assert.ThrowsExactly<FileNotFoundException>(() => fileSystem.GetFileLength(missing));
        Assert.ThrowsExactly<FileNotFoundException>(() => fileSystem.GetFileLength(Root));
        Assert.ThrowsExactly<FileNotFoundException>(() => fileSystem.OpenFileForAsyncRead(missing));
        Assert.ThrowsExactly<FileNotFoundException>(() => fileSystem.GetLastWriteTimeUtc(missing));
    }

    [TestMethod]
    public void ResolveFinalPath_DotSegmentsAndTrailingSeparator_AreNormalised()
    {
        string unnormalised = Root + Path.DirectorySeparatorChar + "a" + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + "b" + Path.DirectorySeparatorChar;

        Assert.AreEqual(Path.Join(Root, "b"), NewFileSystem().ResolveFinalPath(unnormalised));
    }

    [TestMethod]
    public void MoveFileReplacing_ExistingFile_IsReplaced()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        WriteFile(fileSystem, FilePath, "old contents"u8.ToArray());
        string temporary = Path.Join(Root, ".surl-upload-1");
        WriteFile(fileSystem, temporary, "new"u8.ToArray());
        clock.Now = Later;

        fileSystem.MoveFileReplacing(temporary, FilePath);

        CollectionAssert.AreEqual("new"u8.ToArray(), ReadFile(fileSystem, FilePath));
        Assert.AreEqual(Started, fileSystem.GetLastWriteTimeUtc(FilePath));
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(temporary));
        Assert.AreEqual(3, fileSystem.TotalBytes);
    }

    [TestMethod]
    public void MoveFileReplacing_OntoItself_KeepsTheFile()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        WriteFile(fileSystem, FilePath, [1, 2]);

        fileSystem.MoveFileReplacing(FilePath, FilePath);

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, ReadFile(fileSystem, FilePath));
        Assert.AreEqual(2, fileSystem.TotalBytes);
    }

    [TestMethod]
    public void MoveFileReplacing_MissingSource_ThrowsFileNotFound() =>
        Assert.ThrowsExactly<FileNotFoundException>(() => NewFileSystem().MoveFileReplacing(Path.Join(Root, "missing"), FilePath));

    [TestMethod]
    public void MoveFileReplacing_MissingDestinationDirectory_ThrowsAndKeepsTheSource()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        WriteFile(fileSystem, FilePath, [1]);

        Assert.ThrowsExactly<DirectoryNotFoundException>(() => fileSystem.MoveFileReplacing(FilePath, Path.Join(Root, "missing", "x")));
        Assert.AreEqual(ContentEntryKind.File, fileSystem.GetEntryKind(FilePath));
    }

    [TestMethod]
    public void MoveFileReplacing_DirectoryAtTheDestination_ThrowsUnauthorizedAccess()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        WriteFile(fileSystem, FilePath, [1]);
        fileSystem.CreateDirectory(Path.Join(Root, "sub"));

        Assert.ThrowsExactly<UnauthorizedAccessException>(() => fileSystem.MoveFileReplacing(FilePath, Path.Join(Root, "sub")));
        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(Path.Join(Root, "sub")));
    }

    [TestMethod]
    public void DeleteFile_MissingFile_DoesNothing()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();

        fileSystem.DeleteFile(FilePath);

        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(FilePath));
        Assert.AreEqual(0, fileSystem.EnumerateDirectoryEntryNames(Root).Count());
    }

    [TestMethod]
    public void DeleteFile_ExistingFile_RemovesItAndReleasesItsBytes()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        WriteFile(fileSystem, FilePath, new byte[5]);

        fileSystem.DeleteFile(FilePath);

        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(FilePath));
        Assert.AreEqual(0, fileSystem.TotalBytes);
    }

    [TestMethod]
    public void DeleteFile_Directory_IsLeftAlone()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();

        fileSystem.DeleteFile(Root);

        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(Root));
    }

    [TestMethod]
    public void CreateDirectory_MissingParents_AreMadeAndReportedAsDirectories()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        string parent = Path.Join(Root, ".surl");
        string child = Path.Join(parent, "mqtt");
        clock.Now = Later;

        fileSystem.CreateDirectory(child);

        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(parent));
        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(child));
        Assert.AreEqual(Later, fileSystem.GetLastWriteTimeUtc(child));
        CollectionAssert.AreEqual(new[] { "mqtt" }, fileSystem.EnumerateDirectoryEntryNames(parent).ToArray());
    }

    [TestMethod]
    public void CreateDirectory_AlreadyThere_DoesNothing()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        clock.Now = Later;

        fileSystem.CreateDirectory(Root);

        Assert.AreEqual(Started, fileSystem.GetLastWriteTimeUtc(Root));
    }

    [TestMethod]
    public void CreateDirectory_AboveTheRoot_CreatesTheFileSystemRootToo()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();

        fileSystem.CreateDirectory(DriveRoot);

        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(DriveRoot));
    }

    [TestMethod]
    public void CreateDirectory_FileAtOrAboveThePath_ThrowsIOException()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        WriteFile(fileSystem, FilePath, [1]);

        Assert.ThrowsExactly<IOException>(() => fileSystem.CreateDirectory(FilePath));
        Assert.ThrowsExactly<IOException>(() => fileSystem.CreateDirectory(Path.Join(FilePath, "a", "b")));
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(FilePath, "a")));
    }

    [TestMethod]
    public void Write_PastTheBound_ThrowsIOExceptionAndKeepsNothingPastIt()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem(maxTotalBytes: 10);
        WriteFile(fileSystem, Path.Join(Root, "held.bin"), new byte[6]);
        using Stream stream = fileSystem.CreateFileForAsyncWrite(FilePath);
        stream.Write(new byte[4]);

        IOException refused = Assert.ThrowsExactly<IOException>(() => stream.Write(new byte[1]));

        Assert.AreEqual("The in-memory file system is full.", refused.Message);
        Assert.AreEqual(10, fileSystem.TotalBytes);
        stream.Dispose();
        Assert.AreEqual(4, fileSystem.GetFileLength(FilePath));
    }

    [TestMethod]
    public async Task WriteUploadAsync_PastTheBound_ThrowsIOExceptionAndLeavesNoTemporaryFile()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem(maxTotalBytes: 10);
        var store = new ContentStore(Root, fileSystem, new ContentExposureOptions { AllowUploads = true });

        await Assert.ThrowsExactlyAsync<IOException>(
            () => store.WriteUploadAsync(store.MapRequestPath("/upload.bin"), new MemoryStream(new byte[11]), CancellationToken.None));

        Assert.AreEqual(0, fileSystem.EnumerateDirectoryEntryNames(Root).Count());
        Assert.AreEqual(0, fileSystem.TotalBytes);
    }

    [TestMethod]
    public void Write_AfterTheFileWasDeleted_ThrowsIOException()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        using Stream stream = fileSystem.CreateFileForAsyncWrite(FilePath);
        stream.Write(new byte[3]);

        fileSystem.DeleteFile(FilePath);

        Assert.ThrowsExactly<IOException>(() => stream.Write(new byte[1]));
        stream.Dispose();
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(FilePath));
        Assert.AreEqual(0, fileSystem.TotalBytes);
    }

    [TestMethod]
    public void TwoInstances_ShareNothing()
    {
        InMemoryContentFileSystem first = NewFileSystem();
        InMemoryContentFileSystem second = NewFileSystem();

        WriteFile(first, FilePath, [1]);
        first.CreateDirectory(Path.Join(Root, "sub"));

        Assert.AreEqual(ContentEntryKind.None, second.GetEntryKind(FilePath));
        Assert.AreEqual(ContentEntryKind.None, second.GetEntryKind(Path.Join(Root, "sub")));
        Assert.AreEqual(0, second.TotalBytes);
    }

    [TestMethod]
    public async Task CreateFileForAsyncWrite_ParallelTasksWritingDifferentPaths_AllLand()
    {
        const int Writers = 64;
        InMemoryContentFileSystem fileSystem = NewFileSystem();

        await Task.WhenAll(Enumerable.Range(0, Writers).Select(index => Task.Run(async () =>
        {
            await using Stream stream = fileSystem.CreateFileForAsyncWrite(Path.Join(Root, $"file-{index}"));
            for (int chunk = 0; chunk < 16; chunk++)
            {
                await stream.WriteAsync(new byte[] { (byte)index, (byte)chunk });
            }
        })));

        Assert.AreEqual(Writers, fileSystem.EnumerateDirectoryEntryNames(Root).Count());
        Assert.AreEqual(Writers * 32, fileSystem.TotalBytes);
        for (int index = 0; index < Writers; index++)
        {
            byte[] contents = ReadFile(fileSystem, Path.Join(Root, $"file-{index}"));
            Assert.AreEqual(32, contents.Length);
            Assert.IsTrue(contents.Where((_, position) => position % 2 == 0).All(value => value == index));
        }
    }

    [TestMethod]
    public void WriteStream_IsWriteOnlyAndNotSeekable()
    {
        using Stream stream = NewFileSystem().CreateFileForAsyncWrite(FilePath);

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public async Task WriteStream_EveryWriteOverload_KeepsTheBytesInOrder()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        await using (Stream stream = fileSystem.CreateFileForAsyncWrite(FilePath))
        {
            stream.Write([9, 1, 2, 9], 1, 2);
            stream.WriteByte(3);
            await stream.WriteAsync(new byte[] { 9, 4, 9 }, 1, 1, CancellationToken.None);
            await stream.WriteAsync(new ReadOnlyMemory<byte>([5]), CancellationToken.None);
            stream.Flush();
        }

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, ReadFile(fileSystem, FilePath));
    }

    [TestMethod]
    public void WriteStream_InvalidBufferArguments_Throw()
    {
        using Stream stream = NewFileSystem().CreateFileForAsyncWrite(FilePath);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stream.Write(new byte[1], 0, 2));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stream.WriteAsync(new byte[1], 0, 2, CancellationToken.None));
    }

    [TestMethod]
    public async Task WriteStream_CancelledToken_ThrowsAndKeepsNothing()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await using Stream stream = fileSystem.CreateFileForAsyncWrite(FilePath);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await stream.WriteAsync(new byte[1], cancelled.Token));

        Assert.AreEqual(0, fileSystem.TotalBytes);
    }

    [TestMethod]
    public void WriteStream_AfterDispose_RefusesWritesAndDisposesOnce()
    {
        InMemoryContentFileSystem fileSystem = NewFileSystem();
        Stream stream = fileSystem.CreateFileForAsyncWrite(FilePath);
        stream.Write([1]);
        stream.Dispose();
        clock.Now = Later;

        stream.Dispose();

        Assert.IsFalse(stream.CanWrite);
        Assert.ThrowsExactly<ObjectDisposedException>(() => stream.Write([2]));
        Assert.AreEqual(Started, fileSystem.GetLastWriteTimeUtc(FilePath));
    }
}
