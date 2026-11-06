using static Surl.MessageBus.PersistedBusFixture;

namespace Surl.MessageBus;

[TestClass]
public sealed class MessageBusPersistenceTests
{
    // Group g, topic t, offset 5, leader epoch -1, no metadata, committed at 2026-11-04T08:00:00Z.
    private const string OffsetEntry = "00000001 67 00000001 74 0000000000000005 ffffffff 00 08df3a499fec8000";

    private readonly PersistedBusFixture fixture = new();

    [TestMethod]
    public async Task LoadAsync_CommittedOffsetsSaved_ComeBackEqualAndTheNextSaveWritesNothingNew()
    {
        var bus = await fixture.LoadBusAsync();
        bus.CommitOffset(null, "g", "t", 5, -1, metadata: null);
        bus.CommitOffset(null, "h/i", "a/b", -2, 9, "meta");
        Assert.AreEqual(BusSaveResult.Saved, await bus.SaveChangesAsync());
        var written = fixture.Read(fixture.Files.OffsetsPath);

        var loaded = await fixture.LoadBusAsync();
        fixture.FileSystem.FailCreatesOf = "offsets";

        Assert.AreEqual(new BusCommittedOffset("g", "t", 5, -1, null, fixture.Clock.UtcNow), loaded.GetCommittedOffset("g", "t"));
        Assert.AreEqual(new BusCommittedOffset("h/i", "a/b", -2, 9, "meta", fixture.Clock.UtcNow), loaded.GetCommittedOffset("h/i", "a/b"));
        Assert.AreEqual(BusSaveResult.Saved, await loaded.SaveChangesAsync());
        CollectionAssert.AreEqual(written, fixture.Read(fixture.Files.OffsetsPath));
    }

    [TestMethod]
    public async Task SaveChangesAsync_TheOffsetsWriteFails_KeepsTheCommitAndTheNextSaveWritesIt()
    {
        var bus = await fixture.LoadBusAsync();
        fixture.FileSystem.FailCreatesOf = "offsets";
        bus.CommitOffset(null, "g", "t", 5, -1, null);

        var failed = await bus.SaveChangesAsync();
        fixture.FileSystem.FailCreatesOf = null;
        var retried = await bus.SaveChangesAsync();

        Assert.AreEqual(new BusSaveResult(MessageBusOutcome.StorageFailed, "The disk failed."), failed);
        Assert.AreEqual(BusSaveResult.Saved, retried);
        Assert.AreEqual(5, (await fixture.LoadBusAsync()).GetCommittedOffset("g", "t")!.Offset);
    }

    [TestMethod]
    public async Task LoadAsync_ABusHoldingACommittedOffset_Throws()
    {
        var bus = fixture.NewBus();
        bus.CommitOffset(null, "g", "t", 5, -1, null);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => bus.LoadAsync(fixture.Files));
    }

    [TestMethod]
    [DataRow("SURL-BUS-OFFSETS-2\n00000000", DisplayName = "Another header")]
    [DataRow("SURL-BUS-OFFSETS-1\n00000001", DisplayName = "A file cut short")]
    [DataRow("SURL-BUS-OFFSETS-1\n00000000 00", DisplayName = "Bytes after the last entry")]
    [DataRow("SURL-BUS-OFFSETS-1\n00000002 " + OffsetEntry + " " + OffsetEntry, DisplayName = "A group and topic twice")]
    [DataRow("SURL-BUS-OFFSETS-1\n00000001 00000001 67 00000001 2b 0000000000000005 ffffffff 00 08df3a499fec8000", DisplayName = "A wildcard topic")]
    [DataRow("SURL-BUS-OFFSETS-1\n00000001 00000001 67 00000001 74 0000000000000005 ffffffff 02 08df3a499fec8000", DisplayName = "A flags byte with an unknown bit")]
    [DataRow("SURL-BUS-OFFSETS-1\n00000001 00000001 67 00000001 74 0000000000000005 ffffffff 01", DisplayName = "Metadata flagged but missing")]
    [DataRow("SURL-BUS-OFFSETS-1\n00000001 00000001 67 00000001 74 0000000000000005 ffffffff 00 ffffffffffffffff", DisplayName = "A commit time out of range")]
    public async Task LoadAsync_AMalformedOffsetsFile_ThrowsWithItsPathAndNotAMessageBusFile(string headerAndHex)
    {
        var split = headerAndHex.IndexOf('\n', StringComparison.Ordinal) + 1;
        fixture.Write(fixture.Files.OffsetsPath, [.. System.Text.Encoding.ASCII.GetBytes(headerAndHex[..split]), .. Bytes(headerAndHex[split..])]);

        var exception = await fixture.LoadFailureAsync();

        Assert.AreEqual(fixture.Files.OffsetsPath, exception.FilePath);
        Assert.AreEqual(MessageBusFiles.MalformedFileReason, exception.Message);
    }

    [TestMethod]
    public async Task LoadAsync_AnOffsetsFileHoldingMoreThanMaxCommittedOffsets_ThrowsNotAMessageBusFile()
    {
        var first = OffsetEntry;
        var second = "00000001 68 00000001 74 0000000000000005 ffffffff 00 08df3a499fec8000";
        fixture.Write(fixture.Files.OffsetsPath, [.. MessageBusFiles.OffsetsHeader, .. Bytes("00000002 " + first + " " + second)]);

        var exception = await fixture.LoadFailureAsync(new MessageBus(fixture.Clock, maxCommittedOffsets: 1));

        Assert.AreEqual(fixture.Files.OffsetsPath, exception.FilePath);
        Assert.AreEqual(MessageBusFiles.MalformedFileReason, exception.Message);
        Assert.AreEqual(2, (await fixture.LoadBusAsync()).GetCommittedGroups().Count);
    }

    [TestMethod]
    public async Task SaveChangesAsync_ABusNeverLoaded_WritesNothing()
    {
        var bus = fixture.NewBus();
        bus.Publish(Message("t", "hi") with { Retain = true });

        var saved = await bus.SaveChangesAsync();

        Assert.AreEqual(BusSaveResult.Saved, saved);
        Assert.IsEmpty(fixture.Entries(SurlFolderPath));
    }

    [TestMethod]
    public async Task LoadAsync_NoFiles_LoadsAnEmptyBusAndWritesNothing()
    {
        var bus = await fixture.LoadBusAsync();

        Assert.IsEmpty(bus.GetLogTopics());
        Assert.IsEmpty(fixture.Entries(SurlFolderPath));
    }

    [TestMethod]
    public async Task LoadAsync_ABusLoadedOrHoldingALogOrAQueue_Throws()
    {
        var loaded = await fixture.LoadBusAsync();
        var published = fixture.NewBus();
        published.Publish(Message("t", "hi"));
        var declared = fixture.NewBus();
        declared.Connect().DeclareQueue("q", default);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => loaded.LoadAsync(fixture.Files));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => published.LoadAsync(fixture.Files));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => declared.LoadAsync(fixture.Files));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => fixture.NewBus().LoadAsync(null!));
    }

    [TestMethod]
    public async Task LoadAsync_RetainedMessagesSaved_AreOfferedToANewSubscriptionEqual()
    {
        var bus = await fixture.LoadBusAsync();
        var full = Message("a/b", "x") with { Retain = true, ContentType = "c", Headers = [new("h", "v")], Key = new byte[] { 2 } };
        bus.Publish(full);
        bus.Publish(Message("gone", "y") with { Retain = true });
        bus.Publish(new BusMessage("gone", ReadOnlyMemory<byte>.Empty) { Retain = true });
        await bus.SaveChangesAsync();

        var loaded = await fixture.LoadBusAsync();
        var retained = loaded.Connect().Subscribe("#", _ => { }, includeRetained: true).RetainedMessages;

        Assert.HasCount(1, retained);
        BusMessageCodecTests.AssertEquivalent(full with { PublishedAt = fixture.Clock.UtcNow }, retained[0]);
    }

    [TestMethod]
    public async Task LoadAsync_LogsSaved_KeepTheirOffsetsRecordsAndStartOffset()
    {
        var bus = await fixture.LoadBusAsync();
        bus.Publish(Message("t", "a"));
        await bus.SaveChangesAsync();
        bus.Publish(Message("t", "b"));
        bus.Publish(Message("u", "c"));
        await bus.SaveChangesAsync();

        var loaded = await fixture.LoadBusAsync();
        var read = loaded.ReadLog("t", 0, long.MaxValue);

        CollectionAssert.AreEqual(new[] { "t", "u" }, loaded.GetLogTopics().ToArray());
        CollectionAssert.AreEqual(new[] { "a", "b" }, read.Records.Select(record => Text(record.Message)).ToArray());
        Assert.AreEqual(2L, loaded.Publish(Message("t", "d")).LogOffset);
    }

    [TestMethod]
    public async Task SaveChangesAsync_ALogThatOnlyGrew_AppendsToItsFileTheSameBytesAWholeWriteHolds()
    {
        var bus = await fixture.LoadBusAsync();
        bus.Publish(Message("t", "a"));
        await bus.SaveChangesAsync();
        bus.Publish(Message("t", "b"));

        await bus.SaveChangesAsync();

        var expected = MessageBusFiles.EncodeLog("t", 0, bus.ReadLog("t", 0, long.MaxValue).Records.Select(record => record.Message));
        CollectionAssert.AreEqual(expected, fixture.Read(fixture.Files.LogFilePath(0)));
    }

    [TestMethod]
    public async Task SaveChangesAsync_ALogWhoseOldestRecordsWereDropped_RewritesItsFileFromTheNewStartOffset()
    {
        var bus = fixture.NewBus(maxLogBytesPerTopic: 2 * (MessageBus.LogRecordOverheadBytes + 1));
        await bus.LoadAsync(fixture.Files);
        bus.Publish(Message("t", "a"));
        bus.Publish(Message("t", "b"));
        await bus.SaveChangesAsync();
        bus.Publish(Message("t", "c"));

        await bus.SaveChangesAsync();

        var loaded = await fixture.LoadBusAsync();
        Assert.AreEqual(new BusLogOffsets(MessageBusOutcome.Succeeded, 1, 3), loaded.GetLogOffsets("t"));
        CollectionAssert.AreEqual(new[] { "b", "c" }, loaded.ReadLog("t", 1, long.MaxValue).Records.Select(record => Text(record.Message)).ToArray());
    }

    [TestMethod]
    public async Task LoadAsync_ADurableQueueSaved_HoldsItsBindingAndMessagesWithTheHandedOutOneRedelivered()
    {
        var bus = await fixture.LoadBusAsync();
        var connection = bus.Connect();
        connection.DeclareQueue("q", new BusQueueFlags(Durable: true));
        connection.BindQueue("q", "t/#");
        bus.Publish(Message("t/1", "a"));
        bus.Publish(Message("t/2", "b"));
        var consumer = connection.Consume("q", _ => { }, maxUnacknowledged: 1).Consumer!;
        consumer.Start();
        await bus.SaveChangesAsync();

        var loaded = await fixture.LoadBusAsync();
        var deliveries = new List<BusQueueDelivery>();
        var loadedConnection = loaded.Connect();
        var declared = loadedConnection.DeclareQueue("q", new BusQueueFlags(Durable: true));
        loadedConnection.Consume("q", deliveries.Add, maxUnacknowledged: 0).Consumer!.Start();
        loaded.Publish(Message("t/3", "c"));

        Assert.AreEqual(2, declared.ReadyMessageCount);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, deliveries.Select(delivery => Text(delivery.Message)).ToArray());
        CollectionAssert.AreEqual(new[] { true, false, false }, deliveries.Select(delivery => delivery.Redelivered).ToArray());
    }

    [TestMethod]
    public async Task SaveChangesAsync_QueuesNotDurableExclusiveOrAutoDelete_AreNotSaved()
    {
        var bus = await fixture.LoadBusAsync();
        var connection = bus.Connect();
        connection.DeclareQueue("plain", default);
        connection.DeclareQueue("exclusive", new BusQueueFlags(Durable: true, Exclusive: true));
        connection.DeclareQueue("auto", new BusQueueFlags(Durable: true, AutoDelete: true));
        bus.PublishToQueue("plain", Message("t", "a"));

        await bus.SaveChangesAsync();

        CollectionAssert.AreEqual(Bytes("00000000"), fixture.Read(fixture.Files.QueueIndexPath)[MessageBusFiles.QueueIndexHeader.Length..]);
        Assert.IsEmpty(fixture.Entries(fixture.Files.QueueMessagesFolderPath));
    }

    [TestMethod]
    public async Task SaveChangesAsync_AnAcknowledgedMessage_DeletesItsFile()
    {
        var bus = await fixture.LoadBusAsync();
        var connection = bus.Connect();
        connection.DeclareQueue("q", new BusQueueFlags(Durable: true));
        bus.PublishToQueue("q", Message("t", "a"));
        await bus.SaveChangesAsync();
        var deliveries = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", deliveries.Add, maxUnacknowledged: 0).Consumer!;
        consumer.Start();
        consumer.Acknowledge(deliveries[0].DeliveryTag);

        await bus.SaveChangesAsync();

        Assert.IsFalse(fixture.Exists(fixture.Files.QueueMessageFilePath(0)));
        Assert.AreEqual(0, (await fixture.LoadBusAsync()).Connect().DeclareQueue("q", new BusQueueFlags(Durable: true)).ReadyMessageCount);
    }

    [TestMethod]
    public async Task LoadAsync_AnMqttRetainedMessagesFileAndNoBusRetainedFile_PutsItsMessagesOnTheBusAndWritesTheBusFileAtOnce()
    {
        byte[] mqtt = [.. "SURL-MQTT-RETAINED-1\n"u8, .. Bytes("0001 74 00000002 6869")];
        fixture.Write(fixture.Files.MqttRetainedMessagesPath, mqtt);

        var bus = await fixture.LoadBusAsync();
        var retained = bus.Connect().Subscribe("t", _ => { }, includeRetained: true).RetainedMessages;

        Assert.HasCount(1, retained);
        BusMessageCodecTests.AssertEquivalent(Message("t", "hi") with { Retain = true, PublishedAt = fixture.Clock.UtcNow }, retained[0]);
        CollectionAssert.AreEqual(MessageBusFiles.EncodeRetained(retained), fixture.Read(fixture.Files.RetainedPath));
        CollectionAssert.AreEqual(mqtt, fixture.Read(fixture.Files.MqttRetainedMessagesPath));
        CollectionAssert.AreEqual(new[] { "t" }, bus.GetLogTopics().ToArray());
        Assert.AreEqual(new BusLogOffsets(MessageBusOutcome.Succeeded, 0, 0), bus.GetLogOffsets("t"));
    }

    [TestMethod]
    public async Task LoadAsync_ABusRetainedFile_IsReadAndTheMqttFileIsNot()
    {
        fixture.Write(fixture.Files.MqttRetainedMessagesPath, [.. "SURL-MQTT-RETAINED-1\n"u8, .. Bytes("0001 74 00000002 6869")]);
        fixture.Write(fixture.Files.RetainedPath, [.. MessageBusFiles.RetainedHeader]);

        var bus = await fixture.LoadBusAsync();

        Assert.IsEmpty(bus.Connect().Subscribe("#", _ => { }, includeRetained: true).RetainedMessages);
    }

    [TestMethod]
    public async Task LoadAsync_TheMigratedRetainedFileCannotBeWritten_ReportsStorageFailedAndKeepsTheMessages()
    {
        fixture.Write(fixture.Files.MqttRetainedMessagesPath, [.. "SURL-MQTT-RETAINED-1\n"u8, .. Bytes("0001 74 00000002 6869")]);
        fixture.FileSystem.FailMovesTo = fixture.Files.RetainedPath;
        var bus = fixture.NewBus();

        var result = await bus.LoadAsync(fixture.Files);

        Assert.AreEqual(new BusSaveResult(MessageBusOutcome.StorageFailed, "The disk failed."), result);
        Assert.HasCount(1, bus.Connect().Subscribe("t", _ => { }, includeRetained: true).RetainedMessages);
        Assert.IsFalse(fixture.Exists(fixture.Files.RetainedPath));
        Assert.HasCount(1, fixture.Entries(fixture.Files.LogsFolderPath));
        Assert.IsEmpty(fixture.Entries(fixture.Files.StateFolderPath).Where(name => name.StartsWith('.')));
    }

    [TestMethod]
    public async Task SaveChangesAsync_ARetainedWriteFails_KeepsTheChangeInMemoryAndTheNextSaveWritesIt()
    {
        var bus = await fixture.LoadBusAsync();
        fixture.FileSystem.FailCreatesOf = "retained";
        bus.Publish(Message("t", "hi") with { Retain = true });

        var failed = await bus.SaveChangesAsync();
        fixture.FileSystem.FailCreatesOf = null;
        var retried = await bus.SaveChangesAsync();

        Assert.AreEqual(new BusSaveResult(MessageBusOutcome.StorageFailed, "The disk failed."), failed);
        Assert.AreEqual(BusSaveResult.Saved, retried);
        Assert.HasCount(1, (await fixture.LoadBusAsync()).Connect().Subscribe("t", _ => { }, includeRetained: true).RetainedMessages);
    }

    [TestMethod]
    public async Task SaveChangesAsync_ALogAppendFails_KeepsTheRecordAndTheNextSaveRewritesTheFileWhole()
    {
        var bus = await fixture.LoadBusAsync();
        bus.Publish(Message("t", "a"));
        await bus.SaveChangesAsync();
        bus.Publish(Message("t", "b"));
        fixture.FileSystem.FailAppendsTo = fixture.Files.LogFilePath(0);

        var failed = await bus.SaveChangesAsync();
        var retried = await bus.SaveChangesAsync();

        Assert.AreEqual(MessageBusOutcome.StorageFailed, failed.Outcome);
        Assert.AreEqual(BusSaveResult.Saved, retried);
        Assert.AreEqual(2L, (await fixture.LoadBusAsync()).GetLogOffsets("t").HighWatermark);
    }

    [TestMethod]
    public async Task SaveChangesAsync_AQueueMessageFileWriteFails_KeepsTheMessageAndTheNextSaveWritesIt()
    {
        var bus = await fixture.LoadBusAsync();
        bus.Connect().DeclareQueue("q", new BusQueueFlags(Durable: true));
        await bus.SaveChangesAsync();
        bus.PublishToQueue("q", Message("t", "a"));
        fixture.FileSystem.FailCreatesOf = fixture.Files.QueueMessagesFolderPath;

        var failed = await bus.SaveChangesAsync();
        fixture.FileSystem.FailCreatesOf = null;
        var retried = await bus.SaveChangesAsync();

        Assert.AreEqual(MessageBusOutcome.StorageFailed, failed.Outcome);
        Assert.AreEqual(BusSaveResult.Saved, retried);
        Assert.AreEqual(1, (await fixture.LoadBusAsync()).Connect().DeclareQueue("q", new BusQueueFlags(Durable: true)).ReadyMessageCount);
    }

    [TestMethod]
    public async Task SaveChangesAsync_TheQueueIndexWriteFails_KeepsTheChangeAndTheNextSaveWritesIt()
    {
        var bus = await fixture.LoadBusAsync();
        bus.Connect().DeclareQueue("q", new BusQueueFlags(Durable: true));
        bus.PublishToQueue("q", Message("t", "a"));
        fixture.FileSystem.FailMovesTo = fixture.Files.QueueIndexPath;

        var failed = await bus.SaveChangesAsync();
        fixture.FileSystem.FailMovesTo = null;
        var retried = await bus.SaveChangesAsync();

        Assert.AreEqual(MessageBusOutcome.StorageFailed, failed.Outcome);
        Assert.AreEqual(BusSaveResult.Saved, retried);
        CollectionAssert.AreEqual(new[] { "0000000000000000" }, fixture.Entries(fixture.Files.QueueMessagesFolderPath));
        Assert.AreEqual(1, (await fixture.LoadBusAsync()).Connect().DeclareQueue("q", new BusQueueFlags(Durable: true)).ReadyMessageCount);
    }

    [TestMethod]
    public async Task SaveChangesAsync_AFileNoQueueHoldsCannotBeDeleted_ReportsItAndTheNextSaveDeletesIt()
    {
        var bus = await fixture.LoadBusAsync();
        var connection = bus.Connect();
        connection.DeclareQueue("q", new BusQueueFlags(Durable: true));
        bus.PublishToQueue("q", Message("t", "a"));
        await bus.SaveChangesAsync();
        connection.DeleteQueue("q");
        fixture.FileSystem.FailDeletesOf = fixture.Files.QueueMessagesFolderPath;

        var failed = await bus.SaveChangesAsync();
        fixture.FileSystem.FailDeletesOf = null;
        connection.DeclareQueue("other", new BusQueueFlags(Durable: true));
        var retried = await bus.SaveChangesAsync();

        Assert.AreEqual(MessageBusOutcome.StorageFailed, failed.Outcome);
        Assert.AreEqual(BusSaveResult.Saved, retried);
        Assert.IsEmpty(fixture.Entries(fixture.Files.QueueMessagesFolderPath));
    }

    [TestMethod]
    public async Task SaveChangesAsync_ABusLoadedWithQueueMessageFiles_NumbersNewFilesAfterThem()
    {
        var bus = await fixture.LoadBusAsync();
        bus.Connect().DeclareQueue("q", new BusQueueFlags(Durable: true));
        bus.PublishToQueue("q", Message("t", "a"));
        bus.PublishToQueue("q", Message("t", "b"));
        await bus.SaveChangesAsync();

        var loaded = await fixture.LoadBusAsync();
        loaded.PublishToQueue("q", Message("t", "c"));
        await loaded.SaveChangesAsync();

        CollectionAssert.AreEqual(
            new[] { "0000000000000000", "0000000000000001", "0000000000000002" },
            fixture.Entries(fixture.Files.QueueMessagesFolderPath));
    }

    [TestMethod]
    public async Task SaveChangesAsync_AWriteThrowsSomethingOtherThanAStorageFailure_LetsItThrough()
    {
        var bus = await fixture.LoadBusAsync();
        bus.Publish(Message("t", "hi") with { Retain = true });
        fixture.FileSystem.FailCreatesOf = "retained";
        fixture.FileSystem.Failure = new InvalidOperationException("A defect.");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => bus.SaveChangesAsync());
    }
}
