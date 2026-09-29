namespace Surl.Content;

public sealed partial class ContentStoreTests
{
    private static readonly string ServiceStateFolder = Path.Join(Root, ".surl");

    [TestMethod]
    [DataRow("/.surl")]
    [DataRow("/.surl/")]
    [DataRow("/.surl/lock")]
    [DataRow("/.surl/mqtt/x")]
    [DataRow("/.SURL")]
    [DataRow("/.SURL/lock")]
    [DataRow("/.Surl/mqtt/x")]
    [DataRow("/link")]
    [DataRow("/linked-folder/lock")]
    public async Task MapRequestPath_EveryOptionOn_ServiceStateFolderIsAnsweredAsAMissingPath(string requestPath)
    {
        ContentStore store = new(Root, ServiceStateFileSystem(), EverythingOn);

        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(ContentEntryKind.None, mapping.EntryKind);
        Assert.AreEqual(ContentEntryKind.None, store.GetEntryKind(mapping));
        Assert.IsNull(store.GetFileStatus(mapping));
        Assert.AreEqual(ContentEntryKind.None, store.ListDirectory(mapping, CancellationToken.None).LocationKind);
        await Assert.ThrowsAsync<IOException>(
            () => store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(1), Stream.Null, CancellationToken.None));
    }

    [TestMethod]
    [DataRow("/.surl")]
    [DataRow("/.surl/x")]
    [DataRow("/.surl/lock")]
    [DataRow("/.SURL/x")]
    [DataRow("/.SURL/lock")]
    [DataRow("/linked-folder/x")]
    public async Task WriteUploadAsync_EveryOptionOn_ServiceStateFolderIsNotPermittedAndNothingIsWritten(string requestPath)
    {
        UnitTestInMemoryContentFileSystem fileSystem = ServiceStateFileSystem();
        ContentStore store = new(Root, fileSystem, EverythingOn);

        ContentUploadResult result = await store.WriteUploadAsync(
            store.MapRequestPath(requestPath), new MemoryStream([1, 2, 3]), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.NotPermitted, result);
        Assert.IsFalse(fileSystem.Calls.Any(call => call.StartsWith(nameof(IContentFileSystem.CreateFileForAsyncWrite), StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ListDirectory_EveryOptionOn_RootListingLeavesOutTheServiceStateFolderAndLinksIntoIt()
    {
        ContentStore store = new(Root, ServiceStateFileSystem(), EverythingOn);

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/"), CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { ".hidden", ".surlx", "sub" },
            listing.Entries.Select(entry => entry.Name).ToArray());
    }

    [TestMethod]
    [DataRow("/.hidden", ContentEntryKind.File)]
    [DataRow("/.surlx", ContentEntryKind.File)]
    [DataRow("/sub/.surl", ContentEntryKind.Directory)]
    [DataRow("/sub/.surl/x", ContentEntryKind.File)]
    public void MapRequestPath_DotFilesOn_OtherDotFilesAndALowerServiceStateFolderAreServed(string requestPath, ContentEntryKind expectedKind)
    {
        ContentStore store = new(Root, ServiceStateFileSystem(), new ContentExposureOptions { ServeDotFiles = true });

        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        Assert.AreEqual(expectedKind, mapping.EntryKind);
        Assert.AreEqual(expectedKind, store.GetEntryKind(mapping));
    }

    [TestMethod]
    public void ListDirectory_EveryOptionOn_ALowerServiceStateFolderIsListed()
    {
        ContentStore store = new(Root, ServiceStateFileSystem(), EverythingOn);

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/sub/"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { ".surl" }, listing.Entries.Select(entry => entry.Name).ToArray());
    }

    [TestMethod]
    public void MapRequestPath_EveryOptionOn_ServedRootItselfIsStillServed()
    {
        ContentStore store = new(Root, ServiceStateFileSystem(), EverythingOn);

        Assert.AreEqual(ContentEntryKind.Directory, store.MapRequestPath("/").EntryKind);
    }

    [TestMethod]
    public void MapRequestPath_ServedRootEndsInASeparator_ServiceStateFolderIsAnsweredAsAMissingPath()
    {
        string root = Path.Join("/", "srv", "www") + Path.DirectorySeparatorChar;
        var fileSystem = new UnitTestInMemoryContentFileSystem()
            .AddDirectory(root)
            .AddFile(root + Path.Join(".surl", "lock"));
        ContentStore store = new(root, fileSystem, EverythingOn);

        Assert.AreEqual(ContentEntryKind.None, store.MapRequestPath("/.surl/lock").EntryKind);
    }

    // The fake is case-sensitive, as Linux is: /.SURL is answered as missing because the
    // store refuses it, not because nothing is there.
    private static UnitTestInMemoryContentFileSystem ServiceStateFileSystem() =>
        new UnitTestInMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddDirectory(ServiceStateFolder)
            .AddFile(Path.Join(ServiceStateFolder, "lock"), [1], Modified)
            .AddDirectory(Path.Join(ServiceStateFolder, "mqtt"))
            .AddFile(Path.Join(ServiceStateFolder, "mqtt", "x"), [2], Modified)
            .AddDirectory(Path.Join(Root, ".SURL"))
            .AddFile(Path.Join(Root, ".SURL", "lock"), [3], Modified)
            .AddDirectory(Path.Join(Root, ".Surl"))
            .AddDirectory(Path.Join(Root, ".Surl", "mqtt"))
            .AddFile(Path.Join(Root, ".Surl", "mqtt", "x"), [4], Modified)
            .AddFile(Path.Join(Root, ".hidden"), [5], Modified)
            .AddFile(Path.Join(Root, ".surlx"), [6], Modified)
            .AddDirectory(Path.Join(Root, "sub"))
            .AddDirectory(Path.Join(Root, "sub", ".surl"))
            .AddFile(Path.Join(Root, "sub", ".surl", "x"), [7], Modified)
            .AddSymbolicLink(Path.Join(Root, "link"), Path.Join(ServiceStateFolder, "lock"))
            .AddSymbolicLink(Path.Join(Root, "linked-folder"), ServiceStateFolder);
}
