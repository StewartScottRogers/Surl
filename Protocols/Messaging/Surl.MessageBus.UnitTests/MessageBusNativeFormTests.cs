using static Surl.MessageBus.PersistedBusFixture;

namespace Surl.MessageBus;

/// <summary>
/// <see cref="BusMessage.NativeForm"/> through the bus (ADR-0107 decision 6, BL-706): carried
/// unchanged by live fan-out, named queues, topic logs and the bus files, and counted with the
/// payload against the byte bounds.
/// </summary>
[TestClass]
public sealed class MessageBusNativeFormTests
{
    private static readonly byte[] NativeBytes = [0x00, 0x53, 0x72, 0xC1, 0x01, 0x00];

    private readonly PersistedBusFixture fixture = new();

    [TestMethod]
    public void Publish_AMessageWithANativeForm_DeliversItsFormatAndBytesUnchangedToASubscription()
    {
        var bus = fixture.NewBus();
        using var connection = bus.Connect();
        var received = new List<BusMessage>();
        connection.Subscribe("t", received.Add);
        var published = (byte[])NativeBytes.Clone();

        bus.Publish(WithNativeForm("t", "hi", published));
        published[0] = 0xFF;

        Assert.HasCount(1, received);
        AssertNativeForm(received[0]);
    }

    [TestMethod]
    public void PublishToQueue_AMessageWithANativeForm_DeliversItUnchangedAndAgainAfterARejectWithRequeue()
    {
        var bus = fixture.NewBus();
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var deliveries = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", deliveries.Add, maxUnacknowledged: 1).Consumer!;
        consumer.Start();

        bus.PublishToQueue("q", WithNativeForm("t", "hi", NativeBytes));
        consumer.Reject(deliveries[0].DeliveryTag, requeue: true);

        Assert.HasCount(2, deliveries);
        Assert.IsTrue(deliveries[1].Redelivered);
        AssertNativeForm(deliveries[0].Message);
        AssertNativeForm(deliveries[1].Message);
    }

    [TestMethod]
    public void ReadLog_AMessageWithANativeForm_ReturnsItsFormatAndBytesUnchanged()
    {
        var bus = fixture.NewBus();
        bus.Publish(WithNativeForm("t", "hi", NativeBytes));

        var read = bus.ReadLog("t", 0, long.MaxValue);

        Assert.HasCount(1, read.Records);
        AssertNativeForm(read.Records[0].Message);
    }

    [TestMethod]
    public void Publish_AMessageWithoutANativeForm_DeliversItWithNone()
    {
        var bus = fixture.NewBus();
        using var connection = bus.Connect();
        var received = new List<BusMessage>();
        connection.Subscribe("t", received.Add);

        bus.Publish(Message("t", "hi"));

        Assert.IsNull(received[0].NativeForm);
    }

    [TestMethod]
    public async Task LoadAsync_MessagesWithANativeFormSaved_KeepItInTheQueueTheLogAndTheRetainedSlot()
    {
        var bus = await fixture.LoadBusAsync();
        var connection = bus.Connect();
        connection.DeclareQueue("q", new BusQueueFlags(Durable: true));
        connection.BindQueue("q", "t");
        bus.Publish(WithNativeForm("t", "hi", NativeBytes) with { Retain = true });
        await bus.SaveChangesAsync();

        var loaded = await fixture.LoadBusAsync();
        var loadedConnection = loaded.Connect();
        Assert.AreEqual(1, loadedConnection.DeclareQueue("q", new BusQueueFlags(Durable: true)).ReadyMessageCount);
        var deliveries = new List<BusQueueDelivery>();
        loadedConnection.Consume("q", deliveries.Add).Consumer!.Start();

        var retained = loadedConnection.Subscribe("t", _ => { }, includeRetained: true).RetainedMessages;

        AssertNativeForm(deliveries.Single().Message);
        AssertNativeForm(retained.Single());
        AssertNativeForm(loaded.ReadLog("t", 0, long.MaxValue).Records.Single().Message);
    }

    [TestMethod]
    public async Task LoadAsync_ABusLogFileWrittenWithoutNativeForms_LoadsItsMessagesWithNone()
    {
        fixture.Write(
            fixture.Files.LogFilePath(0),
            [.. "SURL-BUS-LOG-1\n"u8, .. Bytes("00000001 74 0000000000000000 00000001 74 00 00 08df3a499fec8000 00000000 00000002 6869")]);

        var loaded = await fixture.LoadBusAsync();

        var record = loaded.ReadLog("t", 0, long.MaxValue).Records.Single();
        Assert.AreEqual("hi", Text(record.Message));
        Assert.IsNull(record.Message.NativeForm);
    }

    [TestMethod]
    public void Publish_PayloadAndNativeFormPastMaxMessageBytes_ReturnsMessageTooLarge()
    {
        var bus = new MessageBus(fixture.Clock, maxMessageBytes: 2);

        var result = bus.Publish(WithNativeForm("t", "a", [0x01, 0x02]));

        Assert.AreEqual(MessageBusOutcome.MessageTooLarge, result.Outcome);
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Publish(WithNativeForm("t", "a", [0x01])).Outcome);
    }

    [TestMethod]
    public void PublishToQueue_PayloadAndNativeFormPastMaxMessageBytes_ReturnsMessageTooLarge()
    {
        var bus = new MessageBus(fixture.Clock, maxMessageBytes: 2);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");

        Assert.AreEqual(MessageBusOutcome.MessageTooLarge, bus.PublishToQueue("q", WithNativeForm("t", "a", [0x01, 0x02])));
    }

    [TestMethod]
    public void PublishToQueue_NativeFormBytesCountAgainstMaxQueuedBytesAndAreFreedOnAcknowledge()
    {
        var bus = new MessageBus(fixture.Clock, maxQueuedBytes: 3);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var deliveries = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", deliveries.Add, maxUnacknowledged: 1).Consumer!;

        var first = bus.PublishToQueue("q", WithNativeForm("t", "a", [0x01, 0x02]));
        var second = bus.PublishToQueue("q", Message("t", "b"));
        consumer.Start();
        consumer.Acknowledge(deliveries[0].DeliveryTag);
        var third = bus.PublishToQueue("q", Message("t", "b"));

        Assert.AreEqual(MessageBusOutcome.Succeeded, first);
        Assert.AreEqual(MessageBusOutcome.QueueFull, second);
        Assert.AreEqual(MessageBusOutcome.Succeeded, third);
    }

    [TestMethod]
    public void LogRecordBytes_AMessageWithANativeForm_CountsItsBytes()
    {
        var withNativeForm = WithNativeForm("t", "hi", NativeBytes);

        var bytes = MessageBus.LogRecordBytes(withNativeForm);

        Assert.AreEqual(MessageBus.LogRecordBytes(Message("t", "hi")) + NativeBytes.Length, bytes);
    }

    private static BusMessage WithNativeForm(string topic, string payload, byte[] nativeBytes) =>
        Message(topic, payload) with { NativeForm = new BusNativeForm("amqp1", nativeBytes) };

    private static void AssertNativeForm(BusMessage message)
    {
        Assert.IsNotNull(message.NativeForm);
        Assert.AreEqual("amqp1", message.NativeForm.Format);
        CollectionAssert.AreEqual(NativeBytes, message.NativeForm.Bytes.ToArray());
    }
}
