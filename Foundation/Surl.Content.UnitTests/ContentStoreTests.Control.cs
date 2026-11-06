using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Content;

public sealed partial class ContentStoreTests
{
    private static string TeamShareRoot => Path.Join(MemoryRoot, "team");

    [TestMethod]
    public async Task SeedFileAsync_WithUploadsOffAndAnOutOfSpaceRule_WritesTheFileAndItsFoldersReadBackByteForByte()
    {
        var events = new RecordingSurlEventPublisher();
        var faults = new UnitTestPathPrefixFaultPlan(new FaultRule("full", FaultKind.OutOfSpace) { Path = "/a" });
        var store = new ContentStore(MemoryRoot, new InMemoryContentFileSystem(TimeProvider.System), new ContentExposureOptions(), [], [], events, faults);
        byte[] contents = [0, 1, 2, 255, 13, 10];

        ContentUploadResult result = await store.SeedFileAsync("/a/b/c.bin", new MemoryStream(contents), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, result);
        CollectionAssert.AreEqual(contents, await ReadBackAsync(store, "/a/b/c.bin"));
        SurlEvent[] expected =
        [
            Changed(StoreChange.Seeded, "/a", null),
            Changed(StoreChange.Seeded, "/a/b", null),
            Changed(StoreChange.Seeded, "/a/b/c.bin", 6),
        ];
        CollectionAssert.AreEqual(expected, events.Events.ToArray());
    }

    [TestMethod]
    [DataRow("/.surl/x.txt")]
    [DataRow("/.SURL/objects/x")]
    [DataRow("/.hidden.txt")]
    [DataRow("/folder/")]
    [DataRow("/folder")]
    [DataRow("/file.txt/x.txt")]
    [DataRow("/file.txt/deeper/x.txt")]
    public async Task SeedFileAsync_UnderSurlHiddenADirectoryOrBelowAFile_IsNotPermittedAndPublishesNothing(string requestPath)
    {
        var events = new RecordingSurlEventPublisher();
        ContentStore store = await StoreWithFolderAndFileAsync(events);

        ContentUploadResult result = await store.SeedFileAsync(requestPath, Bytes("x"), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.NotPermitted, result);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task SeedFileAsync_PastMaxUploadBytes_IsTooLargeAndPublishesOnlyItsFolder()
    {
        var events = new RecordingSurlEventPublisher();
        var store = new ContentStore(MemoryRoot, new InMemoryContentFileSystem(TimeProvider.System), new ContentExposureOptions { MaxUploadBytes = 2 }, [], [], events);

        ContentUploadResult result = await store.SeedFileAsync("/d/big.txt", Bytes("too big"), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.TooLarge, result);
        Assert.AreEqual(ContentEntryKind.None, store.GetEntryKind(store.MapRequestPath("/d/big.txt")));
        CollectionAssert.AreEqual(new SurlEvent[] { Changed(StoreChange.Seeded, "/d", null) }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task SeedFileAsync_ARefusedPathOrNoContents_Throws()
    {
        var store = new ContentStore(MemoryRoot, new InMemoryContentFileSystem(TimeProvider.System), new ContentExposureOptions());

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SeedFileAsync("/../x", Bytes("x"), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.SeedFileAsync("/x", null!, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => store.SeedFileAsync("/x", Bytes("x"), new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public async Task SeedFileAsync_IntoAReadOnlyShareClosedToEveryoneAndLimitedToOneScheme_IsWritten()
    {
        var events = new RecordingSurlEventPublisher();
        var share = new ContentShare("team", MemoryRoot, new InMemoryContentFileSystem(TimeProvider.System))
        {
            IsReadOnly = true,
            Schemes = new HashSet<string>(StringComparer.Ordinal) { "sftp" },
            Grants = new Dictionary<string, ContentShareAccess> { ["alice"] = ContentShareAccess.Read },
        };
        var store = new ContentStore(MemoryRoot, new InMemoryContentFileSystem(TimeProvider.System), new ContentExposureOptions(), [share], [], events);

        ContentUploadResult result = await store.SeedFileAsync("/team/x.txt", Bytes("hi"), CancellationToken.None);

        Assert.AreEqual(ContentUploadResult.Written, result);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("hi"), await ReadBackAsync(store, "/team/x.txt", ContentCaller.ForAccount("alice", "sftp")));
        CollectionAssert.AreEqual(new SurlEvent[] { Changed(StoreChange.Seeded, "/team/x.txt", 2) }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task SeedFileAsyncAndSeedFolder_InAShareWhoseFolderIsMissing_AreNotPermitted()
    {
        var share = new ContentShare("team", TeamShareRoot, new InMemoryContentFileSystem(TimeProvider.System));
        var store = new ContentStore(MemoryRoot, new InMemoryContentFileSystem(TimeProvider.System), new ContentExposureOptions(), [share], []);

        Assert.AreEqual(ContentUploadResult.NotPermitted, await store.SeedFileAsync("/team/x.txt", Bytes("x"), CancellationToken.None));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.SeedFolder("/team"));
        Assert.AreEqual(ContentChangeResult.NotPermitted, store.SeedFolder("/team/sub"));
    }

    [TestMethod]
    public void SeedFolder_NewNestedFolder_CreatesEachFolderAndPublishesEach()
    {
        var events = new RecordingSurlEventPublisher();
        var store = new ContentStore(MemoryRoot, new InMemoryContentFileSystem(TimeProvider.System), new ContentExposureOptions(), [], [], events);

        Assert.AreEqual(ContentChangeResult.Done, store.SeedFolder("/x/y/"));
        Assert.AreEqual(ContentChangeResult.Exists, store.SeedFolder("/x/y"));
        Assert.AreEqual(ContentChangeResult.Exists, store.SeedFolder("/"));

        Assert.AreEqual(ContentEntryKind.Directory, store.GetEntryKind(store.MapRequestPath("/x/y")));
        CollectionAssert.AreEqual(new SurlEvent[] { Changed(StoreChange.Seeded, "/x", null), Changed(StoreChange.Seeded, "/x/y", null) }, events.Events.ToArray());
    }

    [TestMethod]
    [DataRow("/.surl/sub")]
    [DataRow("/.hidden")]
    [DataRow("/file.txt")]
    [DataRow("/file.txt/sub")]
    public async Task SeedFolder_UnderSurlHiddenOrAtOrBelowAFile_IsNotPermittedAndPublishesNothing(string requestPath)
    {
        var events = new RecordingSurlEventPublisher();
        ContentStore store = await StoreWithFolderAndFileAsync(events);

        Assert.AreEqual(ContentChangeResult.NotPermitted, store.SeedFolder(requestPath));
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public void SeedFolder_ARefusedPath_Throws()
    {
        var store = new ContentStore(MemoryRoot, new InMemoryContentFileSystem(TimeProvider.System), new ContentExposureOptions());

        Assert.ThrowsExactly<ArgumentException>(() => store.SeedFolder("/a//b"));
    }

    [TestMethod]
    public async Task ResetAsync_AfterChanges_ReturnsTheHostsFilesAndTheConfiguredSharesAndBucketsAsEmptyFoldersWithNoMetadata()
    {
        var events = new RecordingSurlEventPublisher();
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        await WriteAsync(fileSystem, Path.Join(MemoryRoot, "given.txt"), "from the host");
        var teamFileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        ContentBucket[] buckets = [new("photos", "/media/photos"), new("given", "/given.txt"), new("dot", "/.dot")];
        var store = new ContentStore(MemoryRoot, fileSystem, UploadsOn, [new ContentShare("team", TeamShareRoot, teamFileSystem)], buckets, events);
        var index = new ObjectMetadataIndex(fileSystem, MemoryRoot, persists: false);
        Assert.IsTrue(store.IsControllable);
        await store.SeedFileAsync("/team/t.txt", Bytes("t"), CancellationToken.None);
        await store.SeedFileAsync("/media/photos/a.jpg", Bytes("jpeg"), CancellationToken.None);
        await index.StoreAsync("media/photos/a.jpg", new ObjectMetadata { ContentType = "image/jpeg" }, CancellationToken.None);
        store.DeleteFile(store.MapRequestPath("/given.txt"));
        int seen = events.Events.Count;

        await store.ResetAsync(index, CancellationToken.None);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("from the host"), await ReadBackAsync(store, "/given.txt"));
        CollectionAssert.AreEqual(new[] { "given.txt", "media", "team" }, Names(store, "/"));
        Assert.IsEmpty(Names(store, "/media/photos"));
        Assert.IsEmpty(Names(store, "/team"));
        Assert.AreEqual(0, index.Count);
        Assert.AreEqual(ContentEntryKind.None, store.GetEntryKind(store.MapRequestPath("/.dot")));
        CollectionAssert.AreEqual(new SurlEvent[] { Changed(StoreChange.Reset, "/", null) }, events.Events.Skip(seen).ToArray());
    }

    [TestMethod]
    public async Task TakeSnapshotAsyncThenRestoreAsync_AfterChanges_GivesBackTheSameFilesFoldersAndObjectMetadata()
    {
        var events = new RecordingSurlEventPublisher();
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        var store = new ContentStore(MemoryRoot, fileSystem, UploadsOn, [], [], events);
        var index = new ObjectMetadataIndex(fileSystem, MemoryRoot, persists: false);
        await store.SeedFileAsync("/b/kept.txt", Bytes("kept"), CancellationToken.None);
        await store.SeedFileAsync("/b/changed.txt", Bytes("before"), CancellationToken.None);
        store.SeedFolder("/empty");
        await index.StoreAsync("b/changed.txt", new ObjectMetadata { ContentType = "text/x-before" }, CancellationToken.None);
        ContentStoreSnapshot snapshot = await store.TakeSnapshotAsync(index, CancellationToken.None);
        int seen = events.Events.Count;

        await store.WriteUploadAsync(store.MapRequestPath("/b/changed.txt"), Bytes("after!"), CancellationToken.None);
        await index.StoreAsync("b/changed.txt", new ObjectMetadata { ContentType = "text/x-after" }, CancellationToken.None);
        store.DeleteFile(store.MapRequestPath("/b/kept.txt"));
        store.RemoveEmptyDirectory(store.MapRequestPath("/empty"));
        await store.SeedFileAsync("/new.txt", Bytes("new"), CancellationToken.None);
        seen = events.Events.Count;
        await store.RestoreAsync(snapshot, CancellationToken.None);

        Assert.AreEqual(10, snapshot.ByteCount);
        CollectionAssert.AreEqual(new[] { "b", "empty" }, Names(store, "/"));
        CollectionAssert.AreEqual(new[] { "changed.txt", "kept.txt" }, Names(store, "/b"));
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("before"), await ReadBackAsync(store, "/b/changed.txt"));
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("kept"), await ReadBackAsync(store, "/b/kept.txt"));
        ObjectMetadataReading reading = await index.ReadAsync("b/changed.txt", CancellationToken.None);
        Assert.AreEqual("text/x-before", reading.Properties!.ContentType);
        CollectionAssert.AreEqual(new SurlEvent[] { Changed(StoreChange.Restored, "/", null) }, events.Events.Skip(seen).ToArray());
    }

    [TestMethod]
    public async Task TakeSnapshotAsync_WithoutAnIndexAndAShareOnTheRootsFileSystem_RestoresBothAndPublishesNothingItself()
    {
        var events = new RecordingSurlEventPublisher();
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        fileSystem.CreateDirectory(TeamShareRoot);
        var store = new ContentStore(MemoryRoot, fileSystem, UploadsOn, [new ContentShare("team", TeamShareRoot, fileSystem)], [], events);
        await store.SeedFileAsync("/team/t.txt", Bytes("t"), CancellationToken.None);
        int seen = events.Events.Count;

        ContentStoreSnapshot snapshot = await store.TakeSnapshotAsync(null, CancellationToken.None);
        store.DeleteFile(store.MapRequestPath("/team/t.txt"));
        await store.RestoreAsync(snapshot, CancellationToken.None);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("t"), await ReadBackAsync(store, "/team/t.txt"));
        CollectionAssert.AreEqual(
            new SurlEvent[] { Changed(StoreChange.Deleted, "/team/t.txt", null), Changed(StoreChange.Restored, "/", null) },
            events.Events.Skip(seen).ToArray());
    }

    [TestMethod]
    public async Task RestoreAsync_ASnapshotFromAnotherStoreOrNone_IsRefused()
    {
        var store = new ContentStore(MemoryRoot, new InMemoryContentFileSystem(TimeProvider.System), UploadsOn);
        var other = new ContentStore(MemoryRoot, new InMemoryContentFileSystem(TimeProvider.System), UploadsOn);
        ContentStoreSnapshot othersSnapshot = await other.TakeSnapshotAsync(null, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.RestoreAsync(othersSnapshot, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.RestoreAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task RestoreAsync_WhileAnUploadSessionIsOpen_LeavesItsTemporaryFileOutAndFailsItsCommit()
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        var store = new ContentStore(MemoryRoot, fileSystem, UploadsOn);
        ContentUploadOpeningOutcome outcome = await store.OpenUploadAsync(store.MapRequestPath("/late.txt"), CreateOrTruncate, CancellationToken.None);
        await using ContentUploadSession session = outcome.Session!;
        await session.WriteAtAsync(0, Encoding.UTF8.GetBytes("late"), CancellationToken.None);

        ContentStoreSnapshot snapshot = await store.TakeSnapshotAsync(null, CancellationToken.None);
        await store.RestoreAsync(snapshot, CancellationToken.None);

        Assert.IsEmpty(fileSystem.EnumerateDirectoryEntryNames(MemoryRoot));
        Assert.AreEqual(0, fileSystem.TotalBytes);
        await Assert.ThrowsAsync<IOException>(() => session.CommitAsync(CancellationToken.None));
        Assert.AreEqual(ContentEntryKind.None, store.GetEntryKind(store.MapRequestPath("/late.txt")));
    }

    [TestMethod]
    public async Task ResetAsync_WhileAnUploadIsBeingWritten_FailsTheUploadsNextWrite()
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        var store = new ContentStore(MemoryRoot, fileSystem, UploadsOn);
        ContentUploadOpeningOutcome outcome = await store.OpenUploadAsync(store.MapRequestPath("/late.txt"), CreateOrTruncate, CancellationToken.None);
        await using ContentUploadSession session = outcome.Session!;

        await store.ResetAsync(null, CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(() => session.WriteAtAsync(0, Encoding.UTF8.GetBytes("late"), CancellationToken.None));
    }

    [TestMethod]
    public async Task ResetSnapshotAndRestore_OnADiskLikeFileSystem_AreRefusedAndChangeNothingWhileSeedingIsAllowed()
    {
        var events = new RecordingSurlEventPublisher();
        var diskLike = new UnitTestInMemoryContentFileSystem().AddDirectory(Root).AddFile(Path.Join(Root, "real.txt"));
        var store = new ContentStore(Root, diskLike, UploadsOn, [], [], events);
        var memory = new ContentStore(MemoryRoot, new InMemoryContentFileSystem(TimeProvider.System), UploadsOn);
        ContentStoreSnapshot memorySnapshot = await memory.TakeSnapshotAsync(null, CancellationToken.None);

        Assert.IsFalse(store.IsControllable);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.ResetAsync(null, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.TakeSnapshotAsync(null, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.RestoreAsync(memorySnapshot, CancellationToken.None));

        Assert.AreEqual(ContentEntryKind.File, store.GetEntryKind(store.MapRequestPath("/real.txt")));
        Assert.AreEqual(ContentUploadResult.Written, await store.SeedFileAsync("/seeded.txt", Bytes("s"), CancellationToken.None));
        Assert.AreEqual(ContentChangeResult.Done, store.SeedFolder("/seeded"));
        CollectionAssert.AreEqual(
            new SurlEvent[] { Changed(StoreChange.Seeded, "/seeded.txt", 1), Changed(StoreChange.Seeded, "/seeded", null) },
            events.Events.ToArray());
    }

    [TestMethod]
    public async Task ResetAndSnapshot_WithADiskLikeShareOrAPersistedIndex_AreRefused()
    {
        var withDiskShare = new ContentStore(
            MemoryRoot, new InMemoryContentFileSystem(TimeProvider.System), UploadsOn,
            [new ContentShare("team", Root, new UnitTestInMemoryContentFileSystem().AddDirectory(Root))], []);
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        var inMemory = new ContentStore(MemoryRoot, fileSystem, UploadsOn);
        var persisted = new ObjectMetadataIndex(fileSystem, MemoryRoot, persists: true);

        Assert.IsFalse(withDiskShare.IsControllable);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => withDiskShare.ResetAsync(null, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => inMemory.ResetAsync(persisted, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => inMemory.TakeSnapshotAsync(persisted, CancellationToken.None));
    }

    private static async Task<ContentStore> StoreWithFolderAndFileAsync(ISurlEventPublisher events)
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        fileSystem.CreateDirectory(Path.Join(MemoryRoot, "folder"));
        await WriteAsync(fileSystem, Path.Join(MemoryRoot, "file.txt"), "f");
        return new ContentStore(MemoryRoot, fileSystem, new ContentExposureOptions(), [], [], events);
    }

    private static async Task WriteAsync(InMemoryContentFileSystem fileSystem, string path, string text)
    {
        await using Stream stream = fileSystem.CreateFileForAsyncWrite(path);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(text));
    }

    private static Task<byte[]> ReadBackAsync(ContentStore store, string requestPath) =>
        ReadBackAsync(store, requestPath, ContentCaller.Anonymous("ftp"));

    private static async Task<byte[]> ReadBackAsync(ContentStore store, string requestPath, ContentCaller caller)
    {
        ContentPathMapping mapping = store.MapRequestPathForCaller(requestPath, caller);
        var destination = new MemoryStream();
        await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(store.GetFileStatus(mapping)!.Length), destination, CancellationToken.None);
        return destination.ToArray();
    }

    private static string[] Names(ContentStore store, string requestPath) =>
        [.. store.ListDirectoryWhateverTheListingSwitchSays(store.MapRequestPath(requestPath), CancellationToken.None).Entries.Select(entry => entry.Name)];
}
