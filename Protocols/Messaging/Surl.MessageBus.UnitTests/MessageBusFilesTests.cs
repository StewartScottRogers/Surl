using static Surl.MessageBus.PersistedBusFixture;

namespace Surl.MessageBus;

[TestClass]
public sealed class MessageBusFilesTests
{
    // The message "hi" on topic t, QoS 0, published at 2026-11-04T08:00:00Z, retained or not.
    private const string RetainedHi = "00000001 74 01 00 08df3a499fec8000 00000000 00000002 6869";
    private const string Hi = "00000001 74 00 00 08df3a499fec8000 00000000 00000002 6869";

    private readonly PersistedBusFixture fixture = new();

    [TestMethod]
    public void MessageBusFiles_UnderASurlFolder_KeepsEveryFileUnderItsBusFolder()
    {
        var files = fixture.Files;

        Assert.AreEqual(Path.Join(SurlFolderPath, "bus"), files.StateFolderPath);
        Assert.AreEqual(Path.Join(SurlFolderPath, "bus", "retained"), files.RetainedPath);
        Assert.AreEqual(Path.Join(SurlFolderPath, "bus", "queues", "index"), files.QueueIndexPath);
        Assert.AreEqual(Path.Join(SurlFolderPath, "bus", "queues", "messages", "00000000000000ff"), files.QueueMessageFilePath(255));
        Assert.AreEqual(Path.Join(SurlFolderPath, "bus", "logs", "000000000000000a"), files.LogFilePath(10));
        Assert.AreEqual(Path.Join(SurlFolderPath, "mqtt", "retained-messages"), files.MqttRetainedMessagesPath);
    }

    [TestMethod]
    public async Task SaveChangesAsync_CommittedOffsetsOfTwoGroups_WritesTheOffsetsFilesExactBytesInOrdinalOrder()
    {
        var bus = await fixture.LoadBusAsync();
        bus.CommitOffset(null, "g2", "a/b", 0x0102, 3, "m");
        bus.CommitOffset(null, "g1", "t", 5, -1, metadata: null);

        await bus.SaveChangesAsync();

        Assert.AreEqual(Path.Join(SurlFolderPath, "bus", "offsets"), fixture.Files.OffsetsPath);
        CollectionAssert.AreEqual(
            (byte[])[
                .. "SURL-BUS-OFFSETS-1\n"u8,
                .. Bytes("00000002"),
                .. Bytes("00000002 6731 00000001 74 0000000000000005 ffffffff 00 08df3a499fec8000"),
                .. Bytes("00000002 6732 00000003 612f62 0000000000000102 00000003 01 00000001 6d 08df3a499fec8000"),
            ],
            fixture.Read(fixture.Files.OffsetsPath));
    }

    [TestMethod]
    public void MessageBusFiles_NullOrEmptyArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MessageBusFiles(null!, SurlFolderPath));
        Assert.ThrowsExactly<ArgumentException>(() => new MessageBusFiles(fixture.FileSystem, string.Empty));
    }

    [TestMethod]
    public async Task SaveChangesAsync_ARetainedMessage_WritesTheRetainedFilesExactBytes()
    {
        var bus = await fixture.LoadBusAsync();
        bus.Publish(Message("t", "hi") with { Retain = true });

        await bus.SaveChangesAsync();

        CollectionAssert.AreEqual(
            (byte[])[.. "SURL-BUS-RETAINED-1\n"u8, .. Bytes(RetainedHi)],
            fixture.Read(fixture.Files.RetainedPath));
    }

    [TestMethod]
    public async Task SaveChangesAsync_ATopicsFirstPublish_WritesItsLogFilesExactBytes()
    {
        var bus = await fixture.LoadBusAsync();
        bus.Publish(Message("t", "hi"));

        await bus.SaveChangesAsync();

        CollectionAssert.AreEqual(
            (byte[])[.. "SURL-BUS-LOG-1\n"u8, .. Bytes("00000001 74 0000000000000000"), .. Bytes(Hi)],
            fixture.Read(fixture.Files.LogFilePath(0)));
    }

    [TestMethod]
    public async Task SaveChangesAsync_ADurableQueueHoldingAMessage_WritesTheIndexAndMessageFilesExactBytes()
    {
        var bus = await fixture.LoadBusAsync();
        var connection = bus.Connect();
        connection.DeclareQueue("q", new BusQueueFlags(Durable: true));
        connection.BindQueue("q", "t");
        bus.Publish(Message("t", "hi"));

        await bus.SaveChangesAsync();

        CollectionAssert.AreEqual(
            (byte[])[.. "SURL-BUS-QUEUES-1\n"u8, .. Bytes("00000001 00000001 71 00000001 00000001 74 00000001 0000000000000000 00")],
            fixture.Read(fixture.Files.QueueIndexPath));
        CollectionAssert.AreEqual(
            (byte[])[.. "SURL-BUS-MESSAGE-1\n"u8, .. Bytes(Hi)],
            fixture.Read(fixture.Files.QueueMessageFilePath(0)));
    }

    [TestMethod]
    public async Task SaveChangesAsync_ADurableQueueWithASubscriptionDescription_WritesTheSecondIndexFormAndLoadsItBack()
    {
        var bus = await fixture.LoadBusAsync();
        var connection = bus.Connect();
        connection.DeclareQueue("a", new BusQueueFlags(Durable: true), subscriptionDescription: "d");
        connection.DeclareQueue("b", new BusQueueFlags(Durable: true));

        await bus.SaveChangesAsync();
        var loaded = (await fixture.LoadBusAsync()).Connect();

        CollectionAssert.AreEqual(
            (byte[])[.. "SURL-BUS-QUEUES-2\n"u8, .. Bytes("00000002 00000001 61 01 00000001 64 00000000 00000000 00000001 62 00 00000000 00000000")],
            fixture.Read(fixture.Files.QueueIndexPath));
        Assert.AreEqual("d", loaded.InspectQueue("a").SubscriptionDescription);
        Assert.IsNull(loaded.InspectQueue("b").SubscriptionDescription);
    }

    [TestMethod]
    [DataRow("SURL-BUS-RETAINED-2\n", "", DisplayName = "Another header")]
    [DataRow("SURL-BUS-RETAINED-1\n", "00000001 2b 01 00 08df3a499fec8000 00000000 00000002 6869", DisplayName = "A wildcard topic")]
    [DataRow("SURL-BUS-RETAINED-1\n", Hi, DisplayName = "A message not marked retained")]
    [DataRow("SURL-BUS-RETAINED-1\n", "00000001 74 01 00 08df3a499fec8000 00000000 00000000", DisplayName = "An empty payload")]
    [DataRow("SURL-BUS-RETAINED-1\n", RetainedHi + RetainedHi, DisplayName = "A topic twice")]
    [DataRow("SURL-BUS-RETAINED-1\n", "00000001 74 01", DisplayName = "A message cut short")]
    public async Task LoadAsync_AMalformedRetainedFile_ThrowsWithItsPathAndNotAMessageBusFile(string header, string hex)
    {
        fixture.Write(fixture.Files.RetainedPath, [.. System.Text.Encoding.ASCII.GetBytes(header), .. Bytes(hex)]);

        var exception = await fixture.LoadFailureAsync();

        Assert.AreEqual(fixture.Files.RetainedPath, exception.FilePath);
        Assert.AreEqual(MessageBusFiles.MalformedFileReason, exception.Message);
    }

    [TestMethod]
    [DataRow("SURL-BUS-QUEUES-3\n00000000", DisplayName = "Another header")]
    [DataRow("SURL-BUS-QUEUES-2\n00000001 00000001 71 02 00000000 00000000", DisplayName = "A description byte that is neither 0 nor 1")]
    [DataRow("SURL-BUS-QUEUES-2\n00000001 00000001 71 01", DisplayName = "A description cut short")]
    [DataRow("SURL-BUS-QUEUES-1\n00000000 00", DisplayName = "Bytes after the last queue")]
    [DataRow("SURL-BUS-QUEUES-1\n00000001 00000000 00000000 00000000", DisplayName = "An empty queue name")]
    [DataRow("SURL-BUS-QUEUES-1\n00000002 00000001 71 00000000 00000000 00000001 71 00000000 00000000", DisplayName = "A queue twice")]
    [DataRow("SURL-BUS-QUEUES-1\n00000001 00000001 71 00000001 00000002 2323 00000000", DisplayName = "An invalid filter")]
    [DataRow("SURL-BUS-QUEUES-1\n00000001 00000001 71 00000000 00000002 0000000000000000 00 0000000000000000 00", DisplayName = "A message file twice")]
    [DataRow("SURL-BUS-QUEUES-1\n00000001 00000001 71 00000000 00000001 0000000000000000 04", DisplayName = "A marks byte with an unknown bit")]
    [DataRow("SURL-BUS-QUEUES-1\n00000001", DisplayName = "An index cut short")]
    public async Task LoadAsync_AMalformedQueueIndex_ThrowsWithItsPathAndNotAMessageBusFile(string headerAndHex)
    {
        var split = headerAndHex.IndexOf('\n', StringComparison.Ordinal) + 1;
        fixture.Write(fixture.Files.QueueIndexPath, [.. System.Text.Encoding.ASCII.GetBytes(headerAndHex[..split]), .. Bytes(headerAndHex[split..])]);
        fixture.Write(fixture.Files.QueueMessageFilePath(0), [.. MessageBusFiles.QueueMessageHeader, .. Bytes(Hi)]);

        var exception = await fixture.LoadFailureAsync();

        Assert.AreEqual(fixture.Files.QueueIndexPath, exception.FilePath);
        Assert.AreEqual(MessageBusFiles.MalformedFileReason, exception.Message);
    }

    [TestMethod]
    public async Task LoadAsync_AQueueMessageFileTheIndexNamesIsMissing_ThrowsWithItsPathAndMissingReason()
    {
        WriteIndexNamingMessageFileZero();

        var exception = await fixture.LoadFailureAsync();

        Assert.AreEqual(fixture.Files.QueueMessageFilePath(0), exception.FilePath);
        Assert.AreEqual(MessageBusFiles.MissingQueueMessageFileReason, exception.Message);
    }

    [TestMethod]
    [DataRow("SURL-BUS-MESSAGE-1\n" + "", DisplayName = "No message")]
    [DataRow("SURL-BUS-MESSAGE-2\n" + Hi, DisplayName = "Another header")]
    [DataRow("SURL-BUS-MESSAGE-1\n" + Hi + "00", DisplayName = "Bytes after the message")]
    public async Task LoadAsync_AMalformedQueueMessageFile_ThrowsWithItsPathAndNotAMessageBusFile(string headerAndHex)
    {
        WriteIndexNamingMessageFileZero();
        var split = headerAndHex.IndexOf('\n', StringComparison.Ordinal) + 1;
        fixture.Write(fixture.Files.QueueMessageFilePath(0), [.. System.Text.Encoding.ASCII.GetBytes(headerAndHex[..split]), .. Bytes(headerAndHex[split..])]);

        var exception = await fixture.LoadFailureAsync();

        Assert.AreEqual(fixture.Files.QueueMessageFilePath(0), exception.FilePath);
        Assert.AreEqual(MessageBusFiles.MalformedFileReason, exception.Message);
    }

    [TestMethod]
    [DataRow("SURL-BUS-LOG-2\n00000001 74 0000000000000000", DisplayName = "Another header")]
    [DataRow("SURL-BUS-LOG-1\n00000001 23 0000000000000000", DisplayName = "A wildcard topic")]
    [DataRow("SURL-BUS-LOG-1\n00000001 75 0000000000000000 " + Hi, DisplayName = "A record on another topic")]
    [DataRow("SURL-BUS-LOG-1\n00000001 74 8000000000000000", DisplayName = "A start offset past the largest 8-byte value")]
    [DataRow("SURL-BUS-LOG-1\n00000001 74 00000000", DisplayName = "A start offset cut short")]
    public async Task LoadAsync_AMalformedLogFile_ThrowsWithItsPathAndNotAMessageBusFile(string headerAndHex)
    {
        var split = headerAndHex.IndexOf('\n', StringComparison.Ordinal) + 1;
        fixture.Write(fixture.Files.LogFilePath(3), [.. System.Text.Encoding.ASCII.GetBytes(headerAndHex[..split]), .. Bytes(headerAndHex[split..])]);

        var exception = await fixture.LoadFailureAsync();

        Assert.AreEqual(fixture.Files.LogFilePath(3), exception.FilePath);
        Assert.AreEqual(MessageBusFiles.MalformedFileReason, exception.Message);
    }

    [TestMethod]
    public async Task LoadAsync_TwoLogFilesForOneTopic_ThrowsWithTheSecondFilesPath()
    {
        byte[] log = [.. MessageBusFiles.LogHeader, .. Bytes("00000001 74 0000000000000000")];
        fixture.Write(fixture.Files.LogFilePath(1), log);
        fixture.Write(fixture.Files.LogFilePath(2), log);

        var exception = await fixture.LoadFailureAsync();

        Assert.AreEqual(fixture.Files.LogFilePath(2), exception.FilePath);
        Assert.AreEqual(MessageBusFiles.MalformedFileReason, exception.Message);
    }

    [TestMethod]
    public async Task LoadAsync_EntriesInTheLogsFolderThatAreNotLogFiles_AreIgnored()
    {
        byte[] garbage = [1, 2, 3];
        fixture.Write(Path.Join(fixture.Files.LogsFolderPath, ".0000000000000000-0123"), garbage);
        fixture.Write(Path.Join(fixture.Files.LogsFolderPath, "000000000000000A"), garbage);
        fixture.Write(Path.Join(fixture.Files.LogsFolderPath, "notes"), garbage);
        fixture.FileSystem.Files.CreateDirectory(fixture.Files.LogFilePath(5));

        var bus = await fixture.LoadBusAsync();

        Assert.IsEmpty(bus.GetLogTopics());
    }

    [TestMethod]
    [DataRow("SURL-MQTT-RETAINED-2\n", "", DisplayName = "Another header")]
    [DataRow("SURL-MQTT-RETAINED-1\n", "0001 23 00000001 61", DisplayName = "A wildcard topic")]
    [DataRow("SURL-MQTT-RETAINED-1\n", "0001 74 00000000", DisplayName = "An empty payload")]
    [DataRow("SURL-MQTT-RETAINED-1\n", "0001 74 00000001 61 0001 74 00000001 62", DisplayName = "A topic twice")]
    [DataRow("SURL-MQTT-RETAINED-1\n", "0001 74 00000005 61", DisplayName = "A payload cut short")]
    public async Task LoadAsync_AMalformedMqttRetainedMessagesFile_ThrowsWithItsPathAndNotARetainedMessageFile(string header, string hex)
    {
        fixture.Write(fixture.Files.MqttRetainedMessagesPath, [.. System.Text.Encoding.ASCII.GetBytes(header), .. Bytes(hex)]);

        var exception = await fixture.LoadFailureAsync();

        Assert.AreEqual(fixture.Files.MqttRetainedMessagesPath, exception.FilePath);
        Assert.AreEqual(MessageBusFiles.MalformedMqttRetainedFileReason, exception.Message);
    }

    [TestMethod]
    public async Task LoadAsync_AFileThatCannotBeRead_ThrowsWithItsPathAndTheFailure()
    {
        fixture.Write(fixture.Files.RetainedPath, [.. MessageBusFiles.RetainedHeader]);
        fixture.FileSystem.FailReadsOf = "retained";
        fixture.FileSystem.Failure = new UnauthorizedAccessException("Access denied.");

        var exception = await fixture.LoadFailureAsync();

        Assert.AreEqual(fixture.Files.RetainedPath, exception.FilePath);
        Assert.AreEqual("Access denied.", exception.Message);
        Assert.IsInstanceOfType<UnauthorizedAccessException>(exception.InnerException);
    }

    [TestMethod]
    public async Task LoadAsync_ALogsFolderThatCannotBeListed_ThrowsWithItsPathAndTheFailure()
    {
        fixture.FileSystem.Files.CreateDirectory(fixture.Files.LogsFolderPath);
        fixture.FileSystem.FailListingsOf = "logs";

        var exception = await fixture.LoadFailureAsync();

        Assert.AreEqual(fixture.Files.LogsFolderPath, exception.FilePath);
        Assert.AreEqual("The disk failed.", exception.Message);
    }

    [TestMethod]
    public async Task LoadAsync_LogsHoldingMoreTopicsThanTheBound_ThrowsWithTheLogsFolder()
    {
        fixture.Write(fixture.Files.LogFilePath(0), [.. MessageBusFiles.LogHeader, .. Bytes("00000001 74 0000000000000000")]);
        fixture.Write(fixture.Files.LogFilePath(1), [.. MessageBusFiles.LogHeader, .. Bytes("00000001 75 0000000000000000")]);

        var exception = await fixture.LoadFailureAsync(fixture.NewBus(maxTopics: 1));

        Assert.AreEqual(fixture.Files.LogsFolderPath, exception.FilePath);
        Assert.AreEqual(MessageBusFiles.MalformedFileReason, exception.Message);
    }

    [TestMethod]
    public async Task LoadAsync_LogsHoldingMoreBytesThanTheBound_ThrowsWithTheLogsFolder()
    {
        fixture.Write(fixture.Files.LogFilePath(0), [.. MessageBusFiles.LogHeader, .. Bytes("00000001 74 0000000000000000"), .. Bytes(Hi)]);

        var exception = await fixture.LoadFailureAsync(fixture.NewBus(maxLogBytes: 17));

        Assert.AreEqual(fixture.Files.LogsFolderPath, exception.FilePath);
    }

    [TestMethod]
    public async Task LoadAsync_RetainedTopicsPastTheTopicBound_ThrowsWithTheRetainedFile()
    {
        fixture.Write(fixture.Files.LogFilePath(0), [.. MessageBusFiles.LogHeader, .. Bytes("00000001 75 0000000000000000")]);
        fixture.Write(fixture.Files.RetainedPath, [.. MessageBusFiles.RetainedHeader, .. Bytes(RetainedHi)]);

        var exception = await fixture.LoadFailureAsync(fixture.NewBus(maxTopics: 1));

        Assert.AreEqual(fixture.Files.RetainedPath, exception.FilePath);
        Assert.AreEqual(MessageBusFiles.MalformedFileReason, exception.Message);
    }

    [TestMethod]
    public async Task LoadAsync_AnMqttFileHoldingMoreRetainedBytesThanTheBound_ThrowsWithItsPathAndItsReason()
    {
        fixture.Write(fixture.Files.MqttRetainedMessagesPath, [.. "SURL-MQTT-RETAINED-1\n"u8, .. Bytes("0001 74 00000002 6869")]);

        var exception = await fixture.LoadFailureAsync(fixture.NewBus(maxRetainedPayloadBytes: 1));

        Assert.AreEqual(fixture.Files.MqttRetainedMessagesPath, exception.FilePath);
        Assert.AreEqual(MessageBusFiles.MalformedMqttRetainedFileReason, exception.Message);
    }

    [TestMethod]
    [DataRow(1, 10, 100L, DisplayName = "More queues than the bound")]
    [DataRow(10, 1, 100L, DisplayName = "More messages in a queue than the bound")]
    [DataRow(10, 10, 3L, DisplayName = "More queued bytes than the bound")]
    public async Task LoadAsync_QueuesPastABound_ThrowsWithTheQueueIndex(int maxQueues, int maxMessagesPerQueue, long maxQueuedBytes)
    {
        fixture.Write(
            fixture.Files.QueueIndexPath,
            [.. MessageBusFiles.QueueIndexHeader, .. Bytes("00000002 00000001 61 00000000 00000002 0000000000000000 00 0000000000000001 00 00000001 62 00000000 00000000")]);
        fixture.Write(fixture.Files.QueueMessageFilePath(0), [.. MessageBusFiles.QueueMessageHeader, .. Bytes(Hi)]);
        fixture.Write(fixture.Files.QueueMessageFilePath(1), [.. MessageBusFiles.QueueMessageHeader, .. Bytes(Hi)]);

        var exception = await fixture.LoadFailureAsync(fixture.NewBus(maxQueues: maxQueues, maxMessagesPerQueue: maxMessagesPerQueue, maxQueuedBytes: maxQueuedBytes));

        Assert.AreEqual(fixture.Files.QueueIndexPath, exception.FilePath);
        Assert.AreEqual(MessageBusFiles.MalformedFileReason, exception.Message);
    }

    private void WriteIndexNamingMessageFileZero() =>
        fixture.Write(fixture.Files.QueueIndexPath, [.. MessageBusFiles.QueueIndexHeader, .. Bytes("00000001 00000001 71 00000000 00000001 0000000000000000 00")]);
}
