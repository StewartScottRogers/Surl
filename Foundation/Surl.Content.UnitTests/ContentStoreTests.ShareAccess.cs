namespace Surl.Content;

public sealed partial class ContentStoreTests
{
    private static readonly ContentCaller Alice = ContentCaller.ForAccount("alice", "sftp");

    private static readonly ContentCaller Bob = ContentCaller.ForAccount("bob", "sftp");

    private static readonly ContentCaller Carol = ContentCaller.ForAccount("carol", "sftp");

    private static readonly ContentCaller TftpPeer = ContentCaller.Anonymous("tftp");

    [TestMethod]
    public async Task ReadAccess_ReadsAndListsButEveryWriteIsNotPermitted()
    {
        InMemoryContentFileSystem shareFiles = MemoryWith(("doc.txt", "kept"), ("empty/", ""));
        ContentStore store = StoreWith(MemoryWith(), GrantedShare(shareFiles, ("alice", ContentShareAccess.Read)));
        ContentPathMapping file = store.MapRequestPathForCaller("/team/doc.txt", Alice);
        ContentPathMapping empty = store.MapRequestPathForCaller("/team/empty", Alice);

        Assert.AreEqual("kept", await ReadAsync(store, file));
        Assert.IsTrue(store.ListDirectory(store.MapRequestPathForCaller("/team", Alice), CancellationToken.None).IsListed);
        Assert.AreEqual(ContentUploadResult.NotPermitted, await store.WriteUploadAsync(file, Bytes("x"), CancellationToken.None));
        Assert.AreEqual(ContentUploadResult.NotPermitted, await store.AppendUploadAsync(file, Bytes("x"), CancellationToken.None));
        Assert.AreEqual(ContentUploadOpeningResult.NotPermitted, (await store.OpenUploadAsync(file, CreateOrTruncate, CancellationToken.None)).Result);
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.DeleteFile(file));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.SetLastWriteTime(file, DateTimeOffset.UnixEpoch));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.CreateDirectory(store.MapRequestPathForCaller("/team/new", Alice)));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.RemoveEmptyDirectory(empty));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.RenameEntry(file, store.MapRequestPathForCaller("/team/moved.txt", Alice)));
        Assert.AreEqual("kept", await ReadAsync(store, file));
    }

    [TestMethod]
    public async Task ReadWriteAccess_UploadsRenamesAndDeletes()
    {
        InMemoryContentFileSystem shareFiles = MemoryWith(("doc.txt", "old"));
        ContentStore store = StoreWith(MemoryWith(), GrantedShare(shareFiles, ("alice", ContentShareAccess.ReadWrite)));
        ContentPathMapping file = store.MapRequestPathForCaller("/team/doc.txt", Alice);
        ContentPathMapping moved = store.MapRequestPathForCaller("/team/moved.txt", Alice);

        Assert.AreEqual(ContentUploadResult.Written, await store.WriteUploadAsync(file, Bytes("new"), CancellationToken.None));
        Assert.AreEqual("new", await ReadAsync(store, file));
        Assert.AreEqual(ContentChangeResult.Done, store.RenameEntry(file, moved));
        Assert.AreEqual(ContentChangeResult.Done, store.DeleteFile(moved));
        Assert.AreEqual(ContentChangeResult.Done, store.CreateDirectory(store.MapRequestPathForCaller("/team/new", Alice)));
        Assert.AreEqual(ContentEntryKind.Directory, shareFiles.GetEntryKind(Path.Join(MemoryRoot, "new")));
    }

    [TestMethod]
    public async Task AnUnlistedAccount_FindsAShareWithGrantsMissing_TheSameAsAPathThatDoesNotExist()
    {
        ContentStore store = StoreWith(MemoryWith(("team/doc.txt", "root namesake")), GrantedShare(MemoryWith(("doc.txt", "secret")), ("alice", ContentShareAccess.ReadWrite)));

        ContentPathMapping forbidden = store.MapRequestPathForCaller("/team/doc.txt", Bob);
        ContentPathMapping missing = store.MapRequestPathForCaller("/team/nothing.txt", Bob);
        ContentPathMapping missingForAlice = store.MapRequestPathForCaller("/team/nothing.txt", Alice);

        foreach (ContentPathMapping mapping in new[] { forbidden, missing, missingForAlice })
        {
            Assert.IsTrue(mapping.IsMapped);
            Assert.AreEqual(ContentEntryKind.None, mapping.EntryKind);
            Assert.IsNull(store.GetEntryStatus(mapping));
            Assert.AreEqual(ContentEntryKind.None, store.ListDirectory(mapping, CancellationToken.None).LocationKind);
        }

        Assert.AreEqual(ContentChangeResult.Absent, store.DeleteFile(forbidden));
        Assert.AreEqual(ContentChangeResult.Absent, store.DeleteFile(missingForAlice));
        Assert.AreEqual(ContentUploadResult.NotPermitted, await store.WriteUploadAsync(forbidden, Bytes("x"), CancellationToken.None));
        Assert.AreEqual(ContentEntryKind.None, store.ListDirectory(store.MapRequestPathForCaller("/team", Bob), CancellationToken.None).LocationKind);
    }

    [TestMethod]
    public void AnAnonymousCaller_FindsAShareWithGrantsMissing()
    {
        ContentStore store = StoreWith(MemoryWith(), GrantedShare(MemoryWith(("doc.txt", "secret")), ("alice", ContentShareAccess.ReadWrite)));

        Assert.AreEqual(ContentEntryKind.None, store.MapRequestPathForCaller("/team/doc.txt", TftpPeer).EntryKind);
        Assert.AreEqual(ContentEntryKind.None, store.MapRequestPathForScheme("/team/doc.txt", "sftp").EntryKind);
        Assert.AreEqual(ContentEntryKind.None, store.MapRequestPath("/team/doc.txt").EntryKind);
        Assert.AreEqual(ContentEntryKind.None, store.MapBucketRequestPath("/team/doc.txt", "s3").EntryKind);
    }

    [TestMethod]
    public async Task Everyone_ReachesAnonymousAndUnlistedCallers_AndAnAccountGetsTheWiderOfItsOwnAndEveryonesGrant()
    {
        InMemoryContentFileSystem shareFiles = MemoryWith(("doc.txt", "public"));
        ContentStore store = StoreWith(
            MemoryWith(),
            GrantedShare(shareFiles, (ContentShare.EveryoneMember, ContentShareAccess.Read), ("alice", ContentShareAccess.ReadWrite), ("bob", ContentShareAccess.Read)));

        Assert.AreEqual("public", await ReadAsync(store, store.MapRequestPathForCaller("/team/doc.txt", TftpPeer)));
        Assert.AreEqual("public", await ReadAsync(store, store.MapRequestPathForCaller("/team/doc.txt", Carol)));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.DeleteFile(store.MapRequestPathForCaller("/team/doc.txt", TftpPeer)));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.DeleteFile(store.MapRequestPathForCaller("/team/doc.txt", Bob)));
        Assert.AreEqual(ContentChangeResult.Done, store.DeleteFile(store.MapRequestPathForCaller("/team/doc.txt", Alice)));
    }

    [TestMethod]
    public async Task EveryoneReadWrite_IsNotNarrowedByAnAccountsOwnReadGrant()
    {
        ContentStore store = StoreWith(
            MemoryWith(),
            GrantedShare(MemoryWith(), (ContentShare.EveryoneMember, ContentShareAccess.ReadWrite), ("bob", ContentShareAccess.Read)));

        Assert.AreEqual(ContentUploadResult.Written, await store.WriteUploadAsync(store.MapRequestPathForCaller("/team/up.txt", Bob), Bytes("x"), CancellationToken.None));
        Assert.AreEqual(ContentUploadResult.Written, await store.WriteUploadAsync(store.MapRequestPathForCaller("/team/up2.txt", TftpPeer), Bytes("x"), CancellationToken.None));
    }

    [TestMethod]
    public async Task AllowUploadsOff_RefusesEveryWriteEvenToReadWriteAccess()
    {
        InMemoryContentFileSystem shareFiles = MemoryWith(("doc.txt", "kept"));
        var store = new ContentStore(
            MemoryRoot,
            MemoryWith(),
            new ContentExposureOptions { ListDirectories = true },
            [GrantedShare(shareFiles, ("alice", ContentShareAccess.ReadWrite))],
            []);
        ContentPathMapping file = store.MapRequestPathForCaller("/team/doc.txt", Alice);

        Assert.AreEqual("kept", await ReadAsync(store, file));
        Assert.AreEqual(ContentUploadResult.NotPermitted, await store.WriteUploadAsync(file, Bytes("x"), CancellationToken.None));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.DeleteFile(file));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.RenameEntry(file, store.MapRequestPathForCaller("/team/moved.txt", Alice)));
    }

    [TestMethod]
    public async Task ReadOnlyShare_RefusesEveryWriteEvenToReadWriteAccess()
    {
        ContentShare share = GrantedShare(MemoryWith(("doc.txt", "kept")), ("alice", ContentShareAccess.ReadWrite));
        ContentStore store = StoreWith(MemoryWith(), new ContentShare(share.Name, share.Path, share.FileSystem) { IsReadOnly = true, Grants = share.Grants });

        Assert.AreEqual(ContentUploadResult.NotPermitted, await store.WriteUploadAsync(store.MapRequestPathForCaller("/team/doc.txt", Alice), Bytes("x"), CancellationToken.None));
    }

    [TestMethod]
    public void ASchemeTheShareExcludes_FindsItMissingWhateverTheGrant()
    {
        ContentStore store = StoreWith(
            MemoryWith(),
            new ContentShare("team", MemoryRoot, MemoryWith(("doc.txt", "x")))
            {
                Schemes = new HashSet<string> { "ftp" },
                Grants = new Dictionary<string, ContentShareAccess> { ["alice"] = ContentShareAccess.ReadWrite },
            });

        Assert.AreEqual(ContentEntryKind.None, store.MapRequestPathForCaller("/team/doc.txt", Alice).EntryKind);
        Assert.AreEqual(ContentEntryKind.File, store.MapRequestPathForCaller("/team/doc.txt", ContentCaller.ForAccount("alice", "ftp")).EntryKind);
    }

    [TestMethod]
    public void ListDirectory_TheRoot_ShowsOnlyTheSharesTheCallerMayRead()
    {
        ContentStore store = StoreWith(
            MemoryWith(("a.txt", "x")),
            GrantedShare(MemoryWith(), ("alice", ContentShareAccess.Read)),
            new ContentShare("open", MemoryRoot, MemoryWith()));

        CollectionAssert.AreEqual(new[] { "a.txt", "open", "team" }, CallerRootListingNames(store, Alice));
        CollectionAssert.AreEqual(new[] { "a.txt", "open" }, CallerRootListingNames(store, Bob));
        CollectionAssert.AreEqual(new[] { "a.txt", "open" }, CallerRootListingNames(store, TftpPeer));
    }

    [TestMethod]
    public async Task WithNoGrants_EveryCallerReadsAndWritesAsBefore()
    {
        InMemoryContentFileSystem shareFiles = MemoryWith(("doc.txt", "x"));
        ContentStore store = StoreWith(MemoryWith(), new ContentShare("team", MemoryRoot, shareFiles));

        Assert.AreEqual(ContentUploadResult.Written, await store.WriteUploadAsync(store.MapRequestPathForCaller("/team/doc.txt", TftpPeer), Bytes("y"), CancellationToken.None));
        Assert.AreEqual(ContentChangeResult.Done, store.DeleteFile(store.MapRequestPathForCaller("/team/doc.txt", Bob)));
    }

    [TestMethod]
    public async Task TheRoot_HasNoAccessRows()
    {
        ContentStore store = StoreWith(MemoryWith(("doc.txt", "x")), GrantedShare(MemoryWith(), ("alice", ContentShareAccess.Read)));

        Assert.AreEqual(ContentUploadResult.Written, await store.WriteUploadAsync(store.MapRequestPathForCaller("/doc.txt", TftpPeer), Bytes("y"), CancellationToken.None));
    }

    [TestMethod]
    public void MapBucketRequestPathForCaller_AppliesTheAccessOfTheShareTheBucketLiesIn()
    {
        var store = new ContentStore(
            MemoryRoot,
            MemoryWith(),
            UploadsAndListingsOn,
            [GrantedShare(MemoryWith(("albums/p.jpg", "x")), ("alice", ContentShareAccess.Read))],
            [new ContentBucket("photos", "/team/albums")]);

        ContentPathMapping forAlice = store.MapBucketRequestPathForCaller("/photos/p.jpg", ContentCaller.ForAccount("alice", "s3"));
        ContentPathMapping forBob = store.MapBucketRequestPathForCaller("/photos/p.jpg", ContentCaller.ForAccount("bob", "s3"));

        Assert.AreEqual(ContentEntryKind.File, forAlice.EntryKind);
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.DeleteFile(forAlice));
        Assert.AreEqual(ContentEntryKind.None, forBob.EntryKind);
    }

    [TestMethod]
    public void TheCallerMappings_ANullCaller_Throws()
    {
        ContentStore store = StoreWith(MemoryWith());

        Assert.ThrowsExactly<ArgumentNullException>(() => store.MapRequestPathForCaller("/", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.MapBucketRequestPathForCaller("/", null!));
    }

    private static ContentShare GrantedShare(IContentFileSystem files, params (string Member, ContentShareAccess Access)[] grants) =>
        new("team", MemoryRoot, files) { Grants = grants.ToDictionary(grant => grant.Member, grant => grant.Access) };

    private static string[] CallerRootListingNames(ContentStore store, ContentCaller caller) =>
        [.. store.ListDirectory(store.MapRequestPathForCaller("/", caller), CancellationToken.None).Entries.Select(entry => entry.Name)];
}
