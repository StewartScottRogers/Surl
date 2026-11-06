using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Source filters on a client receiver's link (AMQP 1.0 Part 3 sections 3.5.1, 3.5.3 and 3.5.8; the
/// Apache filter registry; ADR-0146 decisions 2.1, 2.2 and 2.4). Filter entries and messages are built
/// with the library's codec, which BL-510's tests pin.
/// </summary>
public sealed partial class Amqp1ContainerConnectionTests
{
    private static readonly Amqp1Symbol SelectorDescriptor = new("apache.org:selector-filter:string");

    [TestMethod]
    public async Task ServeAsync_AttachWithSelectorNoLocalAndAnUnknownFilter_EchoesTheFirstTwoUnderTheClientsKeys()
    {
        var selector = new Amqp1Described(SelectorDescriptor, "color = 'red'");
        var noLocal = new Amqp1Described(0x0000468C_00000003ul, new List<object?>());
        var xquery = new Amqp1Described(new Amqp1Symbol("apache.org:xquery-filter:string"), "/a");
        var filters = new Amqp1Map(
        [
            new(new Amqp1Symbol("my-selector"), selector),
            new(new Amqp1Symbol("my-no-local"), noLocal),
            new(new Amqp1Symbol("my-xquery"), xquery),
        ]);

        var (written, _) = await ServeOnBusAsync(Bus(), Open(), Begin(), BusFrame(FilteredReceiverAttach(0, "/topic/news", filters)));

        var source = Fields(((Amqp1Attach)written[2].Performative!).Source);
        Assert.HasCount(8, source);
        var echoed = (Amqp1Map)source[7]!;
        CollectionAssert.AreEqual(new object?[] { new Amqp1Symbol("my-selector"), new Amqp1Symbol("my-no-local") }, echoed.Entries.Select(entry => entry.Key).ToArray());
        Assert.AreEqual(selector, echoed.Entries[0].Value);
        var echoedNoLocal = (Amqp1Described)echoed.Entries[1].Value!;
        Assert.AreEqual(0x0000468C_00000003ul, echoedNoLocal.Descriptor);
        Assert.IsEmpty((IReadOnlyList<object?>)echoedNoLocal.Value!);
    }

    [TestMethod]
    public async Task ServeAsync_TopicLinkWithASelector_GetsOnlySelectedMessagesAndFilteredOnesNeverWait()
    {
        var sends = Enumerable.Range(0, Amqp1ContainerConnection.MaxWaitingDeliveries + 1)
            .Select(index => BusFrame(new Amqp1Transfer(1) { DeliveryId = (uint)index, DeliveryTag = BitConverter.GetBytes(index), Settled = true }, ColoredMessage("blue", "skip")));

        var (written, log) = await ServeOnBusAsync(
            Bus(),
            [
                Open(),
                BusFrame(new Amqp1Begin(0, 5000, 5000)),
                BusFrame(FilteredReceiverAttach(0, "/topic/news", SelectorFilter("color = 'red'"))),
                BusFrame(SenderAttach(1, "/topic/news")),
                .. sends,
                BusFrame(new Amqp1Transfer(1) { DeliveryId = 1001, DeliveryTag = [9], Settled = true }, ColoredMessage("red", "wanted")),
                BusFrame(Credit(0, deliveryCount: 0, credit: 10) with { IncomingWindow = 5000 }),
            ]);

        Assert.AreEqual("wanted", DataOf(Transfers(written).Single().Payload));
        Assert.IsFalse(log.Notes.Any(note => note.Contains("dropped", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_TopicLinkWithNoLocal_WithholdsTheConnectionsOwnPublishesOnly()
    {
        var bus = Bus();
        var connection = new InMemoryConnection(
            [
                Open(),
                Begin(),
                BusFrame(FilteredReceiverAttach(0, "/topic/news", new Amqp1Map([new(new Amqp1Symbol("nl"), new Amqp1Described(new Amqp1Symbol("apache.org:no-local-filter:list"), new List<object?>()))]))),
                BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
                BusFrame(SenderAttach(1, "/topic/news")),
                BusFrame(new Amqp1Transfer(1) { DeliveryId = 0, DeliveryTag = [1], Settled = true }, ColoredMessage("red", "own")),
            ],
            peerHalfClosesWhenExhausted: false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = new Amqp1ContainerConnection(connection, Context(new RecordingExchangeLog(), cancellation.Token), Options(), bus, "alice").ServeAsync();
        await WaitUntilWrittenAsync(async () => (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Count(frame => frame.Performative is Amqp1Attach) == 2);

        bus.Publish(new BusMessage("news", "other"u8.ToArray()));

        await WaitUntilWrittenAsync(async () => Transfers(await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Count == 1);
        await cancellation.CancelAsync();
        await serving;

        Assert.AreEqual("other", DataOf(Transfers(await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Single().Payload));
    }

    [TestMethod]
    public async Task ServeAsync_QueueLinksWithDifferentSelectors_EachGetWhatTheirsAcceptsAndTheRestStaysReady()
    {
        var bus = Bus();
        bus.Connect().DeclareQueue("orders", new BusQueueFlags(Durable: true));
        foreach (var color in new[] { "red", "blue", "green", "red" })
        {
            bus.PublishToQueue("orders", new BusMessage("orders", Encoding.UTF8.GetBytes(color)) { Headers = [new("color", color)] });
        }

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(FilteredReceiverAttach(0, "orders", SelectorFilter("color = 'red'"), senderSettleMode: 1)),
            BusFrame(FilteredReceiverAttach(1, "orders", SelectorFilter("color = 'blue'"), senderSettleMode: 1)),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)),
            BusFrame(Credit(1, deliveryCount: 0, credit: 10)));

        var transfers = Transfers(written);
        CollectionAssert.AreEqual(new[] { "red", "red" }, transfers.Where(transfer => transfer.Transfer.Handle == 0).Select(transfer => DataOf(transfer.Payload)).ToArray());
        CollectionAssert.AreEqual(new[] { "blue" }, transfers.Where(transfer => transfer.Transfer.Handle == 1).Select(transfer => DataOf(transfer.Payload)).ToArray());
        var left = bus.Connect().GetFromQueue("orders").Delivery!;
        Assert.AreEqual("green", Encoding.UTF8.GetString(left.Message.Payload.Span));
    }

    [TestMethod]
    public async Task ServeAsync_TopicBindingOnAMessageAnotherProtocolPublished_MatchesItsTopicAsARoutingKey()
    {
        var bus = Bus();
        var binding = new Amqp1Map([new(new Amqp1Symbol("tb"), new Amqp1Described(0x0000468C_00000001ul, "sensors.*.temp"))]);
        var connection = new InMemoryConnection(
            [Open(), Begin(), BusFrame(FilteredReceiverAttach(0, "/topic/sensors/#", binding)), BusFrame(Credit(0, deliveryCount: 0, credit: 10))],
            peerHalfClosesWhenExhausted: false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = new Amqp1ContainerConnection(connection, Context(new RecordingExchangeLog(), cancellation.Token), Options(), bus, "alice").ServeAsync();
        await WaitUntilWrittenAsync(async () => (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Any(frame => frame.Performative is Amqp1Attach));

        bus.Publish(new BusMessage(MqttTopicNames.ToCanonicalTopic("sensors/kitchen/humidity").Name!, "40"u8.ToArray()));
        bus.Publish(new BusMessage(MqttTopicNames.ToCanonicalTopic("sensors/kitchen/temp").Name!, "21"u8.ToArray()));

        await WaitUntilWrittenAsync(async () => Transfers(await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Count == 1);
        await cancellation.CancelAsync();
        await serving;
        Assert.AreEqual("21", DataOf(Transfers(await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Single().Payload));
    }

    [TestMethod]
    [DataRow("color = ", DisplayName = "malformed")]
    [DataRow(null, DisplayName = "not a string")]
    public async Task ServeAsync_AttachWithARefusedSelector_IsRefusedWithInvalidField(string? selector)
    {
        var bus = Bus();

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(FilteredReceiverAttach(0, "orders", new Amqp1Map([new(new Amqp1Symbol("s"), new Amqp1Described(SelectorDescriptor, (object?)selector ?? 7))]))));

        Assert.IsNull(((Amqp1Attach)written[2].Performative!).Source);
        Assert.AreEqual(
            new Amqp1Detach(0) { Closed = true, Error = new(Amqp1ErrorCondition.InvalidField) { Description = "selector not valid" } },
            written[3].Performative);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.Connect().GetFromQueue("orders").Outcome);
    }

    [TestMethod]
    public async Task ServeAsync_SenderAttachWithAFilterOnItsSource_IsNotFiltered()
    {
        var attach = SenderAttach(0, "orders") with
        {
            Source = new Amqp1Described(Amqp1Descriptor.Source, new List<object?> { "client", null, null, null, null, null, null, SelectorFilter("((") }),
        };

        var (written, _) = await ServeOnBusAsync(Bus(), Open(), Begin(), BusFrame(attach));

        Assert.IsNotNull(((Amqp1Attach)written[2].Performative!).Target);
        Assert.IsInstanceOfType<Amqp1Flow>(written[3].Performative);
    }

    private static Amqp1Map SelectorFilter(string selector) =>
        new([new(new Amqp1Symbol("jms-selector"), new Amqp1Described(SelectorDescriptor, selector))]);

    private static Amqp1Attach FilteredReceiverAttach(uint handle, string address, Amqp1Map filters, byte senderSettleMode = 0) =>
        ReceiverAttach(handle, address, senderSettleMode) with
        {
            Source = new Amqp1Described(Amqp1Descriptor.Source, new List<object?> { address, null, null, null, null, null, null, filters }),
        };

    private static byte[] ColoredMessage(string color, string body)
    {
        var encoder = new Amqp1Encoder();
        encoder.WriteValue(new Amqp1Described(Amqp1Descriptor.ApplicationProperties, new Amqp1Map([new("color", color)])));
        encoder.WriteValue(new Amqp1Described(Amqp1Descriptor.Data, Encoding.UTF8.GetBytes(body)));
        return encoder.Written.ToArray();
    }
}
