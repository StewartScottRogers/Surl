using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

[TestClass]
public sealed class MqttRetainedMessageFileTests
{
    private static readonly string StateFolder = Path.Join(InMemoryContentFileSystem.RootPath, ".surl", "mqtt");
    private static readonly string FilePath = Path.Join(StateFolder, MqttRetainedMessageFile.FileName);
    private static readonly byte[] PublishAcknowledged = [.. MqttTestExchange.ConnackAccepted, 0x40, 0x02, 0x00, 0x01];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullFileSystem_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MqttRetainedMessageFile(null!, StateFolder));
    }

    [TestMethod]
    public void Constructor_EmptyStateFolder_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MqttRetainedMessageFile(NewFileSystem(), ""));
    }

    [TestMethod]
    public void Constructor_NamesTheFileRetainedMessagesInTheStateFolder()
    {
        var file = new MqttRetainedMessageFile(NewFileSystem(), StateFolder);

        Assert.AreEqual(StateFolder, file.StateFolderPath);
        Assert.AreEqual(FilePath, file.FilePath);
    }

    [TestMethod]
    public async Task LoadAsync_NullFile_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => MqttRetainedMessages.LoadAsync(null!, cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadAsync_MissingFile_IsAnEmptyStoreAndWritesNothing()
    {
        var fileSystem = NewFileSystem();

        var retained = await LoadAsync(fileSystem);

        Assert.IsEmpty(retained.MatchingAny(["#"]));
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(StateFolder));
    }

    [TestMethod]
    public async Task RetainedMessage_WrittenByOneStore_IsLoadedByASecondStoreOverTheSameFileSystem()
    {
        var fileSystem = NewFileSystem();
        var first = await LoadAsync(fileSystem);
        first.Retain("a/b", "hi"u8);
        first.Retain("t", "there"u8);
        await first.SaveChangesAsync(TestContext.CancellationToken);

        var second = await LoadAsync(fileSystem);

        var loaded = second.MatchingAny(["#"]);
        CollectionAssert.AreEqual(new[] { "a/b", "t" }, loaded.Select(message => message.Key).ToArray());
        Assert.AreEqual("hi", Encoding.ASCII.GetString(loaded[0].Value));
        Assert.AreEqual("there", Encoding.ASCII.GetString(loaded[1].Value));
    }

    [TestMethod]
    public async Task LoadedStore_KeepsItsBoundsCountingTheLoadedMessages()
    {
        var fileSystem = NewFileSystem();
        var first = await LoadAsync(fileSystem, maxTopics: 1);
        first.Retain("t", "hi"u8);
        await first.SaveChangesAsync(TestContext.CancellationToken);

        var second = await LoadAsync(fileSystem, maxTopics: 1, maxTotalPayloadBytes: 3);

        Assert.IsFalse(second.Retain("u", "x"u8));
        Assert.IsFalse(second.Retain("t", "xyzw"u8));
        Assert.IsTrue(second.Retain("t", "xyz"u8));
    }

    [TestMethod]
    public async Task SaveChangesAsync_EmptyPayload_RemovesTheTopicFromTheFileToo()
    {
        var fileSystem = NewFileSystem();
        var retained = await LoadAsync(fileSystem);
        retained.Retain("a", "1"u8);
        retained.Retain("b", "2"u8);
        await retained.SaveChangesAsync(TestContext.CancellationToken);

        retained.Retain("a", ReadOnlySpan<byte>.Empty);
        await retained.SaveChangesAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(MqttRetainedMessageFile.Encode([new("b", "2"u8.ToArray())]), await ReadFileAsync(fileSystem));
        Assert.AreEqual("b", (await LoadAsync(fileSystem)).MatchingAny(["#"]).Single().Key);
    }

    [TestMethod]
    public async Task SaveChangesAsync_MessageRefusedByTheBounds_IsNotWritten()
    {
        var fileSystem = NewFileSystem();
        var retained = await LoadAsync(fileSystem, maxTopics: 1);

        Assert.IsFalse(retained.Retain("t", new byte[MqttRetainedMessages.DefaultMaxTotalPayloadBytes + 1]));
        await retained.SaveChangesAsync(TestContext.CancellationToken);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(FilePath));

        retained.Retain("t", "kept"u8);
        await retained.SaveChangesAsync(TestContext.CancellationToken);
        Assert.IsFalse(retained.Retain("u", "refused"u8));
        await retained.SaveChangesAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(MqttRetainedMessageFile.Encode([new("t", "kept"u8.ToArray())]), await ReadFileAsync(fileSystem));
    }

    [TestMethod]
    public async Task SaveChangesAsync_NothingChangedSinceTheLastWrite_WritesNothing()
    {
        var fileSystem = NewFileSystem();
        var retained = await LoadAsync(fileSystem);
        retained.Retain("t", "hi"u8);
        await retained.SaveChangesAsync(TestContext.CancellationToken);
        fileSystem.DeleteFile(FilePath);

        retained.Retain("unknown", ReadOnlySpan<byte>.Empty);
        await retained.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(FilePath));
    }

    [TestMethod]
    public async Task SaveChangesAsync_StoreWithoutAFile_DoesNothing()
    {
        var retained = new MqttRetainedMessages();
        retained.Retain("t", "hi"u8);

        await retained.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreEqual("t", retained.MatchingAny(["t"]).Single().Key);
    }

    [TestMethod]
    public async Task SaveChangesAsync_StateFolderMissing_CreatesIt()
    {
        var fileSystem = NewFileSystem();
        var retained = await LoadAsync(fileSystem);
        retained.Retain("t", "hi"u8);

        await retained.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreEqual(ContentEntryKind.Directory, fileSystem.GetEntryKind(StateFolder));
        Assert.AreEqual(ContentEntryKind.File, fileSystem.GetEntryKind(FilePath));
    }

    [TestMethod]
    public async Task SaveChangesAsync_WritesThroughATemporaryNameRenamedIntoPlace()
    {
        var fileSystem = NewFileSystem();
        var retained = await LoadAsync(fileSystem);
        retained.Retain("t", "hi"u8);

        await retained.SaveChangesAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { MqttRetainedMessageFile.FileName }, fileSystem.EnumerateDirectoryEntryNames(StateFolder).ToArray());
    }

    [TestMethod]
    public async Task SaveChangesAsync_FailureMidWrite_LeavesTheFileAsItWasAndNoTemporaryFile()
    {
        var header = MqttRetainedMessageFile.Header.Length;
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System, maxTotalBytes: (2 * header) + 20);
        var retained = await LoadAsync(fileSystem);
        retained.Retain("t", "hi"u8);
        await retained.SaveChangesAsync(TestContext.CancellationToken);
        var before = await ReadFileAsync(fileSystem);

        retained.Retain("t", new byte[100]);
        await Assert.ThrowsExactlyAsync<IOException>(() => retained.SaveChangesAsync(TestContext.CancellationToken));

        CollectionAssert.AreEqual(before, await ReadFileAsync(fileSystem));
        CollectionAssert.AreEqual(new[] { MqttRetainedMessageFile.FileName }, fileSystem.EnumerateDirectoryEntryNames(StateFolder).ToArray());
    }

    [TestMethod]
    public async Task SaveChangesAsync_AfterAFailedWrite_TheNextSaveWritesTheChange()
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System, maxTotalBytes: 60);
        var retained = await LoadAsync(fileSystem);
        retained.Retain("t", new byte[100]);
        await Assert.ThrowsExactlyAsync<IOException>(() => retained.SaveChangesAsync(TestContext.CancellationToken));

        retained.Retain("t", "hi"u8);
        await retained.SaveChangesAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(MqttRetainedMessageFile.Encode([new("t", "hi"u8.ToArray())]), await ReadFileAsync(fileSystem));
    }

    [TestMethod]
    public async Task SaveChangesAsync_ConcurrentPublishes_LeaveTheFileHoldingTheStoresLastState()
    {
        var fileSystem = NewFileSystem();
        var retained = await LoadAsync(fileSystem);

        await Parallel.ForAsync(0, 200, TestContext.CancellationToken, async (index, cancellationToken) =>
        {
            retained.Retain($"t/{index % 7}", Encoding.ASCII.GetBytes(index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            await retained.SaveChangesAsync(cancellationToken);
        });

        CollectionAssert.AreEqual(MqttRetainedMessageFile.Encode(retained.MatchingAny(["#"])), await ReadFileAsync(fileSystem));
    }

    [TestMethod]
    public async Task Bytes_ForTwoTopics_AreTheFormatAdr0031Specifies()
    {
        var fileSystem = NewFileSystem();
        var retained = await LoadAsync(fileSystem);
        retained.Retain("t", [0x00, 0x01, 0x02]);
        retained.Retain("a/é", "hi"u8);

        await retained.SaveChangesAsync(TestContext.CancellationToken);

        byte[] expected =
        [
            .. "SURL-MQTT-RETAINED-1\n"u8,
            0x00, 0x04, (byte)'a', (byte)'/', 0xC3, 0xA9,
            0x00, 0x00, 0x00, 0x02, (byte)'h', (byte)'i',
            0x00, 0x01, (byte)'t',
            0x00, 0x00, 0x00, 0x03, 0x00, 0x01, 0x02,
        ];
        CollectionAssert.AreEqual(expected, await ReadFileAsync(fileSystem));
    }

    [TestMethod]
    public async Task Bytes_ForAnEmptyStore_AreTheHeaderAlone()
    {
        var fileSystem = NewFileSystem();
        var retained = await LoadAsync(fileSystem);
        retained.Retain("t", "hi"u8);
        retained.Retain("t", ReadOnlySpan<byte>.Empty);

        await retained.SaveChangesAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual("SURL-MQTT-RETAINED-1\n"u8.ToArray(), await ReadFileAsync(fileSystem));
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty file")]
    [DataRow("53 55 52 4C 2D 4D 51 54 54 2D 52 45 54 41 49 4E 45 44 2D 32 0A", DisplayName = "wrong header")]
    [DataRow("|00", DisplayName = "truncated topic length")]
    [DataRow("|00 02 74", DisplayName = "truncated topic")]
    [DataRow("|00 01 74 00 00", DisplayName = "truncated payload length")]
    [DataRow("|00 01 74 00 00 00 02 68", DisplayName = "truncated payload")]
    [DataRow("|00 00 00 00 00 01 68", DisplayName = "empty topic")]
    [DataRow("|00 01 23 00 00 00 01 68", DisplayName = "invalid topic name")]
    [DataRow("|00 01 FF 00 00 00 01 68", DisplayName = "topic not valid UTF-8")]
    [DataRow("|00 01 74 00 00 00 01 68 00 01 74 00 00 00 01 69", DisplayName = "repeated topic")]
    [DataRow("|00 01 74 00 00 00 00", DisplayName = "empty payload")]
    [DataRow("|00 01 74 FF FF FF FF 68", DisplayName = "payload length past what a file can hold")]
    public async Task LoadAsync_MalformedFile_ThrowsNotARetainedMessageFile(string hex)
    {
        var fileSystem = NewFileSystem();
        await WriteFileAsync(fileSystem, FromHex(hex));

        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => LoadAsync(fileSystem));

        Assert.AreEqual("not a retained-message file", exception.Message);
    }

    [TestMethod]
    public async Task LoadAsync_TrailingBytesAfterTheLastRecord_ThrowsNotARetainedMessageFile()
    {
        var fileSystem = NewFileSystem();
        await WriteFileAsync(fileSystem, [.. MqttRetainedMessageFile.Encode([new("t", "hi"u8.ToArray())]), 0x00]);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => LoadAsync(fileSystem));
    }

    [TestMethod]
    public async Task LoadAsync_FileWithMoreTopicsThanTheBound_LoadsNothing()
    {
        var fileSystem = NewFileSystem();
        await WriteFileAsync(fileSystem, MqttRetainedMessageFile.Encode([new("a", [1]), new("b", [2]), new("c", [3])]));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => LoadAsync(fileSystem, maxTopics: 2));
    }

    [TestMethod]
    public async Task LoadAsync_FileWithMorePayloadBytesThanTheBound_LoadsNothing()
    {
        var fileSystem = NewFileSystem();
        await WriteFileAsync(fileSystem, MqttRetainedMessageFile.Encode([new("a", [1, 2]), new("b", [3, 4])]));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => LoadAsync(fileSystem, maxTotalPayloadBytes: 3));
    }

    [TestMethod]
    public async Task LoadAsync_FileLongerThanAnyWithinTheBounds_IsRefusedBeforeItIsRead()
    {
        var fileSystem = NewFileSystem();
        var longest = MqttRetainedMessageFile.Header.Length + 2 + ushort.MaxValue + 4 + 1;
        await WriteFileAsync(fileSystem, [.. MqttRetainedMessageFile.Header, .. new byte[longest - MqttRetainedMessageFile.Header.Length + 1]]);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => LoadAsync(fileSystem, maxTopics: 1, maxTotalPayloadBytes: 1));
    }

    [TestMethod]
    public async Task LoadAsync_FileExactlyAtTheBounds_Loads()
    {
        var fileSystem = NewFileSystem();
        var topic = new string('t', ushort.MaxValue);
        await WriteFileAsync(fileSystem, MqttRetainedMessageFile.Encode([new(topic, [7])]));

        var retained = await LoadAsync(fileSystem, maxTopics: 1, maxTotalPayloadBytes: 1);

        Assert.AreEqual(topic, retained.MatchingAny(["#"]).Single().Key);
    }

    [TestMethod]
    public async Task LoadAsync_LeftoverTemporaryFiles_AreIgnored()
    {
        var fileSystem = NewFileSystem();
        await WriteFileAsync(fileSystem, MqttRetainedMessageFile.Encode([new("t", "hi"u8.ToArray())]));
        await using (var leftover = fileSystem.CreateFileForAsyncWrite(Path.Join(StateFolder, ".retained-messages-0123")))
        {
            await leftover.WriteAsync(new byte[] { 0xFF }, TestContext.CancellationToken);
        }

        var retained = await LoadAsync(fileSystem);

        Assert.AreEqual("t", retained.MatchingAny(["#"]).Single().Key);
    }

    [TestMethod]
    public async Task LoadAsync_UnreadableFile_ThrowsTheFileSystemsException()
    {
        var fileSystem = new UnitTestThrowingContentFileSystem(ContentEntryKind.File, new IOException("The device is not ready."));

        var exception = await Assert.ThrowsExactlyAsync<IOException>(() => MqttRetainedMessages.LoadAsync(
            new MqttRetainedMessageFile(fileSystem, StateFolder),
            cancellationToken: TestContext.CancellationToken));

        Assert.AreEqual("The device is not ready.", exception.Message);
    }

    [TestMethod]
    public async Task ServeAsync_PublishToAPersistedStore_WritesTheFile()
    {
        var fileSystem = NewFileSystem();
        var server = new MqttProtocolServer(await LoadAsync(fileSystem), new AnonymousAuthenticationPolicy());

        var (connection, _) = await ServePublishAsync(server);

        CollectionAssert.AreEqual(PublishAcknowledged, connection.WrittenBytes);
        CollectionAssert.AreEqual(MqttRetainedMessageFile.Encode([new("t", "hi"u8.ToArray())]), await ReadFileAsync(fileSystem));
    }

    [TestMethod]
    public async Task ServeAsync_WriteFailsWithIOException_NotesItKeepsTheChangeAndAcknowledges()
    {
        await AssertWriteFailureIsNotedAsync(new IOException("The disk is full."));
    }

    [TestMethod]
    public async Task ServeAsync_WriteFailsWithUnauthorizedAccess_NotesItKeepsTheChangeAndAcknowledges()
    {
        await AssertWriteFailureIsNotedAsync(new UnauthorizedAccessException("Access is denied."));
    }

    private static InMemoryContentFileSystem NewFileSystem() => new(TimeProvider.System);

    private static byte[] FromHex(string hex)
    {
        var header = hex.StartsWith('|') ? MqttRetainedMessageFile.Header.ToArray() : [];
        var digits = hex.TrimStart('|').Replace(" ", "", StringComparison.Ordinal);
        return [.. header, .. Convert.FromHexString(digits)];
    }

    private async Task AssertWriteFailureIsNotedAsync(Exception failure)
    {
        var fileSystem = new UnitTestThrowingContentFileSystem(ContentEntryKind.None, failure);
        var retained = await MqttRetainedMessages.LoadAsync(new MqttRetainedMessageFile(fileSystem, StateFolder), cancellationToken: TestContext.CancellationToken);

        var (connection, log) = await ServePublishAsync(new MqttProtocolServer(retained, new AnonymousAuthenticationPolicy()));

        CollectionAssert.AreEqual(PublishAcknowledged, connection.WrittenBytes);
        Assert.AreEqual("hi", Encoding.ASCII.GetString(retained.MatchingAny(["t"]).Single().Value));
        Assert.IsTrue(log.Notes.Any(note => note.Contains($"could not be written to their file ({failure.Message})", StringComparison.Ordinal)));
    }

    private async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServePublishAsync(MqttProtocolServer server)
    {
        var request = ClientPackets.Join(ClientPackets.CurlConnect(), ClientPackets.Publish(0x32, "t", 1, "hi"), ClientPackets.Disconnect);
        var connection = new InMemoryConnection([request]);
        var log = new RecordingExchangeLog();

        await server.ServeAsync(connection, MqttTestExchange.Context(TimeProvider.System, TestContext.CancellationToken, log: log));
        await connection.DisposeAsync();

        return (connection, log);
    }

    private Task<MqttRetainedMessages> LoadAsync(
        InMemoryContentFileSystem fileSystem,
        int maxTopics = MqttRetainedMessages.DefaultMaxTopics,
        long maxTotalPayloadBytes = MqttRetainedMessages.DefaultMaxTotalPayloadBytes) =>
        MqttRetainedMessages.LoadAsync(new MqttRetainedMessageFile(fileSystem, StateFolder), maxTopics, maxTotalPayloadBytes, TestContext.CancellationToken);

    private async Task<byte[]> ReadFileAsync(InMemoryContentFileSystem fileSystem)
    {
        var bytes = new MemoryStream();
        await using (var stream = fileSystem.OpenFileForAsyncRead(FilePath))
        {
            await stream.CopyToAsync(bytes, TestContext.CancellationToken);
        }

        return bytes.ToArray();
    }

    private async Task WriteFileAsync(InMemoryContentFileSystem fileSystem, byte[] bytes)
    {
        fileSystem.CreateDirectory(StateFolder);
        await using var stream = fileSystem.CreateFileForAsyncWrite(FilePath);
        await stream.WriteAsync(bytes, TestContext.CancellationToken);
    }
}
