using System.Text;

namespace Surl.Content;

public sealed partial class ContentStoreTests
{
    private static readonly string MemoryRoot = InMemoryContentFileSystem.RootPath;

    private static readonly ContentExposureOptions UploadsAndListingsOn = new()
    {
        AllowUploads = true,
        ListDirectories = true,
        ServeDotFiles = true,
    };

    private static readonly ContentUploadOpening CreateOrTruncate = new(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false);

    [TestMethod]
    public async Task MapRequestPath_PathUnderAShareName_MapsIntoTheSharesPath()
    {
        InMemoryContentFileSystem shareFiles = MemoryWith(("doc.txt", "share bytes"));
        ContentStore store = StoreWith(MemoryWith(("doc.txt", "root bytes")), new ContentShare("team", MemoryRoot, shareFiles));

        ContentPathMapping mapping = store.MapRequestPathForScheme("/team/doc.txt", "ftp");

        Assert.AreEqual(Path.Join(MemoryRoot, "doc.txt"), mapping.Location);
        Assert.AreEqual(ContentEntryKind.File, mapping.EntryKind);
        Assert.AreEqual("share bytes", await ReadAsync(store, mapping));
        Assert.AreEqual("root bytes", await ReadAsync(store, store.MapRequestPathForScheme("/doc.txt", "ftp")));
    }

    [TestMethod]
    [DataRow("/team")]
    [DataRow("/team/")]
    public void MapRequestPath_TheShareNameAlone_IsTheSharesDirectory(string requestPath)
    {
        ContentStore store = StoreWith(MemoryWith(), new ContentShare("team", MemoryRoot, MemoryWith()));

        ContentPathMapping mapping = store.MapRequestPath(requestPath);

        Assert.AreEqual(ContentEntryKind.Directory, mapping.EntryKind);
        Assert.IsTrue(store.ListDirectory(mapping, CancellationToken.None).IsListed);
    }

    [TestMethod]
    public async Task WriteUploadAsync_InAWritableShare_LandsInTheShareOnly()
    {
        InMemoryContentFileSystem rootFiles = MemoryWith();
        InMemoryContentFileSystem shareFiles = MemoryWith();
        ContentStore store = StoreWith(rootFiles, new ContentShare("team", MemoryRoot, shareFiles));

        ContentUploadResult result = await store.WriteUploadAsync(store.MapRequestPathForScheme("/team/up.txt", "ftp"), Bytes("new"), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, result);
        Assert.AreEqual(ContentEntryKind.File, shareFiles.GetEntryKind(Path.Join(MemoryRoot, "up.txt")));
        Assert.AreEqual(ContentEntryKind.None, rootFiles.GetEntryKind(Path.Join(MemoryRoot, "up.txt")));
    }

    [TestMethod]
    public async Task EveryWrite_InAReadOnlyShare_IsNotPermittedWhateverAllowUploadsSays()
    {
        InMemoryContentFileSystem shareFiles = MemoryWith(("doc.txt", "kept"), ("empty/", ""));
        ContentStore store = StoreWith(MemoryWith(), new ContentShare("team", MemoryRoot, shareFiles) { IsReadOnly = true });
        ContentPathMapping file = store.MapRequestPathForScheme("/team/doc.txt", "ftp");
        ContentPathMapping empty = store.MapRequestPathForScheme("/team/empty", "ftp");

        Assert.AreEqual(ContentUploadResult.NotPermitted, await store.WriteUploadAsync(file, Bytes("x"), CancellationToken.None));
        Assert.AreEqual(ContentUploadResult.NotPermitted, await store.AppendUploadAsync(file, Bytes("x"), CancellationToken.None));
        Assert.AreEqual(ContentUploadOpeningResult.NotPermitted, (await store.OpenUploadAsync(file, CreateOrTruncate, CancellationToken.None)).Result);
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.DeleteFile(file));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.SetLastWriteTime(file, DateTimeOffset.UnixEpoch));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.CreateDirectory(store.MapRequestPathForScheme("/team/new", "ftp")));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.RemoveEmptyDirectory(empty));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.RenameEntry(file, store.MapRequestPathForScheme("/team/moved.txt", "ftp")));
        Assert.AreEqual("kept", await ReadAsync(store, file));
    }

    [TestMethod]
    public void RenameEntry_IntoAReadOnlyShareFromTheRoot_IsNotPermitted()
    {
        ContentStore store = StoreWith(MemoryWith(("doc.txt", "x")), new ContentShare("team", MemoryRoot, MemoryWith()) { IsReadOnly = true });

        ContentChangeResult result = store.RenameEntry(store.MapRequestPathForScheme("/doc.txt", "ftp"), store.MapRequestPathForScheme("/team/doc.txt", "ftp"));

        Assert.AreEqual(ContentChangeResult.NotPermitted, result);
    }

    [TestMethod]
    public void RenameEntry_BetweenTheRootAndAWritableShare_IsNotPermitted()
    {
        InMemoryContentFileSystem rootFiles = MemoryWith(("doc.txt", "x"));
        ContentStore store = StoreWith(rootFiles, new ContentShare("team", MemoryRoot, MemoryWith()));

        ContentChangeResult result = store.RenameEntry(store.MapRequestPathForScheme("/doc.txt", "ftp"), store.MapRequestPathForScheme("/team/doc.txt", "ftp"));

        Assert.AreEqual(ContentChangeResult.NotPermitted, result);
        Assert.AreEqual(ContentEntryKind.File, rootFiles.GetEntryKind(Path.Join(MemoryRoot, "doc.txt")));
    }

    [TestMethod]
    public void RenameEntry_InsideAWritableShare_IsDone()
    {
        InMemoryContentFileSystem shareFiles = MemoryWith(("doc.txt", "x"));
        ContentStore store = StoreWith(MemoryWith(), new ContentShare("team", MemoryRoot, shareFiles));

        ContentChangeResult result = store.RenameEntry(store.MapRequestPathForScheme("/team/doc.txt", "ftp"), store.MapRequestPathForScheme("/team/moved.txt", "ftp"));

        Assert.AreEqual(ContentChangeResult.Done, result);
        Assert.AreEqual(ContentEntryKind.File, shareFiles.GetEntryKind(Path.Join(MemoryRoot, "moved.txt")));
    }

    [TestMethod]
    public void TheSharesOwnDirectory_CanBeNeitherRenamedNorRemoved()
    {
        ContentStore store = StoreWith(MemoryWith(), new ContentShare("team", MemoryRoot, MemoryWith(("sub/", ""))));
        ContentPathMapping shareRoot = store.MapRequestPathForScheme("/team", "ftp");

        Assert.AreEqual(ContentChangeResult.NotPermitted, store.RemoveEmptyDirectory(shareRoot));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.RenameEntry(shareRoot, store.MapRequestPathForScheme("/team/sub/x", "ftp")));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.RenameEntry(store.MapRequestPathForScheme("/team/sub", "ftp"), shareRoot));
    }

    [TestMethod]
    [DataRow("sftp")]
    [DataRow("gopher")]
    public async Task AShareLimitedToFtpAndHttp_IsMissingFromAnotherScheme(string scheme)
    {
        InMemoryContentFileSystem rootFiles = MemoryWith(("team/doc.txt", "shadowed"));
        ContentStore store = StoreWith(rootFiles, FtpAndHttpShare(MemoryWith(("doc.txt", "share"))));

        ContentPathMapping mapping = store.MapRequestPathForScheme("/team/doc.txt", scheme);

        Assert.IsTrue(mapping.IsMapped);
        Assert.AreEqual(ContentEntryKind.None, mapping.EntryKind);
        Assert.AreEqual(ContentEntryKind.None, store.GetEntryKind(store.MapRequestPathForScheme("/team", scheme)));
        Assert.AreEqual(ContentUploadResult.NotPermitted, await store.WriteUploadAsync(mapping, Bytes("x"), CancellationToken.None));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.CreateDirectory(store.MapRequestPathForScheme("/team/new", scheme)));
        Assert.AreEqual(ContentChangeResult.Absent, store.DeleteFile(mapping));
        Assert.IsFalse(RootListingNames(store, scheme).Contains("team"));
    }

    [TestMethod]
    [DataRow("ftp")]
    [DataRow("HTTP")]
    public void AShareLimitedToFtpAndHttp_IsSeenFromThoseSchemesIgnoringCase(string scheme)
    {
        ContentStore store = StoreWith(MemoryWith(), FtpAndHttpShare(MemoryWith(("doc.txt", "share"))));

        Assert.AreEqual(ContentEntryKind.File, store.MapRequestPathForScheme("/team/doc.txt", scheme).EntryKind);
        CollectionAssert.Contains(RootListingNames(store, scheme), "team");
    }

    [TestMethod]
    public void ALookupNamingNoScheme_SeesOnlySharesOpenToEveryScheme()
    {
        ContentStore store = StoreWith(
            MemoryWith(),
            FtpAndHttpShare(MemoryWith(("doc.txt", "share"))),
            new ContentShare("open", MemoryRoot, MemoryWith(("doc.txt", "open"))));

        Assert.AreEqual(ContentEntryKind.None, store.MapRequestPath("/team/doc.txt").EntryKind);
        Assert.AreEqual(ContentEntryKind.File, store.MapRequestPath("/open/doc.txt").EntryKind);
        CollectionAssert.AreEqual(new[] { "open" }, RootListingNames(store, scheme: null));
    }

    [TestMethod]
    public void ListDirectory_TheRoot_ShowsEachVisibleShareInPlaceOfItsNamesake()
    {
        DateTimeOffset shareTime = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        InMemoryContentFileSystem shareFiles = MemoryWith();
        shareFiles.SetLastWriteTimeUtc(MemoryRoot, shareTime);
        var missingShareFiles = new InMemoryContentFileSystem(TimeProvider.System);
        ContentStore store = StoreWith(
            MemoryWith(("a.txt", "a"), ("team", "a root file the share shadows"), ("sub/b.txt", "b")),
            new ContentShare("team", MemoryRoot, shareFiles),
            new ContentShare("gone", Path.Join(MemoryRoot, "missing"), missingShareFiles));

        IReadOnlyList<ContentDirectoryEntry> entries = store.ListDirectory(store.MapRequestPathForScheme("/", "ftp"), CancellationToken.None).Entries;

        CollectionAssert.AreEqual(new[] { "a.txt", "sub", "team" }, entries.Select(entry => entry.Name).ToArray());
        Assert.AreEqual(new ContentDirectoryEntry("team", ContentEntryKind.Directory, null, shareTime), entries[2]);
        CollectionAssert.AreEqual(new[] { "b.txt" }, store.ListDirectory(store.MapRequestPathForScheme("/sub", "ftp"), CancellationToken.None).Entries.Select(entry => entry.Name).ToArray());
        Assert.IsEmpty(store.ListDirectory(store.MapRequestPathForScheme("/team", "ftp"), CancellationToken.None).Entries);
    }

    [TestMethod]
    [DataRow("/team/../doc.txt", ContentPathRefusal.DotSegment)]
    [DataRow("/team/%2e%2e/doc.txt", ContentPathRefusal.DotSegment)]
    [DataRow("/team/a%2Fb", ContentPathRefusal.SeparatorInSegment)]
    public void MapRequestPath_DotDotOrASeparatorInsideAShare_IsRefusedAsAtTheRoot(string requestPath, ContentPathRefusal expected)
    {
        ContentStore store = StoreWith(MemoryWith(), new ContentShare("team", MemoryRoot, MemoryWith()));

        Assert.AreEqual(expected, store.MapRequestPathForScheme(requestPath, "ftp").Refusal);
    }

    [TestMethod]
    public void MapRequestPath_ASymbolicLinkOutOfAShare_IsRefusedAsOneOutOfTheRoot()
    {
        string sharePath = Path.Join("/", "srv", "team");
        string outside = Path.Join("/", "srv", "secret");
        var shareFiles = new UnitTestInMemoryContentFileSystem()
            .AddDirectory(sharePath)
            .AddDirectory(outside)
            .AddFile(Path.Join(outside, "key"))
            .AddSymbolicLink(Path.Join(sharePath, "link"), outside);
        ContentStore store = StoreWith(MemoryWith(), new ContentShare("team", sharePath, shareFiles));

        Assert.AreEqual(ContentPathRefusal.ResolvesOutsideRoot, store.MapRequestPathForScheme("/team/link/key", "ftp").Refusal);
    }

    [TestMethod]
    [DataRow("/team/.surl/lock")]
    [DataRow("/team/.SURL")]
    public async Task TheServiceStateFolderInsideAShare_IsNeverServed(string requestPath)
    {
        InMemoryContentFileSystem shareFiles = MemoryWith((".surl/lock", "state"));
        ContentStore store = StoreWith(MemoryWith(), new ContentShare("team", MemoryRoot, shareFiles));

        ContentPathMapping mapping = store.MapRequestPathForScheme(requestPath, "ftp");

        Assert.AreEqual(ContentEntryKind.None, mapping.EntryKind);
        Assert.AreEqual(ContentUploadResult.NotPermitted, await store.WriteUploadAsync(mapping, Bytes("x"), CancellationToken.None));
        Assert.IsFalse(store.ListDirectory(store.MapRequestPathForScheme("/team", "ftp"), CancellationToken.None).Entries.Any(entry => entry.Name == ".surl"));
    }

    [TestMethod]
    [DataRow("/photos/2026/a.jpg", "/team/albums/2026/a.jpg")]
    [DataRow("/media/2026/a.jpg", "/media/2026/a.jpg")]
    [DataRow("/raw/a.jpg", "/media/raw/a.jpg")]
    public void MapBucketRequestPath_ABucketName_MapsOntoItsContentPath(string objectPath, string contentPath)
    {
        ContentStore store = new(
            MemoryRoot,
            MemoryWith(("media/2026/a.jpg", "m"), ("media/raw/a.jpg", "r")),
            UploadsAndListingsOn,
            [new ContentShare("team", MemoryRoot, MemoryWith(("albums/2026/a.jpg", "t")))],
            [new ContentBucket("photos", "/team/albums"), new ContentBucket("raw", "/media/raw")]);

        ContentPathMapping mapping = store.MapBucketRequestPath(objectPath, "s3");

        Assert.AreEqual(ContentEntryKind.File, mapping.EntryKind);
        Assert.AreEqual(store.MapRequestPathForScheme(contentPath, "s3").Location, mapping.Location);
    }

    [TestMethod]
    public void MapRequestPath_ABucketName_IsNeverReadByTheOriginalFileServers()
    {
        ContentStore store = new(MemoryRoot, MemoryWith(), UploadsAndListingsOn, [], [new ContentBucket("photos", "/media")]);

        ContentPathMapping mapping = store.MapRequestPathForScheme("/photos/a.jpg", "ftp");

        Assert.AreEqual(Path.Join(MemoryRoot, "photos", "a.jpg"), mapping.Location);
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/photos/")]
    public void MapBucketRequestPath_TheRootOrABucketAlone_IsADirectory(string objectPath)
    {
        ContentStore store = new(MemoryRoot, MemoryWith(("media/a.jpg", "m")), UploadsAndListingsOn, [], [new ContentBucket("photos", "/media")]);

        Assert.AreEqual(ContentEntryKind.Directory, store.MapBucketRequestPath(objectPath, "s3").EntryKind);
    }

    [TestMethod]
    public void MapBucketRequestPath_ARefusedPath_IsRefused()
    {
        ContentStore store = new(MemoryRoot, MemoryWith(), UploadsAndListingsOn, [], [new ContentBucket("photos", "/media")]);

        Assert.AreEqual(ContentPathRefusal.DotSegment, store.MapBucketRequestPath("/photos/../x", "s3").Refusal);
    }

    [TestMethod]
    public void MapRequestPath_AnEmptyScheme_Throws()
    {
        ContentStore store = StoreWith(MemoryWith());

        Assert.ThrowsExactly<ArgumentException>(() => store.MapRequestPathForScheme("/", ""));
        Assert.ThrowsExactly<ArgumentException>(() => store.MapBucketRequestPath("/", ""));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.MapBucketRequestPath(null!, "s3"));
    }

    [TestMethod]
    public void Constructor_KeepsTheSharesAndBucketsInTheOrderGiven()
    {
        var team = new ContentShare("team", MemoryRoot, MemoryWith());
        var ops = new ContentShare("ops", MemoryRoot, MemoryWith());
        var photos = new ContentBucket("photos", "/media");

        ContentStore store = new(MemoryRoot, MemoryWith(), UploadsAndListingsOn, [team, ops], [photos]);

        CollectionAssert.AreEqual(new[] { team, ops }, store.Shares.ToArray());
        CollectionAssert.AreEqual(new[] { photos }, store.Buckets.ToArray());
        Assert.IsEmpty(StoreWith(MemoryWith()).Shares);
        Assert.IsEmpty(new ContentStore(MemoryRoot, MemoryWith(), UploadsAndListingsOn).Buckets);
    }

    [TestMethod]
    public void Constructor_RefusesTwoSharesOrTwoBucketsOfOneName()
    {
        var team = new ContentShare("team", MemoryRoot, MemoryWith());
        var photos = new ContentBucket("photos", "/media");

        Assert.ThrowsExactly<ArgumentException>(() => new ContentStore(MemoryRoot, MemoryWith(), UploadsAndListingsOn, [team, team], []));
        Assert.ThrowsExactly<ArgumentException>(() => new ContentStore(MemoryRoot, MemoryWith(), UploadsAndListingsOn, [], [photos, photos]));
    }

    [TestMethod]
    public void Constructor_RefusesMissingSharesOrBuckets()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ContentStore(MemoryRoot, MemoryWith(), UploadsAndListingsOn, null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ContentStore(MemoryRoot, MemoryWith(), UploadsAndListingsOn, [], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ContentStore(MemoryRoot, MemoryWith(), UploadsAndListingsOn, [null!], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ContentStore(MemoryRoot, MemoryWith(), UploadsAndListingsOn, [], [null!]));
    }

    private static ContentShare FtpAndHttpShare(IContentFileSystem files) =>
        new("team", MemoryRoot, files) { Schemes = new HashSet<string> { "ftp", "http" } };

    private static ContentStore StoreWith(IContentFileSystem rootFiles, params ContentShare[] shares) =>
        new(MemoryRoot, rootFiles, UploadsAndListingsOn, shares, []);

    // Each entry is a path relative to the in-memory root: a file holding its text, or a
    // directory when the path ends in '/'. Directories above an entry are created first.
    private static InMemoryContentFileSystem MemoryWith(params (string RelativePath, string Text)[] entries)
    {
        var files = new InMemoryContentFileSystem(TimeProvider.System);
        foreach ((string relativePath, string text) in entries)
        {
            string[] segments = relativePath.TrimEnd('/').Split('/');
            for (int depth = 1; depth < segments.Length; depth++)
            {
                string directory = Path.Join([MemoryRoot, .. segments[..depth]]);
                if (files.GetEntryKind(directory) == ContentEntryKind.None)
                {
                    files.CreateDirectory(directory);
                }
            }

            string path = Path.Join([MemoryRoot, .. segments]);
            if (relativePath.EndsWith('/'))
            {
                files.CreateDirectory(path);
                continue;
            }

            using Stream written = files.CreateFileForAsyncWrite(path);
            written.Write(Encoding.UTF8.GetBytes(text));
        }

        return files;
    }

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));

    private static async Task<string> ReadAsync(ContentStore store, ContentPathMapping mapping)
    {
        using var destination = new MemoryStream();
        long length = store.GetFileStatus(mapping)!.Length;
        await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(length), destination, CancellationToken.None);
        return Encoding.UTF8.GetString(destination.ToArray());
    }

    private static string[] RootListingNames(ContentStore store, string? scheme)
    {
        ContentPathMapping root = scheme is null ? store.MapRequestPath("/") : store.MapRequestPathForScheme("/", scheme);
        return [.. store.ListDirectory(root, CancellationToken.None).Entries.Select(entry => entry.Name)];
    }
}
