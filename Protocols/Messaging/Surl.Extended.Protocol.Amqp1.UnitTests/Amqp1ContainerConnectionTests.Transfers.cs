using System.Net;
using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Transfers, link credit, settlement and the message format over the shared message bus (AMQP 1.0
/// Part 2 sections 2.6.7, 2.6.12 to 2.6.14, 2.7.4 to 2.7.6, Part 3 sections 3.2 and 3.4; ADR-0107
/// decisions 5 and 6). The messages are written by hand from Part 3 section 3.2; the performatives
/// are built with the library's codec, which BL-510's tests pin.
/// </summary>
public sealed partial class Amqp1ContainerConnectionTests
{
    // delivery-annotations (0x71) {"x-da": "1"}: next-hop only, never kept (Part 3 section 3.2.2).
    private const string DeliveryAnnotationsHex = "005371" + "C10A02" + "A304782D6461" + "A10131";

    // message-annotations (0x72) {"x-opt": "v"}.
    private const string MessageAnnotationsHex = "005372" + "C10B02" + "A305782D6F7074" + "A10176";

    // properties (0x73) with message-id "m1" and every other field left out.
    private const string PropertiesHex = "005373" + "C00501" + "A1026D31";

    // application-properties (0x74) {"k": "v"}.
    private const string ApplicationPropertiesHex = "005374" + "C10702" + "A1016B" + "A10176";

    // data (0x75) "hello".
    private const string DataHex = "005375" + "A005" + "68656C6C6F";

    private static readonly byte[] Message = Convert.FromHexString(MessageAnnotationsHex + PropertiesHex + ApplicationPropertiesHex + DataHex);

    [TestMethod]
    public async Task ServeAsync_MessageInThreeMoreFrames_IsStoredWholeAndAccepted()
    {
        var bus = Bus();

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(SenderAttach(0, "orders")),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [7], MessageFormat = 0, More = true }, Message.AsMemory(0, 10)),
            BusFrame(new Amqp1Transfer(0) { More = true }, Message.AsMemory(10, 10)),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [7] }, Message.AsMemory(20)));

        Assert.AreEqual(
            new Amqp1Disposition(IsReceiver: true, 0) { Settled = true, State = Amqp1DeliveryState.Accepted },
            written.Single(frame => frame.Performative is Amqp1Disposition).Performative);
        var stored = bus.Connect().GetFromQueue("orders").Delivery!.Message;
        Assert.AreEqual("hello", Encoding.UTF8.GetString(stored.Payload.Span));
        Assert.AreEqual(BusQualityOfService.AtLeastOnce, stored.QualityOfService);
        CollectionAssert.AreEqual(Message, stored.NativeForm!.Bytes.ToArray());
        CollectionAssert.AreEqual(new[] { new BusMessageHeader("k", "v") }, stored.Headers.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ReceiverWithCreditTwo_GetsTwoOfThreeThenTheThirdAfterMoreCredit()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "one", "two", "three");

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "orders")),
            BusFrame(Credit(0, deliveryCount: 0, credit: 2)),
            BusFrame(Credit(0, deliveryCount: 2, credit: 1)));

        var transfers = Transfers(written);
        CollectionAssert.AreEqual(new[] { "one", "two", "three" }, transfers.Select(transfer => DataOf(transfer.Payload)).ToArray());
        CollectionAssert.AreEqual(new uint?[] { 0, 1, 2 }, transfers.Select(transfer => transfer.Transfer.DeliveryId).ToArray());
        var firstCreditAt = written.FindIndex(frame => frame.Performative is Amqp1Transfer);
        Assert.IsInstanceOfType<Amqp1Transfer>(written[firstCreditAt + 1].Performative);
        Assert.IsTrue(transfers.All(transfer => transfer.Transfer.Settled == false));
    }

    [TestMethod]
    public async Task ServeAsync_PreSettledDelivery_IsNotRedelivered()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "once");

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "orders", senderSettleMode: 1)),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
            BusFrame(new Amqp1Detach(0) { Closed = true }));

        Assert.IsTrue(Transfers(written).Single().Transfer.Settled);
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Connect().GetFromQueue("orders").Outcome);
        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
    }

    [TestMethod]
    public async Task ServeAsync_ReleasedDelivery_IsRedeliveredWithItsDeliveryCountRaised()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "again");

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "orders")),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
            BusFrame(new Amqp1Disposition(true, 0) { Settled = true, State = State(Amqp1Descriptor.Released) }));

        var transfers = Transfers(written);
        Assert.HasCount(2, transfers);
        var redelivered = Amqp1MessageSection.ReadAll(transfers[1].Payload);
        Assert.AreEqual(1u, Amqp1Header.FromDescribed(redelivered[0].Described).DeliveryCount);
        Assert.AreEqual("again", DataOf(transfers[1].Payload));
    }

    [TestMethod]
    public async Task ServeAsync_RejectedDelivery_IsNotRedelivered()
    {
        var bus = Bus();
        Enqueue(bus, "orders", "bad");

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "orders")),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
            BusFrame(new Amqp1Disposition(true, 0) { Settled = true, State = Amqp1DeliveryState.Rejected(new Amqp1Error(Amqp1ErrorCondition.DecodeError)) }));

        Assert.HasCount(1, Transfers(written));
        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
    }

    [TestMethod]
    public async Task ServeAsync_AbortedTransfer_IsDiscarded()
    {
        var bus = Bus();

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(SenderAttach(0, "orders")),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [1], More = true }, Message.AsMemory(0, 10)),
            BusFrame(new Amqp1Transfer(0) { Aborted = true }));

        Assert.IsFalse(written.Any(frame => frame.Performative is Amqp1Disposition));
        Assert.IsNull(bus.Connect().GetFromQueue("orders").Delivery);
    }

    [TestMethod]
    public async Task ServeAsync_TransferBeyondCredit_DetachesWithTransferLimitExceeded()
    {
        var (written, log) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(SenderAttach(0, "orders")),
            BusFrame(new Amqp1Flow(100, 0, 100) { Handle = 0, DeliveryCount = 100, LinkCredit = 0 }),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [1] }, Message),
            BusFrame(new Amqp1Transfer(0) { DeliveryId = 1, DeliveryTag = [2] }, Message),
            BusFrame(new Amqp1Detach(0) { Closed = true }));

        var detach = (Amqp1Detach)written.Last().Performative!;
        Assert.AreEqual(new Amqp1Detach(0) { Closed = true, Error = new(Amqp1ErrorCondition.TransferLimitExceeded) { Description = "a transfer beyond link credit" } }, detach);
        Assert.HasCount(1, written.Where(frame => frame.Performative is Amqp1Detach));
        CollectionAssert.Contains(log.Notes.ToList(), "Link 0 detached with amqp:link:transfer-limit-exceeded: a transfer beyond link credit.");
    }

    [TestMethod]
    public async Task ServeAsync_MessageSections_ReachTheReceiverByteForByteWithoutDeliveryAnnotations()
    {
        var sent = Convert.FromHexString(DeliveryAnnotationsHex).Concat(Message).ToArray();

        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(ReceiverAttach(0, "/topic/news")),
            BusFrame(Credit(0, deliveryCount: 0, credit: 5)),
            BusFrame(SenderAttach(1, "/topic/news")),
            BusFrame(new Amqp1Transfer(1) { DeliveryId = 0, DeliveryTag = [1], Settled = true }, sent));

        CollectionAssert.AreEqual(Message, Transfers(written).Single().Payload.ToArray());
        Assert.IsFalse(written.Any(frame => frame.Performative is Amqp1Disposition));
    }

    [TestMethod]
    public async Task ServeAsync_MessagePublishedUnderAnMqttName_ReachesTheReceiverOfTheMappedAddress()
    {
        var bus = Bus();
        var connection = new InMemoryConnection(
            [Open(), Begin(), BusFrame(ReceiverAttach(0, "/topic/sensors/temp")), BusFrame(Credit(0, deliveryCount: 0, credit: 5))],
            peerHalfClosesWhenExhausted: false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = new Amqp1ContainerConnection(connection, Context(new RecordingExchangeLog(), cancellation.Token), Options(), bus, "alice").ServeAsync();
        await WaitUntilWrittenAsync(async () => (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Any(frame => frame.Performative is Amqp1Attach));

        bus.Publish(new BusMessage(MqttTopicNames.ToCanonicalTopic("sensors/temp").Name!, "21"u8.ToArray()) { ContentType = "text/plain" });

        await WaitUntilWrittenAsync(async () => Transfers(await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Count == 1);
        await cancellation.CancelAsync();
        await serving;
        var payload = Transfers(await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Single().Payload;
        var properties = Amqp1Properties.FromDescribed(Amqp1MessageSection.ReadAll(payload)[0].Described);
        Assert.AreEqual("/topic/sensors/temp", properties.To);
        Assert.AreEqual(new Amqp1Symbol("text/plain"), properties.ContentType);
        Assert.AreEqual("21", DataOf(payload));
    }

    [TestMethod]
    public async Task ServeAsync_AttachRefusedByTopicAccess_IsAnsweredWithUnauthorizedAccess()
    {
        var bus = Bus(new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("news/#", "alice", TopicAccessRights.Subscribe)] });

        var (written, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(SenderAttach(0, "/topic/news/today")));

        var attach = (Amqp1Attach)written[2].Performative!;
        Assert.IsNull(attach.Target);
        Assert.AreEqual(
            new Amqp1Detach(0) { Closed = true, Error = new(Amqp1ErrorCondition.UnauthorizedAccess) { Description = "not authorized" } },
            written[3].Performative);
        Assert.HasCount(4, written);
    }

    private static MessageBus.MessageBus Bus(MessageBusConfiguration? configuration = null) =>
        new(new ManualTimeProvider(), configuration: configuration);

    private static void Enqueue(MessageBus.MessageBus bus, string queue, params string[] messages)
    {
        bus.Connect().DeclareQueue(queue, new BusQueueFlags(Durable: true));
        foreach (var message in messages)
        {
            Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue(queue, new BusMessage(queue, Encoding.UTF8.GetBytes(message))));
        }
    }

    private static byte[] Open() => Amqp1Frame.Carrying(0, new Amqp1Open("client")).Encode();

    private static byte[] Begin() => BusFrame(new Amqp1Begin(0, 100, 100));

    private static byte[] BusFrame(Amqp1Performative performative, ReadOnlyMemory<byte> payload = default) =>
        Amqp1Frame.Carrying(0, performative, payload).Encode();

    private static Amqp1Attach ReceiverAttach(uint handle, string address, byte senderSettleMode = 0) => new($"receiver-{handle}", handle, IsReceiver: true)
    {
        SenderSettleMode = senderSettleMode,
        Source = new Amqp1Described(Amqp1Descriptor.Source, new List<object?> { address }),
    };

    private static Amqp1Flow Credit(uint handle, uint deliveryCount, uint credit) =>
        new(100, 0, 100) { NextIncomingId = 0, Handle = handle, DeliveryCount = deliveryCount, LinkCredit = credit };

    private static Amqp1Described State(ulong descriptor) => new(descriptor, new List<object?>());

    private static List<(Amqp1Transfer Transfer, byte[] Payload)> Transfers(List<(Amqp1Performative? Performative, byte[] Payload)> written) =>
        [.. written.Where(frame => frame.Performative is Amqp1Transfer).Select(frame => ((Amqp1Transfer)frame.Performative!, frame.Payload))];

    private static string DataOf(byte[] payload) =>
        Encoding.UTF8.GetString((byte[])Amqp1MessageSection.ReadAll(payload).Single(section => section.DescriptorCode == Amqp1Descriptor.Data).Described.Value!);

    private static Amqp1ContainerOptions Options() => new() { ContainerId = "surl-00000000000000000000000000000001", IdleTimeout = TimeSpan.FromSeconds(120) };

    private async Task<(List<(Amqp1Performative? Performative, byte[] Payload)> Written, RecordingExchangeLog Log)> ServeOnBusAsync(MessageBus.MessageBus bus, params byte[][] frames)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(frames.Select(frame => (ReadOnlyMemory<byte>)frame));

        await new Amqp1ContainerConnection(connection, Context(log, TestContext.CancellationToken), Options(), bus, "alice").ServeAsync();

        return (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes), log);
    }

    private async Task<List<(Amqp1Performative? Performative, byte[] Payload)>> ReadWrittenWithPayloadsAsync(byte[] written)
    {
        var reader = new Amqp1FrameReader(new InMemoryConnection([written]));
        List<(Amqp1Performative?, byte[])> frames = [];
        while ((await reader.ReadFrameAsync(uint.MaxValue, TestContext.CancellationToken)).Frame is { } frame)
        {
            var body = Amqp1FrameBody.Read(frame);
            frames.Add((body.Performative, body.Payload.ToArray()));
        }

        return frames;
    }

    private async Task WaitUntilWrittenAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The connection did not reach the expected state in time.");
            }

            await Task.Delay(1, TestContext.CancellationToken);
        }
    }

    private static ExchangeContext Context(IExchangeLog log, CancellationToken cancellation) => new(
        1,
        new ListenUrl("amqp1", "127.0.0.1", 5672).WithBoundPort(5672),
        new IPEndPoint(IPAddress.Loopback, 5672),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        new ManualTimeProvider(),
        cancellation);
}
