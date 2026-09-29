namespace Surl.Content;

[TestClass]
public sealed partial class ContentStoreTests
{
    private static readonly string Root = Path.Join("/", "srv", "www");

    private static readonly DateTimeOffset Modified = new(2024, 5, 6, 7, 8, 9, TimeSpan.Zero);

    [TestMethod]
    [DataRow("/../x", ContentPathRefusal.DotSegment)]
    [DataRow("/a/../../x", ContentPathRefusal.DotSegment)]
    [DataRow("/%2e%2e/x", ContentPathRefusal.DotSegment)]
    [DataRow("/%2E%2E/x", ContentPathRefusal.DotSegment)]
    [DataRow("/.%2e/x", ContentPathRefusal.DotSegment)]
    [DataRow("/./x", ContentPathRefusal.DotSegment)]
    [DataRow("/.", ContentPathRefusal.DotSegment)]
    [DataRow("/..", ContentPathRefusal.DotSegment)]
    [DataRow("/..%2fx", ContentPathRefusal.SeparatorInSegment)]
    [DataRow("/..%2Fx", ContentPathRefusal.SeparatorInSegment)]
    [DataRow("/..%5cx", ContentPathRefusal.SeparatorInSegment)]
    [DataRow("/..\\x", ContentPathRefusal.SeparatorInSegment)]
    [DataRow("/\\\\server\\share\\x", ContentPathRefusal.SeparatorInSegment)]
    [DataRow("/C:%5cx", ContentPathRefusal.SeparatorInSegment)]
    [DataRow("//server/share/x", ContentPathRefusal.EmptySegment)]
    [DataRow("//", ContentPathRefusal.EmptySegment)]
    [DataRow("/a//b", ContentPathRefusal.EmptySegment)]
    [DataRow("/C:/x", ContentPathRefusal.ColonInSegment)]
    [DataRow("/c:", ContentPathRefusal.ColonInSegment)]
    [DataRow("/file.txt::$DATA", ContentPathRefusal.ColonInSegment)]
    [DataRow("/%00x", ContentPathRefusal.ControlCharacter)]
    [DataRow("/x%01", ContentPathRefusal.ControlCharacter)]
    [DataRow("/x%7F", ContentPathRefusal.ControlCharacter)]
    [DataRow("/%zz", ContentPathRefusal.InvalidPercentEncoding)]
    [DataRow("/%", ContentPathRefusal.InvalidPercentEncoding)]
    [DataRow("/%4", ContentPathRefusal.InvalidPercentEncoding)]
    [DataRow("/%4g", ContentPathRefusal.InvalidPercentEncoding)]
    [DataRow("/%g4", ContentPathRefusal.InvalidPercentEncoding)]
    [DataRow("/ok/%zz/x", ContentPathRefusal.InvalidPercentEncoding)]
    [DataRow("/%C3", ContentPathRefusal.InvalidUtf8)]
    [DataRow("/%FF.txt", ContentPathRefusal.InvalidUtf8)]
    [DataRow("/%C0%AE%C0%AE/x", ContentPathRefusal.InvalidUtf8)]
    [DataRow("/x.", ContentPathRefusal.TrailingDotOrSpace)]
    [DataRow("/x%20", ContentPathRefusal.TrailingDotOrSpace)]
    [DataRow("/...", ContentPathRefusal.TrailingDotOrSpace)]
    [DataRow("/CON", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/nul.txt", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/dir/com1", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/LPT9.log", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/CON%20.txt", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/COM%C2%B9", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/CONIN$", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/conout$.txt", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("/CLOCK$", ContentPathRefusal.ReservedDeviceName)]
    [DataRow("", ContentPathRefusal.NotRooted)]
    [DataRow("x", ContentPathRefusal.NotRooted)]
    [DataRow("*", ContentPathRefusal.NotRooted)]
    [DataRow("\\x", ContentPathRefusal.NotRooted)]
    [DataRow("C:/x", ContentPathRefusal.NotRooted)]
    public void MapRequestPath_RefusesEscape_WithoutAskingTheFileSystem(string requestPath, ContentPathRefusal expected)
    {
        var fileSystem = new InMemoryContentFileSystem().AddDirectory(Root);
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        Assert.IsFalse(mapping.IsMapped);
        Assert.AreEqual(expected, mapping.Refusal);
        Assert.IsNull(mapping.Location);
        Assert.AreEqual(ContentEntryKind.None, mapping.EntryKind);
        Assert.IsEmpty(fileSystem.Calls);
    }

    [TestMethod]
    public void MapRequestPath_DotDotThatStaysInsideTheRoot_IsRefused()
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddDirectory(Path.Join(Root, "a"))
            .AddFile(Path.Join(Root, "b"));
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/a/../b");

        Assert.AreEqual(ContentPathRefusal.DotSegment, mapping.Refusal);
        Assert.IsEmpty(fileSystem.Calls);
    }

    [TestMethod]
    public void MapRequestPath_SymbolicLinkPointingOutOfTheRoot_IsRefusedWithoutOpeningIt()
    {
        string outside = Path.Join("/", "etc");
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddSymbolicLink(Path.Join(Root, "out"), outside)
            .AddFile(Path.Join(outside, "passwd"));
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/out/passwd");

        Assert.IsFalse(mapping.IsMapped);
        Assert.AreEqual(ContentPathRefusal.ResolvesOutsideRoot, mapping.Refusal);
        Assert.IsNull(mapping.Location);
        Assert.IsTrue(fileSystem.Calls.TrueForAll(call => call.StartsWith("ResolveFinalPath(", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void MapRequestPath_SymbolicLinkToASiblingWhoseNameStartsWithTheRoot_IsRefused()
    {
        string sibling = Root + "-evil";
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddSymbolicLink(Path.Join(Root, "out"), sibling)
            .AddFile(Path.Join(sibling, "x"));
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/out/x");

        Assert.AreEqual(ContentPathRefusal.ResolvesOutsideRoot, mapping.Refusal);
    }

    [TestMethod]
    public void MapRequestPath_SymbolicLinkToTheRootsParent_IsRefused()
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddSymbolicLink(Path.Join(Root, "up"), Path.Join("/", "srv"));
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/up");

        Assert.AreEqual(ContentPathRefusal.ResolvesOutsideRoot, mapping.Refusal);
    }

    [TestMethod]
    [DataRow("/", new string[0], ContentEntryKind.Directory)]
    [DataRow("/file.txt", new[] { "file.txt" }, ContentEntryKind.File)]
    [DataRow("/dir/file.txt", new[] { "dir", "file.txt" }, ContentEntryKind.File)]
    [DataRow("/dir", new[] { "dir" }, ContentEntryKind.Directory)]
    [DataRow("/dir/", new[] { "dir" }, ContentEntryKind.Directory)]
    [DataRow("/with%20space.txt", new[] { "with space.txt" }, ContentEntryKind.File)]
    [DataRow("/caf%C3%A9.txt", new[] { "caf\u00E9.txt" }, ContentEntryKind.File)]
    [DataRow("/caf%c3%a9.txt", new[] { "caf\u00E9.txt" }, ContentEntryKind.File)]
    [DataRow("/caf\u00E9.txt", new[] { "caf\u00E9.txt" }, ContentEntryKind.File)]
    [DataRow("/.hidden", new[] { ".hidden" }, ContentEntryKind.File)]
    [DataRow("/CONSOLE.txt", new[] { "CONSOLE.txt" }, ContentEntryKind.File)]
    [DataRow("/100%25.txt", new[] { "100%.txt" }, ContentEntryKind.File)]
    [DataRow("/missing.txt", new[] { "missing.txt" }, ContentEntryKind.None)]
    public void MapRequestPath_WellFormedPath_MapsInsideTheRoot(string requestPath, string[] expectedSegments, ContentEntryKind expectedKind)
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(Path.Join(Root, "file.txt"))
            .AddDirectory(Path.Join(Root, "dir"))
            .AddFile(Path.Join(Root, "dir", "file.txt"))
            .AddFile(Path.Join(Root, "with space.txt"))
            .AddFile(Path.Join(Root, "caf\u00E9.txt"))
            .AddFile(Path.Join(Root, ".hidden"))
            .AddFile(Path.Join(Root, "CONSOLE.txt"))
            .AddFile(Path.Join(Root, "100%.txt"));
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(ContentPathRefusal.None, mapping.Refusal);
        Assert.AreEqual(Path.Join([Root, .. expectedSegments]), mapping.Location);
        Assert.AreEqual(expectedKind, mapping.EntryKind);
    }

    [TestMethod]
    public void MapRequestPath_SymbolicLinkWhoseFinalTargetStaysInsideTheRoot_MapsToTheTarget()
    {
        string target = Path.Join(Root, "dir");
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddDirectory(target)
            .AddFile(Path.Join(target, "file.txt"))
            .AddSymbolicLink(Path.Join(Root, "hop"), Path.Join(Root, "link"))
            .AddSymbolicLink(Path.Join(Root, "link"), target);
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/hop/file.txt");

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(Path.Join(target, "file.txt"), mapping.Location);
        Assert.AreEqual(ContentEntryKind.File, mapping.EntryKind);
    }

    [TestMethod]
    public void MapRequestPath_ServedRootIsItselfASymbolicLink_ComparesAgainstItsTarget()
    {
        string realRoot = Path.Join("/", "data", "www");
        var fileSystem = new InMemoryContentFileSystem()
            .AddSymbolicLink(Root, realRoot)
            .AddDirectory(realRoot)
            .AddFile(Path.Join(realRoot, "file.txt"));
        var store = new ContentStore(Root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/file.txt");

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(Path.Join(realRoot, "file.txt"), mapping.Location);
    }

    [TestMethod]
    public void MapRequestPath_ServedRootWithTrailingSeparator_MapsTheRootAndItsChildren()
    {
        string root = Root + Path.DirectorySeparatorChar;
        var fileSystem = new InMemoryContentFileSystem().AddFile(Path.Join(root, "file.txt"));
        var store = new ContentStore(root, fileSystem);

        Assert.AreEqual(root, store.MapRequestPath("/").Location);
        Assert.AreEqual(Path.Join(root, "file.txt"), store.MapRequestPath("/file.txt").Location);
    }

    [TestMethod]
    public void MapRequestPath_ServedRootIsTheFileSystemRoot_MapsItsChildren()
    {
        string root = Path.DirectorySeparatorChar.ToString();
        var fileSystem = new InMemoryContentFileSystem().AddFile(Path.Join(root, "file.txt"));
        var store = new ContentStore(root, fileSystem);

        ContentPathMapping mapping = store.MapRequestPath("/file.txt");

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(ContentEntryKind.File, mapping.EntryKind);
    }

    [TestMethod]
    public void Constructor_KeepsTheServedRoot()
    {
        var store = new ContentStore(Root, new InMemoryContentFileSystem());

        Assert.AreEqual(Root, store.ServedRoot);
    }

    [TestMethod]
    public void Constructor_RejectsMissingArguments()
    {
        var fileSystem = new InMemoryContentFileSystem();

        Assert.ThrowsExactly<ArgumentNullException>(() => new ContentStore(null!, fileSystem));
        Assert.ThrowsExactly<ArgumentException>(() => new ContentStore(string.Empty, fileSystem));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ContentStore(Root, null!));
    }

    [TestMethod]
    public void MapRequestPath_RejectsNull()
    {
        var store = new ContentStore(Root, new InMemoryContentFileSystem());

        Assert.ThrowsExactly<ArgumentNullException>(() => store.MapRequestPath(null!));
    }

    [TestMethod]
    [DataRow("/file.txt", ContentEntryKind.File)]
    [DataRow("/dir", ContentEntryKind.Directory)]
    [DataRow("/missing.txt", ContentEntryKind.None)]
    public void GetEntryKind_MappedLocation_ReportsWhatIsThereNow(string requestPath, ContentEntryKind expected)
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddDirectory(Path.Join(Root, "dir"))
            .AddFile(Path.Join(Root, "file.txt"));
        var store = new ContentStore(Root, fileSystem);
        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        ContentEntryKind kind = store.GetEntryKind(mapping);

        Assert.AreEqual(expected, kind);
    }

    [TestMethod]
    public void GetFileStatus_File_ReportsLengthAndUtcModificationTime()
    {
        var written = new DateTimeOffset(2026, 6, 24, 10, 30, 15, TimeSpan.FromHours(2));
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(Path.Join(Root, "file.txt"), new byte[1234], written);
        var store = new ContentStore(Root, fileSystem);

        ContentFileStatus? status = store.GetFileStatus(store.MapRequestPath("/file.txt"));

        Assert.IsNotNull(status);
        Assert.AreEqual(1234L, status.Length);
        Assert.AreEqual(written, status.LastModifiedUtc);
        Assert.AreEqual(TimeSpan.Zero, status.LastModifiedUtc.Offset);
    }

    [TestMethod]
    [DataRow("/dir")]
    [DataRow("/missing.txt")]
    public void GetFileStatus_DirectoryOrNothing_IsNull(string requestPath)
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddDirectory(Path.Join(Root, "dir"));
        var store = new ContentStore(Root, fileSystem);

        ContentFileStatus? status = store.GetFileStatus(store.MapRequestPath(requestPath));

        Assert.IsNull(status);
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_WholeFile_CopiesEveryByte()
    {
        byte[] contents = "Hello, Surl!"u8.ToArray();
        ContentStore store = StoreWithFile(contents);
        ContentPathMapping mapping = store.MapRequestPath("/file.txt");
        using var destination = new MemoryStream();

        long copied = await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(contents.Length), destination, CancellationToken.None);

        Assert.AreEqual(contents.LongLength, copied);
        CollectionAssert.AreEqual(contents, destination.ToArray());
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_InclusiveRange_CopiesFirstThroughLast()
    {
        ContentStore store = StoreWithFile("0123456789"u8.ToArray());
        ContentPathMapping mapping = store.MapRequestPath("/file.txt");
        using var destination = new MemoryStream();

        long copied = await store.CopyFileBytesAsync(mapping, ContentByteRange.Select(10, 2, 5), destination, CancellationToken.None);

        Assert.AreEqual(4L, copied);
        CollectionAssert.AreEqual("2345"u8.ToArray(), destination.ToArray());
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_LastOffsetPastTheEnd_CopiesToTheEnd()
    {
        ContentStore store = StoreWithFile("0123456789"u8.ToArray());
        ContentPathMapping mapping = store.MapRequestPath("/file.txt");
        using var destination = new MemoryStream();

        long copied = await store.CopyFileBytesAsync(mapping, ContentByteRange.Select(10, 7, 500), destination, CancellationToken.None);

        Assert.AreEqual(3L, copied);
        CollectionAssert.AreEqual("789"u8.ToArray(), destination.ToArray());
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_ZeroLengthFile_CopiesZeroBytes()
    {
        ContentStore store = StoreWithFile([]);
        ContentPathMapping mapping = store.MapRequestPath("/file.txt");
        using var destination = new MemoryStream();

        long copied = await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(0), destination, CancellationToken.None);

        Assert.AreEqual(0L, copied);
        Assert.AreEqual(0L, destination.Length);
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_FileLargerThanOneBuffer_CopiesEveryByte()
    {
        byte[] contents = new byte[200_000];
        new Random(9).NextBytes(contents);
        ContentStore store = StoreWithFile(contents);
        ContentPathMapping mapping = store.MapRequestPath("/file.txt");
        using var destination = new MemoryStream();

        long copied = await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(contents.Length), destination, CancellationToken.None);

        Assert.AreEqual(contents.LongLength, copied);
        CollectionAssert.AreEqual(contents, destination.ToArray());
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_FileShrankSinceTheRangeWasSelected_StopsAtItsEnd()
    {
        ContentStore store = StoreWithFile("0123"u8.ToArray());
        ContentPathMapping mapping = store.MapRequestPath("/file.txt");
        using var destination = new MemoryStream();

        long copied = await store.CopyFileBytesAsync(mapping, ContentByteRange.Select(10, 2, 9), destination, CancellationToken.None);

        Assert.AreEqual(2L, copied);
        CollectionAssert.AreEqual("23"u8.ToArray(), destination.ToArray());
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_CancelledBeforeTheRead_ThrowsWithoutOpeningTheFile()
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(Path.Join(Root, "file.txt"), "abc"u8.ToArray(), DateTimeOffset.UnixEpoch);
        var store = new ContentStore(Root, fileSystem);
        ContentPathMapping mapping = store.MapRequestPath("/file.txt");
        using var destination = new MemoryStream();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(3), destination, new CancellationToken(canceled: true)));

        Assert.IsFalse(fileSystem.Calls.Exists(call => call.StartsWith("OpenFileForAsyncRead(", StringComparison.Ordinal)));
        Assert.AreEqual(0L, destination.Length);
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_CancelledDuringTheRead_ThrowsBeforeTheNextRead()
    {
        byte[] contents = new byte[200_000];
        ContentStore store = StoreWithFile(contents);
        ContentPathMapping mapping = store.MapRequestPath("/file.txt");
        using var cancellation = new CancellationTokenSource();
        using var destination = new CancellingOnWriteStream(cancellation);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(contents.Length), destination, cancellation.Token));

        Assert.IsGreaterThan(0L, destination.Length);
        Assert.IsLessThan(contents.LongLength, destination.Length);
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_UnsatisfiableRange_IsRejected()
    {
        ContentStore store = StoreWithFile("abc"u8.ToArray());
        ContentPathMapping mapping = store.MapRequestPath("/file.txt");

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => store.CopyFileBytesAsync(mapping, ContentByteRange.Select(3, 3, 5), Stream.Null, CancellationToken.None));
    }

    [TestMethod]
    public async Task CopyFileBytesAsync_MissingArguments_AreRejected()
    {
        ContentStore store = StoreWithFile("abc"u8.ToArray());
        ContentPathMapping mapping = store.MapRequestPath("/file.txt");
        ContentByteRange range = ContentByteRange.WholeFile(3);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.CopyFileBytesAsync(null!, range, Stream.Null, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.CopyFileBytesAsync(mapping, null!, Stream.Null, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.CopyFileBytesAsync(mapping, range, null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task RefusedMapping_IsRejectedByEveryRead()
    {
        ContentStore store = StoreWithFile("abc"u8.ToArray());
        ContentPathMapping refused = store.MapRequestPath("/../file.txt");

        Assert.ThrowsExactly<ArgumentException>(() => store.GetEntryKind(refused));
        Assert.ThrowsExactly<ArgumentException>(() => store.GetFileStatus(refused));
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => store.CopyFileBytesAsync(refused, ContentByteRange.WholeFile(3), Stream.Null, CancellationToken.None));
    }

    [TestMethod]
    public void ListDirectory_EmptyDirectory_ListsNoEntries()
    {
        var store = new ContentStore(Root, new InMemoryContentFileSystem().AddDirectory(Root));

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/"), CancellationToken.None);

        Assert.IsTrue(listing.IsListed);
        Assert.AreEqual(ContentEntryKind.Directory, listing.LocationKind);
        Assert.IsEmpty(listing.Entries);
    }

    [TestMethod]
    public void ListDirectory_FilesAndSubdirectories_ListsNameKindLengthAndUtcModificationTime()
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(Path.Join(Root, "readme.txt"), "hello"u8.ToArray(), new DateTimeOffset(2024, 5, 6, 9, 8, 9, TimeSpan.FromHours(2)))
            .AddDirectory(Path.Join(Root, "docs"), Modified.AddDays(1))
            .AddFile(Path.Join(Root, "docs", "nested.bin"), [1, 2, 3], Modified)
            .AddFile(Path.Join(Root, "empty.bin"));
        var store = new ContentStore(Root, fileSystem);

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/"), CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                new ContentDirectoryEntry("docs", ContentEntryKind.Directory, null, Modified.AddDays(1)),
                new ContentDirectoryEntry("empty.bin", ContentEntryKind.File, 0, DateTimeOffset.UnixEpoch),
                new ContentDirectoryEntry("readme.txt", ContentEntryKind.File, 5, Modified),
            },
            listing.Entries.ToArray());
        Assert.IsTrue(listing.Entries.All(entry => entry.LastModifiedUtc.Offset == TimeSpan.Zero));
    }

    [TestMethod]
    public void ListDirectory_Subdirectory_ListsOnlyItsOwnEntries()
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(Path.Join(Root, "top.txt"))
            .AddDirectory(Path.Join(Root, "docs"))
            .AddFile(Path.Join(Root, "docs", "nested.bin"), [1, 2, 3], Modified)
            .AddDirectory(Path.Join(Root, "docs", "deeper"))
            .AddFile(Path.Join(Root, "docs", "deeper", "deepest.txt"));
        var store = new ContentStore(Root, fileSystem);

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/docs/"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "deeper", "nested.bin" }, listing.Entries.Select(entry => entry.Name).ToArray());
    }

    [TestMethod]
    public void ListDirectory_MixedCaseAndNonAsciiNames_AreInOrdinalOrder()
    {
        string[] names = ["b", "\u00e9", "B", "a", "\u00c4", "Z", "e", "\u65e5\u672c", "10", "9"];
        var fileSystem = new InMemoryContentFileSystem().AddDirectory(Root);
        foreach (string name in names)
        {
            fileSystem.AddFile(Path.Join(Root, name));
        }

        var store = new ContentStore(Root, fileSystem);

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/"), CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "10", "9", "B", "Z", "a", "b", "e", "\u00c4", "\u00e9", "\u65e5\u672c" },
            listing.Entries.Select(entry => entry.Name).ToArray());
    }

    [TestMethod]
    public void ListDirectory_SymbolicLinkOutOfTheRoot_IsLeftOut()
    {
        string outside = Path.Join("/", "etc");
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(Path.Join(Root, "kept.txt"))
            .AddDirectory(outside)
            .AddFile(Path.Join(outside, "passwd"))
            .AddSymbolicLink(Path.Join(Root, "escape"), outside)
            .AddSymbolicLink(Path.Join(Root, "secret"), Path.Join(outside, "passwd"))
            .AddSymbolicLink(Path.Join(Root, "sibling"), Root + "-private")
            .AddDirectory(Root + "-private");
        var store = new ContentStore(Root, fileSystem);

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "kept.txt" }, listing.Entries.Select(entry => entry.Name).ToArray());
        Assert.IsFalse(fileSystem.Calls.Exists(call => call.StartsWith("OpenFileForAsyncRead(", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ListDirectory_SymbolicLinkInsideTheRoot_IsListedUnderItsOwnNameWithItsTargetsStatus()
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddDirectory(Path.Join(Root, "docs"), Modified)
            .AddFile(Path.Join(Root, "docs", "file.bin"), [1, 2, 3, 4], Modified)
            .AddSymbolicLink(Path.Join(Root, "alias.bin"), Path.Join(Root, "docs", "file.bin"))
            .AddSymbolicLink(Path.Join(Root, "alias-docs"), Path.Join(Root, "docs"));
        var store = new ContentStore(Root, fileSystem);

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/"), CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                new ContentDirectoryEntry("alias-docs", ContentEntryKind.Directory, null, Modified),
                new ContentDirectoryEntry("alias.bin", ContentEntryKind.File, 4, Modified),
                new ContentDirectoryEntry("docs", ContentEntryKind.Directory, null, Modified),
            },
            listing.Entries.ToArray());
    }

    [TestMethod]
    public void ListDirectory_DanglingSymbolicLink_IsLeftOut()
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddSymbolicLink(Path.Join(Root, "dangling"), Path.Join(Root, "gone.txt"));
        var store = new ContentStore(Root, fileSystem);

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/"), CancellationToken.None);

        Assert.IsTrue(listing.IsListed);
        Assert.IsEmpty(listing.Entries);
    }

    [TestMethod]
    [DataRow("CON")]
    [DataRow("nul.txt")]
    [DataRow("a:b")]
    [DataRow("trailing.")]
    [DataRow("trailing ")]
    [DataRow("back\\slash")]
    [DataRow("control\u0001")]
    public void ListDirectory_NameARequestPathCannotAskFor_IsLeftOut(string name)
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(Path.Join(Root, "kept.txt"))
            .AddFile(Root + Path.DirectorySeparatorChar + name);
        var store = new ContentStore(Root, fileSystem);

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "kept.txt" }, listing.Entries.Select(entry => entry.Name).ToArray());
    }

    [TestMethod]
    public void ListDirectory_DotFiles_AreListed()
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(Path.Join(Root, ".hidden"))
            .AddDirectory(Path.Join(Root, ".git"));
        var store = new ContentStore(Root, fileSystem);

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { ".git", ".hidden" }, listing.Entries.Select(entry => entry.Name).ToArray());
    }

    [TestMethod]
    public void ListDirectory_File_SaysItIsAFileWithoutReadingADirectory()
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(Path.Join(Root, "file.txt"));
        var store = new ContentStore(Root, fileSystem);

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/file.txt"), CancellationToken.None);

        Assert.IsFalse(listing.IsListed);
        Assert.AreEqual(ContentEntryKind.File, listing.LocationKind);
        Assert.IsEmpty(listing.Entries);
        Assert.IsFalse(fileSystem.Calls.Exists(call => call.StartsWith("EnumerateDirectoryEntryNames(", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ListDirectory_MissingPath_SaysNothingIsThere()
    {
        var store = new ContentStore(Root, new InMemoryContentFileSystem().AddDirectory(Root));

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/missing/"), CancellationToken.None);

        Assert.IsFalse(listing.IsListed);
        Assert.AreEqual(ContentEntryKind.None, listing.LocationKind);
        Assert.IsEmpty(listing.Entries);
    }

    [TestMethod]
    public void ListDirectory_CancelledBeforeTheRead_ThrowsWithoutReadingTheDirectory()
    {
        var fileSystem = new InMemoryContentFileSystem().AddDirectory(Root).AddFile(Path.Join(Root, "file.txt"));
        var store = new ContentStore(Root, fileSystem);
        ContentPathMapping mapping = store.MapRequestPath("/");

        Assert.ThrowsExactly<OperationCanceledException>(
            () => store.ListDirectory(mapping, new CancellationToken(canceled: true)));

        Assert.IsFalse(fileSystem.Calls.Exists(call => call.StartsWith("EnumerateDirectoryEntryNames(", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ListDirectory_CancelledDuringTheRead_ThrowsBeforeTheNextEntry()
    {
        var fileSystem = new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(Path.Join(Root, "first.txt"))
            .AddFile(Path.Join(Root, "second.txt"));
        var store = new ContentStore(Root, fileSystem);
        ContentPathMapping mapping = store.MapRequestPath("/");
        using var cancellation = new CancellationTokenSource();
        fileSystem.AfterEachEnumeratedName = cancellation.Cancel;

        Assert.ThrowsExactly<OperationCanceledException>(() => store.ListDirectory(mapping, cancellation.Token));

        Assert.AreEqual(1, fileSystem.Calls.Count(call => call.StartsWith("GetFileLength(", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ListDirectory_RefusedOrMissingMapping_IsRejected()
    {
        var store = new ContentStore(Root, new InMemoryContentFileSystem().AddDirectory(Root));

        Assert.ThrowsExactly<ArgumentException>(() => store.ListDirectory(store.MapRequestPath("/../"), CancellationToken.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.ListDirectory(null!, CancellationToken.None));
    }

    private static ContentStore StoreWithFile(byte[] contents) =>
        new(Root, new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddFile(Path.Join(Root, "file.txt"), contents, DateTimeOffset.UnixEpoch));
}
