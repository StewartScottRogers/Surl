namespace Surl.Content;

public sealed partial class ContentStoreTests
{
    private static readonly ContentExposureOptions EverythingOn = new()
    {
        AllowUploads = true,
        ListDirectories = true,
        FollowSymbolicLinks = true,
        ServeDotFiles = true,
    };

    [TestMethod]
    public void Constructor_WithoutExposureOptions_ServesEverythingInsideTheRoot()
    {
        var store = new ContentStore(Root, new InMemoryContentFileSystem());

        Assert.AreSame(ContentExposureOptions.ServeEverythingInsideTheRoot, store.ExposureOptions);
    }

    [TestMethod]
    public void Constructor_KeepsTheExposureOptions()
    {
        var options = new ContentExposureOptions();

        var store = new ContentStore(Root, new InMemoryContentFileSystem(), options);

        Assert.AreSame(options, store.ExposureOptions);
    }

    [TestMethod]
    public void Constructor_RejectsMissingExposureOptions()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ContentStore(Root, new InMemoryContentFileSystem(), null!));
    }

    [TestMethod]
    [DataRow("/.git/config", new[] { ".git", "config" })]
    [DataRow("/.hidden", new[] { ".hidden" })]
    [DataRow("/dir/.env", new[] { "dir", ".env" })]
    [DataRow("/.git/", new[] { ".git" })]
    [DataRow("/link/file.txt", new[] { "link", "file.txt" })]
    [DataRow("/link", new[] { "link" })]
    [DataRow("/missing.txt", new[] { "missing.txt" })]
    public async Task MapRequestPath_DefaultOptions_DotFileOrLinkIsAnsweredAsAMissingPath(string requestPath, string[] expectedSegments)
    {
        ContentStore store = new(Root, ExposureFileSystem(), new ContentExposureOptions());

        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(ContentPathRefusal.None, mapping.Refusal);
        Assert.AreEqual(Path.Join([Root, .. expectedSegments]), mapping.Location);
        Assert.AreEqual(ContentEntryKind.None, mapping.EntryKind);
        Assert.AreEqual(ContentEntryKind.None, store.GetEntryKind(mapping));
        Assert.IsNull(store.GetFileStatus(mapping));
        Assert.AreEqual(ContentEntryKind.None, store.ListDirectory(mapping, CancellationToken.None).LocationKind);
        await Assert.ThrowsAsync<IOException>(
            () => store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(1), Stream.Null, CancellationToken.None));
    }

    [TestMethod]
    public void MapRequestPath_DefaultOptions_ServesAPlainFile()
    {
        ContentStore store = new(Root, ExposureFileSystem(), new ContentExposureOptions());

        ContentPathMapping mapping = store.MapRequestPath("/dir/file.txt");

        Assert.AreEqual(ContentEntryKind.File, mapping.EntryKind);
        Assert.AreEqual(ContentEntryKind.File, store.GetEntryKind(mapping));
    }

    [TestMethod]
    public void MapRequestPath_ServedRootIsASymbolicLinkAndLinksAreOff_StillServesItsFiles()
    {
        string realRoot = Path.Join("/", "data", "www");
        var fileSystem = new InMemoryContentFileSystem()
            .AddSymbolicLink(Root, realRoot)
            .AddDirectory(realRoot)
            .AddFile(Path.Join(realRoot, "file.txt"));
        var store = new ContentStore(Root, fileSystem, new ContentExposureOptions());

        ContentPathMapping mapping = store.MapRequestPath("/file.txt");

        Assert.AreEqual(Path.Join(realRoot, "file.txt"), mapping.Location);
        Assert.AreEqual(ContentEntryKind.File, mapping.EntryKind);
    }

    [TestMethod]
    [DataRow("/.git/config", ContentEntryKind.File)]
    [DataRow("/.hidden", ContentEntryKind.File)]
    [DataRow("/dir/.env", ContentEntryKind.File)]
    [DataRow("/link/file.txt", ContentEntryKind.File)]
    public void MapRequestPath_DotFileAndLinkOptionsOn_ServesThePath(string requestPath, ContentEntryKind expectedKind)
    {
        ContentStore store = new(Root, ExposureFileSystem(), EverythingOn);

        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        Assert.AreEqual(expectedKind, mapping.EntryKind);
        Assert.AreEqual(expectedKind, store.GetEntryKind(mapping));
        Assert.IsNotNull(store.GetFileStatus(mapping));
    }

    [TestMethod]
    public void MapRequestPath_EveryOptionOn_LinkOutOfTheRootIsStillRefused()
    {
        ContentStore store = new(Root, ExposureFileSystem(), EverythingOn);

        ContentPathMapping mapping = store.MapRequestPath("/escape/passwd");

        Assert.IsFalse(mapping.IsMapped);
        Assert.AreEqual(ContentPathRefusal.ResolvesOutsideRoot, mapping.Refusal);
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/dir")]
    [DataRow("/dir/file.txt")]
    [DataRow("/missing.txt")]
    public void ListDirectory_DefaultOptions_IsAnsweredAsAMissingPathWithoutReadingADirectory(string requestPath)
    {
        InMemoryContentFileSystem fileSystem = ExposureFileSystem();
        ContentStore store = new(Root, fileSystem, new ContentExposureOptions());
        ContentPathMapping mapping = store.MapRequestPath(requestPath);
        fileSystem.Calls.Clear();

        ContentDirectoryListing listing = store.ListDirectory(mapping, CancellationToken.None);

        Assert.IsFalse(listing.IsListed);
        Assert.AreEqual(ContentEntryKind.None, listing.LocationKind);
        Assert.IsEmpty(listing.Entries);
        Assert.IsEmpty(fileSystem.Calls);
    }

    [TestMethod]
    public void ListDirectory_ListingsOnOnly_LeavesOutDotFilesAndLinks()
    {
        ContentStore store = new(Root, ExposureFileSystem(), new ContentExposureOptions { ListDirectories = true });

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "dir" }, listing.Entries.Select(entry => entry.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "file.txt" },
            store.ListDirectory(store.MapRequestPath("/dir"), CancellationToken.None).Entries.Select(entry => entry.Name).ToArray());
    }

    [TestMethod]
    public void ListDirectory_EveryOptionOn_ListsDotFilesAndLinksInsideTheRootButNotOneOut()
    {
        ContentStore store = new(Root, ExposureFileSystem(), EverythingOn);

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { ".git", ".hidden", "dir", "link" }, listing.Entries.Select(entry => entry.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { ".env", "file.txt" },
            store.ListDirectory(store.MapRequestPath("/dir/"), CancellationToken.None).Entries.Select(entry => entry.Name).ToArray());
    }

    [TestMethod]
    public void ListDirectory_ListingsOnAndDotFilesHidden_DotDirectoryIsAnsweredAsAMissingPath()
    {
        ContentStore store = new(Root, ExposureFileSystem(), new ContentExposureOptions { ListDirectories = true });

        ContentDirectoryListing listing = store.ListDirectory(store.MapRequestPath("/.git"), CancellationToken.None);

        Assert.IsFalse(listing.IsListed);
        Assert.AreEqual(ContentEntryKind.None, listing.LocationKind);
    }

    /// <summary>
    /// The served root holding a dot-directory, a dot-file, a directory with a dot-file and a
    /// plain file, a symbolic link to that directory, and a link out of the root.
    /// </summary>
    private static InMemoryContentFileSystem ExposureFileSystem()
    {
        string outside = Path.Join("/", "etc");
        return new InMemoryContentFileSystem()
            .AddDirectory(Root)
            .AddDirectory(Path.Join(Root, ".git"))
            .AddFile(Path.Join(Root, ".git", "config"))
            .AddFile(Path.Join(Root, ".hidden"))
            .AddDirectory(Path.Join(Root, "dir"))
            .AddFile(Path.Join(Root, "dir", ".env"))
            .AddFile(Path.Join(Root, "dir", "file.txt"))
            .AddSymbolicLink(Path.Join(Root, "link"), Path.Join(Root, "dir"))
            .AddDirectory(outside)
            .AddFile(Path.Join(outside, "passwd"))
            .AddSymbolicLink(Path.Join(Root, "escape"), outside);
    }
}
