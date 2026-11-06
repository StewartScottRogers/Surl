using Surl.Protocol.Abstractions;

namespace Surl.Content;

[TestClass]
public sealed class ObjectMetadataIndexTests
{
    private const string HelloETag = "\"5d41402abc4b2a76b9719d911017c592\"";

    private static readonly DateTimeOffset Start = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    private static readonly string Root = InMemoryContentFileSystem.RootPath;

    private static readonly string IndexPath = Path.Join(Root, ".surl", "objects", "index");

    private static readonly long StartGeneration = (Start.UtcTicks - DateTime.UnixEpoch.Ticks) / 10;

    [TestMethod]
    public async Task StoreAsync_StoredOrNoSuchFile_PublishesOneMetadataStoredChangeForTheStoredKeyOnly()
    {
        var fixture = new Fixture(persists: false);
        await fixture.UploadAsync("b/x.txt", "hello"u8.ToArray());
        var events = new RecordingSurlEventPublisher();
        var index = new ObjectMetadataIndex(fixture.FileSystem, Root, persists: false, events: events);

        await index.StoreAsync("b/x.txt", new ObjectMetadata(), CancellationToken.None);
        await index.StoreAsync("b/missing.txt", new ObjectMetadata(), CancellationToken.None);

        var expected = new StoreChangedEvent(StoreKind.Content, StoreChange.MetadataStored, "/b/x.txt", null, null);
        CollectionAssert.AreEqual(new SurlEvent[] { expected }, events.Events.ToArray());
    }

    [TestMethod]
    public async Task StoreAsync_ThenReadAsync_ReadsBackTheStoredMetadata()
    {
        var fixture = new Fixture(persists: false);
        await fixture.UploadAsync("b/x.txt", "hello"u8.ToArray());
        var metadata = new ObjectMetadata
        {
            ContentType = "text/plain",
            ContentEncoding = "gzip",
            ContentDisposition = "attachment",
            CacheControl = "no-cache",
            ContentLanguage = "en",
            UserMetadata = new Dictionary<string, string> { ["z"] = "last", ["a"] = "first" },
        };

        var stored = await fixture.Index.StoreAsync("b/x.txt", metadata, CancellationToken.None);
        var read = await fixture.Index.ReadAsync("b/x.txt", CancellationToken.None);

        Assert.AreEqual(ObjectMetadataStoreOutcome.Stored, stored.Outcome);
        Assert.IsNull(stored.StorageFailure);
        Assert.AreEqual(stored.Properties, read.Properties);
        var properties = read.Properties!;
        Assert.AreEqual("text/plain", properties.ContentType);
        Assert.AreEqual("gzip", properties.ContentEncoding);
        Assert.AreEqual("attachment", properties.ContentDisposition);
        Assert.AreEqual("no-cache", properties.CacheControl);
        Assert.AreEqual("en", properties.ContentLanguage);
        CollectionAssert.AreEqual(new[] { "a", "z" }, properties.UserMetadata.Select(pair => pair.Key).ToArray());
        Assert.AreEqual("first", properties.UserMetadata[0].Value);
        Assert.AreEqual(HelloETag, properties.ETag);
        Assert.AreEqual(StartGeneration, properties.Generation);
        Assert.AreEqual(5, properties.Length);
        Assert.AreEqual(Start, properties.LastWriteTimeUtc);
        Assert.IsNull(read.StorageFailure);
        Assert.IsNull(fixture.Index.IndexPath);
    }

    [TestMethod]
    public async Task StoreAsync_ETagAndGenerationGiven_KeepsThemAndEmptyFieldsAreAbsent()
    {
        var fixture = new Fixture(persists: false);
        await fixture.UploadAsync("big.bin", [1, 2, 3]);

        var stored = await fixture.Index.StoreAsync(
            "big.bin",
            new ObjectMetadata { ETag = "\"abc-2\"", Generation = 42, ContentType = string.Empty },
            CancellationToken.None);

        var properties = stored.Properties!;
        Assert.AreEqual("\"abc-2\"", properties.ETag);
        Assert.AreEqual(42, properties.Generation);
        Assert.AreEqual(ObjectMetadataIndex.DefaultContentType, properties.ContentType);
        Assert.IsEmpty(properties.UserMetadata);
    }

    [TestMethod]
    public async Task ReadAsync_FileWithNoStoredMetadata_AnswersTheDefaultsAndCachesTheETag()
    {
        var fixture = new Fixture(persists: false);
        await fixture.UploadAsync("ftp.txt", "hello"u8.ToArray());

        var first = await fixture.Index.ReadAsync("ftp.txt", CancellationToken.None);
        var second = await fixture.Index.ReadAsync("ftp.txt", CancellationToken.None);

        var properties = first.Properties!;
        Assert.AreEqual("application/octet-stream", properties.ContentType);
        Assert.IsNull(properties.ContentEncoding);
        Assert.IsNull(properties.ContentDisposition);
        Assert.IsNull(properties.CacheControl);
        Assert.IsNull(properties.ContentLanguage);
        Assert.IsEmpty(properties.UserMetadata);
        Assert.AreEqual(HelloETag, properties.ETag);
        Assert.AreEqual(StartGeneration, properties.Generation);
        Assert.AreEqual(1, fixture.Index.Count);
        Assert.AreEqual(properties, second.Properties);
    }

    [TestMethod]
    public async Task ReadAsync_NoFileAtTheKey_ReadsNothing()
    {
        var fixture = new Fixture(persists: true);

        var reading = await fixture.Index.ReadAsync("missing.txt", CancellationToken.None);

        Assert.IsNull(reading.Properties);
        Assert.IsNull(reading.StorageFailure);
        Assert.AreEqual(ContentEntryKind.None, fixture.FileSystem.GetEntryKind(IndexPath));
    }

    [TestMethod]
    public async Task ReadAsync_FileOverwrittenThroughTheStore_AnswersTheDefaultsForTheNewBytes()
    {
        var fixture = new Fixture(persists: false);
        await fixture.UploadAsync("x.txt", "old"u8.ToArray());
        await fixture.Index.StoreAsync("x.txt", new ObjectMetadata { ContentType = "text/plain" }, CancellationToken.None);
        fixture.Time.Now = Start.AddSeconds(1);
        await fixture.UploadAsync("x.txt", "hello"u8.ToArray());

        var reading = await fixture.Index.ReadAsync("x.txt", CancellationToken.None);

        Assert.AreEqual(ObjectMetadataIndex.DefaultContentType, reading.Properties!.ContentType);
        Assert.AreEqual(HelloETag, reading.Properties.ETag);
        Assert.AreEqual(Start.AddSeconds(1), reading.Properties.LastWriteTimeUtc);
    }

    [TestMethod]
    public async Task ReadAsync_FileRenamedThroughTheStore_LosesItsMetadataAndTheOldKeyIsDropped()
    {
        var fixture = new Fixture(persists: true);
        await fixture.UploadAsync("a.txt", "hello"u8.ToArray());
        await fixture.Index.StoreAsync("a.txt", new ObjectMetadata { ContentType = "text/plain" }, CancellationToken.None);

        var renamed = fixture.Store.RenameEntry(fixture.Store.MapRequestPath("/a.txt"), fixture.Store.MapRequestPath("/b.txt"));
        var oldKey = await fixture.Index.ReadAsync("a.txt", CancellationToken.None);
        var newKey = await fixture.Index.ReadAsync("b.txt", CancellationToken.None);

        Assert.AreEqual(ContentChangeResult.Done, renamed);
        Assert.IsNull(oldKey.Properties);
        Assert.AreEqual(ObjectMetadataIndex.DefaultContentType, newKey.Properties!.ContentType);
        var reloaded = await fixture.ReloadAsync();
        Assert.AreEqual(1, reloaded.Count);
    }

    [TestMethod]
    public async Task ReadAsync_FileDeletedThroughTheStore_ReadsNothingAndTheNextWriteDropsTheEntry()
    {
        var fixture = new Fixture(persists: true);
        await fixture.UploadAsync("gone.txt", "hello"u8.ToArray());
        await fixture.UploadAsync("kept.txt", "hello"u8.ToArray());
        await fixture.Index.StoreAsync("gone.txt", new ObjectMetadata(), CancellationToken.None);

        fixture.Store.DeleteFile(fixture.Store.MapRequestPath("/gone.txt"));
        var reading = await fixture.Index.ReadAsync("gone.txt", CancellationToken.None);
        await fixture.Index.StoreAsync("kept.txt", new ObjectMetadata(), CancellationToken.None);

        Assert.IsNull(reading.Properties);
        Assert.AreEqual(1, (await fixture.ReloadAsync()).Count);
    }

    [TestMethod]
    public async Task StoreAsync_PersistingIndex_WritesThePinnedBytes()
    {
        var fixture = new Fixture(persists: true);
        await fixture.UploadAsync("b/x.txt", "hello"u8.ToArray());

        var stored = await fixture.Index.StoreAsync(
            "b/x.txt",
            new ObjectMetadata { ContentType = "text/plain", Generation = 7, UserMetadata = new Dictionary<string, string> { ["k"] = "v" } },
            CancellationToken.None);

        byte[] expected =
        [
            .. "SURL-OBJECT-INDEX-1\n"u8,
            .. Convert.FromHexString("00000001" + "0007"),
            .. "b/x.txt"u8,
            .. Convert.FromHexString("0000000000000005" + "08df3a5a63755000" + "0000000000000007" + "0022"),
            .. "\"5d41402abc4b2a76b9719d911017c592\""u8,
            .. Convert.FromHexString("000a"),
            .. "text/plain"u8,
            .. Convert.FromHexString("0000" + "0000" + "0000" + "0000" + "0001" + "0001" + "6b" + "0001" + "76"),
        ];
        Assert.IsNull(stored.StorageFailure);
        Assert.AreEqual(IndexPath, fixture.Index.IndexPath);
        CollectionAssert.AreEqual(expected, await fixture.ReadFileAsync(IndexPath));
        Assert.IsFalse(fixture.FileSystem.EnumerateDirectoryEntryNames(Path.GetDirectoryName(IndexPath)!).Any(name => name.StartsWith(".index-", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task LoadAsync_IndexWrittenBefore_ReadsBackTheStoredMetadata()
    {
        var fixture = new Fixture(persists: true);
        await fixture.UploadAsync("x.txt", "hello"u8.ToArray());
        await fixture.Index.StoreAsync("x.txt", new ObjectMetadata { CacheControl = "max-age=60" }, CancellationToken.None);

        var reloaded = await fixture.ReloadAsync();
        var reading = await reloaded.ReadAsync("x.txt", CancellationToken.None);

        Assert.AreEqual("max-age=60", reading.Properties!.CacheControl);
        Assert.AreEqual(HelloETag, reading.Properties.ETag);
    }

    [TestMethod]
    public async Task LoadAsync_NoIndexFileOrInMemoryOnly_LoadsNothing()
    {
        var persisting = new Fixture(persists: true);
        var inMemory = new Fixture(persists: false);

        await persisting.Index.LoadAsync(CancellationToken.None);
        await inMemory.Index.LoadAsync(CancellationToken.None);

        Assert.AreEqual(0, persisting.Index.Count);
        Assert.AreEqual(0, inMemory.Index.Count);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("53555246", DisplayName = "truncated header")]
    [DataRow("5355524c2d4f424a4543542d494e4445582d320a00000000", DisplayName = "wrong header")]
    [DataRow("5355524c2d4f424a4543542d494e4445582d310a0000000000", DisplayName = "trailing byte")]
    [DataRow("5355524c2d4f424a4543542d494e4445582d310a00000003", DisplayName = "more entries than the bound")]
    [DataRow("5355524c2d4f424a4543542d494e4445582d310a00000001", DisplayName = "truncated entry")]
    public async Task LoadAsync_MalformedIndex_ThrowsWithThePathAndTheReason(string hex)
    {
        await AssertMalformedAsync(Convert.FromHexString(hex));
    }

    [TestMethod]
    [DataRow("0001", "61", "0000000000000005", "0000000000000000", "0001", "41", "0000", DisplayName = "repeated key")]
    [DataRow("0005", "2e7375726c", "0000000000000005", "0000000000000000", "0001", "41", "0000", DisplayName = ".surl key")]
    [DataRow("0003", "612f2f", "0000000000000005", "0000000000000000", "0001", "41", "0000", DisplayName = "empty segment")]
    [DataRow("0001", "ff", "0000000000000005", "0000000000000000", "0001", "41", "0000", DisplayName = "key not UTF-8")]
    [DataRow("0001", "62", "ffffffffffffffff", "0000000000000000", "0001", "41", "0000", DisplayName = "negative length")]
    [DataRow("0001", "62", "0000000000000005", "ffffffffffffffff", "0001", "41", "0000", DisplayName = "negative ticks")]
    [DataRow("0001", "62", "0000000000000005", "7fffffffffffffff", "0001", "41", "0000", DisplayName = "ticks past DateTime")]
    [DataRow("0001", "62", "0000000000000005", "0000000000000000", "0000", "", "0000", DisplayName = "empty ETag")]
    [DataRow("0001", "62", "0000000000000005", "0000000000000000", "0001", "41", "0002" + "0001" + "62" + "0000" + "0001" + "61" + "0000", DisplayName = "names not ascending")]
    [DataRow("0001", "62", "0000000000000005", "0000000000000000", "0001", "41", "0001" + "0005" + "6162636465" + "0000", DisplayName = "metadata past the bound")]
    public async Task LoadAsync_MalformedSecondEntry_ThrowsWithThePathAndTheReason(
        string keyLength, string key, string length, string ticks, string eTagLength, string eTag, string userMetadata)
    {
        string first = "0001" + "61" + "0000000000000005" + "0000000000000000" + "0000000000000000" + "0001" + "41" + "00000000000000000000" + "0000";
        string second = keyLength + key + length + ticks + "0000000000000000" + eTagLength + eTag + "00000000000000000000" + userMetadata;

        await AssertMalformedAsync(Convert.FromHexString("5355524c2d4f424a4543542d494e4445582d310a" + "00000002" + first + second));
    }

    [TestMethod]
    public async Task LoadAsync_IndexCannotBeRead_ThrowsWithThePathAndTheExceptionMessage()
    {
        var fixture = new Fixture(persists: true);
        await fixture.WriteFileAsync(IndexPath, "SURL-OBJECT-INDEX-1\n\0\0\0\0"u8.ToArray());
        fixture.FileSystem.OpenForReadFailure = new IOException("The read failed.");

        var exception = await Assert.ThrowsExactlyAsync<ObjectMetadataIndexLoadException>(() => fixture.Index.LoadAsync(CancellationToken.None));

        Assert.AreEqual(IndexPath, exception.FilePath);
        Assert.AreEqual("The read failed.", exception.Message);
        Assert.IsInstanceOfType<IOException>(exception.InnerException);
    }

    [TestMethod]
    public async Task LoadAsync_ReadThrowsSomethingOtherThanAStorageFailure_LetsItThrough()
    {
        var fixture = new Fixture(persists: true);
        await fixture.WriteFileAsync(IndexPath, "SURL-OBJECT-INDEX-1\n\0\0\0\0"u8.ToArray());
        fixture.FileSystem.OpenForReadFailure = new NotSupportedException("Not here.");

        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => fixture.Index.LoadAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task StoreAsync_MetadataPastTheBound_IsMetadataTooLargeAndStoresNothing()
    {
        var fixture = new Fixture(persists: true, maxMetadataBytes: 4);
        await fixture.UploadAsync("x.txt", "hello"u8.ToArray());

        var exactly = await fixture.Index.StoreAsync("x.txt", new ObjectMetadata { UserMetadata = new Dictionary<string, string> { ["ab"] = "cd" } }, CancellationToken.None);
        var tooLarge = await fixture.Index.StoreAsync("x.txt", new ObjectMetadata { ContentType = "text/plain" }, CancellationToken.None);

        Assert.AreEqual(ObjectMetadataStoreOutcome.Stored, exactly.Outcome);
        Assert.AreEqual(ObjectMetadataStoreOutcome.MetadataTooLarge, tooLarge.Outcome);
        Assert.IsNull(tooLarge.Properties);
        Assert.AreEqual("cd", (await fixture.Index.ReadAsync("x.txt", CancellationToken.None)).Properties!.UserMetadata[0].Value);
    }

    [TestMethod]
    public async Task StoreAsync_IndexFullOfCurrentEntries_IsMetadataIndexFullButAKeptKeyIsReplaced()
    {
        var fixture = new Fixture(persists: false, maxEntries: 1);
        await fixture.UploadAsync("a.txt", "hello"u8.ToArray());
        await fixture.UploadAsync("b.txt", "hello"u8.ToArray());
        await fixture.Index.StoreAsync("a.txt", new ObjectMetadata(), CancellationToken.None);

        var full = await fixture.Index.StoreAsync("b.txt", new ObjectMetadata(), CancellationToken.None);
        var replaced = await fixture.Index.StoreAsync("a.txt", new ObjectMetadata { ContentLanguage = "fr" }, CancellationToken.None);
        var notCached = await fixture.Index.ReadAsync("b.txt", CancellationToken.None);

        Assert.AreEqual(ObjectMetadataStoreOutcome.MetadataIndexFull, full.Outcome);
        Assert.IsNull(full.Properties);
        Assert.AreEqual(ObjectMetadataStoreOutcome.Stored, replaced.Outcome);
        Assert.AreEqual(HelloETag, notCached.Properties!.ETag);
        Assert.AreEqual(1, fixture.Index.Count);
    }

    [TestMethod]
    public async Task StoreAsync_IndexFullOfStaleEntries_DropsThemAndStores()
    {
        var fixture = new Fixture(persists: false, maxEntries: 2);
        await fixture.UploadAsync("deleted.txt", "hello"u8.ToArray());
        await fixture.UploadAsync("overwritten.txt", "hello"u8.ToArray());
        await fixture.UploadAsync("new.txt", "hello"u8.ToArray());
        await fixture.Index.StoreAsync("deleted.txt", new ObjectMetadata(), CancellationToken.None);
        await fixture.Index.StoreAsync("overwritten.txt", new ObjectMetadata(), CancellationToken.None);
        fixture.Store.DeleteFile(fixture.Store.MapRequestPath("/deleted.txt"));
        await fixture.UploadAsync("overwritten.txt", "changed"u8.ToArray());

        var stored = await fixture.Index.StoreAsync("new.txt", new ObjectMetadata(), CancellationToken.None);

        Assert.AreEqual(ObjectMetadataStoreOutcome.Stored, stored.Outcome);
        Assert.AreEqual(1, fixture.Index.Count);
    }

    [TestMethod]
    public async Task StoreAsync_NoFileAtTheKey_IsNoSuchFile()
    {
        var fixture = new Fixture(persists: false);

        var result = await fixture.Index.StoreAsync("missing.txt", new ObjectMetadata(), CancellationToken.None);

        Assert.AreEqual(ObjectMetadataStoreOutcome.NoSuchFile, result.Outcome);
        Assert.IsNull(result.Properties);
        Assert.AreEqual(0, fixture.Index.Count);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "temporary file deleted")]
    [DataRow(true, DisplayName = "temporary file left behind")]
    public async Task StoreAsync_IndexCannotBeWritten_AnswersTheFailureAndKeepsTheChange(bool deleteFails)
    {
        var fixture = new Fixture(persists: true);
        await fixture.UploadAsync("x.txt", "hello"u8.ToArray());
        fixture.FileSystem.FailCreateForWrite = true;
        fixture.FileSystem.FailDelete = deleteFails;

        var stored = await fixture.Index.StoreAsync("x.txt", new ObjectMetadata { ContentType = "text/plain" }, CancellationToken.None);
        var cached = await fixture.Index.ReadAsync("x.txt", CancellationToken.None);

        Assert.AreEqual(ObjectMetadataStoreOutcome.Stored, stored.Outcome);
        Assert.AreEqual("The write failed.", stored.StorageFailure);
        Assert.AreEqual("text/plain", cached.Properties!.ContentType);
        Assert.AreEqual(fixture.FileSystem.CreatedFiles[^1], fixture.FileSystem.DeletedFiles[^1]);
    }

    [TestMethod]
    public async Task ReadAsync_IndexCannotBeWrittenWhenCaching_AnswersTheDefaultsAndTheFailure()
    {
        var fixture = new Fixture(persists: true);
        await fixture.UploadAsync("x.txt", "hello"u8.ToArray());
        fixture.FileSystem.FailCreateForWrite = true;

        var reading = await fixture.Index.ReadAsync("x.txt", CancellationToken.None);

        Assert.AreEqual(HelloETag, reading.Properties!.ETag);
        Assert.AreEqual("The write failed.", reading.StorageFailure);
    }

    [TestMethod]
    public async Task ContentStore_PersistedIndex_IsNeverListedOrServed()
    {
        var fixture = new Fixture(persists: true);
        await fixture.UploadAsync("x.txt", "hello"u8.ToArray());
        await fixture.Index.StoreAsync("x.txt", new ObjectMetadata(), CancellationToken.None);

        var listing = fixture.Store.ListDirectory(fixture.Store.MapRequestPath("/"), CancellationToken.None);
        var folder = fixture.Store.MapRequestPath("/.surl/objects/");
        var index = fixture.Store.MapRequestPath("/.surl/objects/index");

        Assert.AreEqual(ContentEntryKind.File, fixture.FileSystem.GetEntryKind(IndexPath));
        CollectionAssert.AreEqual(new[] { "x.txt" }, listing.Entries.Select(entry => entry.Name).ToArray());
        Assert.AreEqual(ContentEntryKind.None, fixture.Store.GetEntryKind(folder));
        Assert.AreEqual(ContentEntryKind.None, fixture.Store.GetEntryKind(index));
        Assert.IsNull(fixture.Store.GetFileStatus(index));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("/a")]
    [DataRow("a/")]
    [DataRow("a//b")]
    [DataRow("../a")]
    [DataRow("a/./b")]
    [DataRow("a\\b")]
    [DataRow("c:")]
    [DataRow("CON")]
    [DataRow(".surl/objects/index")]
    [DataRow(".SURL")]
    public async Task StoreAsyncAndReadAsync_InvalidKey_Throw(string key)
    {
        var index = new Fixture(persists: false).Index;

        Assert.IsFalse(ObjectMetadataIndex.IsValidKey(key));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => index.StoreAsync(key, new ObjectMetadata(), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => index.ReadAsync(key, CancellationToken.None));
    }

    [TestMethod]
    public void IsValidKey_KeyPast65535Bytes_IsFalseAndSubSurlIsTrue()
    {
        Assert.IsFalse(ObjectMetadataIndex.IsValidKey(new string('a', ushort.MaxValue + 1)));
        Assert.IsTrue(ObjectMetadataIndex.IsValidKey(new string('a', ushort.MaxValue)));
        Assert.IsTrue(ObjectMetadataIndex.IsValidKey("sub/.surl"));
        Assert.IsTrue(ObjectMetadataIndex.IsValidKey(".surlx"));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(ObjectMetadataIndex.MaxETagBytes + 1)]
    public async Task StoreAsync_InvalidETag_Throws(int eTagLength)
    {
        var fixture = new Fixture(persists: false);
        await fixture.UploadAsync("x.txt", [1]);

        Assert.IsFalse(ObjectMetadataIndex.IsValidETag(new string('e', eTagLength)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => fixture.Index.StoreAsync("x.txt", new ObjectMetadata { ETag = new string('e', eTagLength) }, CancellationToken.None));
        Assert.IsTrue(ObjectMetadataIndex.IsValidETag(new string('e', ObjectMetadataIndex.MaxETagBytes)));
    }

    [TestMethod]
    public void Constructor_BoundsOutOfRange_Throw()
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ObjectMetadataIndex(fileSystem, Root, persists: false, maxEntries: -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ObjectMetadataIndex(fileSystem, Root, persists: false, maxMetadataBytes: -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ObjectMetadataIndex(fileSystem, Root, persists: false, maxMetadataBytes: ushort.MaxValue + 1));
        Assert.ThrowsExactly<ArgumentException>(() => new ObjectMetadataIndex(fileSystem, string.Empty, persists: false));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ObjectMetadataIndex(null!, Root, persists: false));
        var index = new ObjectMetadataIndex(fileSystem, Root, persists: false);
        Assert.AreEqual(ObjectMetadataIndex.DefaultMaxEntries, index.MaxEntries);
        Assert.AreEqual(ObjectMetadataIndex.DefaultMaxMetadataBytes, index.MaxMetadataBytes);
    }

    [TestMethod]
    public void LoadException_WithoutInnerException_CarriesThePathAndReason()
    {
        var exception = new ObjectMetadataIndexLoadException("/i", "why");

        Assert.AreEqual("/i", exception.FilePath);
        Assert.AreEqual("why", exception.Message);
        Assert.IsNull(exception.InnerException);
    }

    private static async Task AssertMalformedAsync(byte[] indexBytes)
    {
        var fixture = new Fixture(persists: true, maxEntries: 2, maxMetadataBytes: 4);
        await fixture.WriteFileAsync(IndexPath, indexBytes);

        var exception = await Assert.ThrowsExactlyAsync<ObjectMetadataIndexLoadException>(() => fixture.Index.LoadAsync(CancellationToken.None));

        Assert.AreEqual(IndexPath, exception.FilePath);
        Assert.AreEqual("not an object metadata index", exception.Message);
        Assert.AreEqual(ObjectMetadataIndex.MalformedIndexReason, exception.Message);
    }

    /// <summary>
    /// A content store over an in-memory file system at a settable time, uploads allowed, and an
    /// object metadata index beside it.
    /// </summary>
    private sealed class Fixture
    {
        private readonly int maxEntries;
        private readonly int maxMetadataBytes;
        private readonly bool persists;

        public Fixture(bool persists, int maxEntries = ObjectMetadataIndex.DefaultMaxEntries, int maxMetadataBytes = ObjectMetadataIndex.DefaultMaxMetadataBytes)
        {
            this.persists = persists;
            this.maxEntries = maxEntries;
            this.maxMetadataBytes = maxMetadataBytes;
            Time = new SettableTimeProvider(Start);
            FileSystem = new UnitTestFaultInjectingContentFileSystem(new InMemoryContentFileSystem(Time));
            Store = new ContentStore(Root, FileSystem, ContentExposureOptions.ServeEverythingInsideTheRoot with { AllowUploads = true });
            Index = new ObjectMetadataIndex(FileSystem, Root, persists, maxEntries, maxMetadataBytes);
        }

        public SettableTimeProvider Time { get; }

        public UnitTestFaultInjectingContentFileSystem FileSystem { get; }

        public ContentStore Store { get; }

        public ObjectMetadataIndex Index { get; }

        public async Task UploadAsync(string key, byte[] bytes)
        {
            string? folder = Path.GetDirectoryName(Path.Join([Root, .. key.Split('/')]));
            FileSystem.CreateDirectory(folder!);
            var result = await Store.WriteUploadAsync(Store.MapRequestPath("/" + key), new MemoryStream(bytes), CancellationToken.None);
            Assert.AreEqual(ContentUploadResult.Written, result);
        }

        public async Task WriteFileAsync(string path, byte[] bytes)
        {
            FileSystem.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var stream = FileSystem.CreateFileForAsyncWrite(path);
            await stream.WriteAsync(bytes);
        }

        public async Task<byte[]> ReadFileAsync(string path)
        {
            await using var stream = FileSystem.OpenFileForAsyncRead(path);
            using var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes);
            return bytes.ToArray();
        }

        public async Task<ObjectMetadataIndex> ReloadAsync()
        {
            var reloaded = new ObjectMetadataIndex(FileSystem, Root, persists, maxEntries, maxMetadataBytes);
            await reloaded.LoadAsync(CancellationToken.None);
            return reloaded;
        }
    }
}
