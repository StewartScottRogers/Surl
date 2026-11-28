using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// No-local on a kept topic subscription (the Apache filter registry's <c>apache.org:no-local-filter:list</c>;
/// Jakarta Messaging 3.1 section 8.3.3; ADR-0146 Amendment 3; matrix row <c>Amqp1:Messaging-3.5.8-3</c>): an
/// unshared one takes nothing an AMQP 1.0 connection with its <c>container-id</c> publishes, and a shared one
/// does not apply the filter. The connections here open with the <c>container-id</c> <c>client</c> unless a
/// test says otherwise.
/// </summary>
public sealed partial class Amqp1ContainerConnectionTests
{
    [TestMethod]
    [DataRow("never")]
    [DataRow("session-end")]
    [DataRow("connection-close")]
    public async Task ServeAsync_UnsharedKeptSubscriptionWithNoLocal_TakesOnlyWhatAnotherContainerIdPublishes(string policy)
    {
        var bus = Bus();
        var connection = new InMemoryConnection(
            [
                Open(),
                Begin(),
                BusFrame(NoLocalAttach(0, policy)),
                BusFrame(new Amqp1Detach(0)),
                BusFrame(SenderAttach(1, "/topic/news")),
                Publication(1, 0, "own while detached"),
                BusFrame(NoLocalAttach(2, policy)),
                BusFrame(Credit(2, deliveryCount: 0, credit: 10)),
                Publication(1, 1, "own while attached", settled: false),
            ],
            peerHalfClosesWhenExhausted: false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = new Amqp1ContainerConnection(connection, Context(new RecordingExchangeLog(), cancellation.Token), Options(), bus, "alice").ServeAsync();
        await WaitUntilWrittenAsync(async () => (await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Any(frame => frame.Performative is Amqp1Disposition));

        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(SenderAttach(0, "/topic/news")), Publication(0, 0, "same container-id"));
        await ServeOnBusAsync(bus, OpenAs("other"), Begin(), BusFrame(SenderAttach(0, "/topic/news")), Publication(0, 0, "other container-id"));

        await WaitUntilWrittenAsync(async () => Transfers(await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Count > 0);
        await cancellation.CancelAsync();
        await serving;
        Assert.AreEqual("other container-id", DataOf(Transfers(await ReadWrittenWithPayloadsAsync(connection.WrittenBytes)).Single().Payload));
    }

    [TestMethod]
    public async Task ServeAsync_DurableSubscriptionWithNoLocal_KeepsWhatALaterConnectionOfItsContainerIdPublishesOutOfTheQueue()
    {
        var bus = Bus();
        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(NoLocalAttach(0, "never")), BusFrame(new Amqp1Detach(0)));
        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(SenderAttach(0, "/topic/news")), Publication(0, 0, "same container-id"));
        await ServeOnBusAsync(bus, OpenAs("other"), Begin(), BusFrame(SenderAttach(0, "/topic/news")), Publication(0, 0, "other container-id"));
        bus.Publish(new BusMessage("news", "from the host"u8.ToArray()));

        Assert.AreEqual(2, bus.Connect("alice").InspectQueue(DurableQueue).ReadyMessageCount);
    }

    [TestMethod]
    public async Task ServeAsync_KeptSubscriptionWithoutNoLocal_ReceivesItsOwnConnectionsPublish()
    {
        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(DurableAttach(0, "/topic/news", "never")),
            BusFrame(SenderAttach(1, "/topic/news")),
            Publication(1, 0, "own"),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)));

        Assert.AreEqual("own", DataOf(Transfers(written).Single().Payload));
    }

    [TestMethod]
    [DataRow("never", false, DisplayName = "durable")]
    [DataRow("session-end", false, DisplayName = "session-end")]
    [DataRow("never", true, DisplayName = "global")]
    public async Task ServeAsync_SharedKeptSubscriptionAskingNoLocal_IsNotEchoedItAndReceivesItsOwnConnectionsPublish(string policy, bool global)
    {
        string[] capabilities = global ? ["shared", "global"] : ["shared"];

        var (written, _) = await ServeOnBusAsync(
            Bus(),
            Open(),
            Begin(),
            BusFrame(DurableAttach(0, "/topic/news", policy, "durable", NoLocal(), capabilities)),
            BusFrame(SenderAttach(1, "/topic/news")),
            Publication(1, 0, "own"),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)));

        Assert.IsNull(Fields(((Amqp1Attach)written[2].Performative!).Source)[7]);
        Assert.AreEqual("own", DataOf(Transfers(written).Single().Payload));
    }

    [TestMethod]
    public async Task ServeAsync_LookupOfASharedSubscriptionKeptWithNoLocal_IsNotEchoedIt()
    {
        var bus = Bus();
        var queueName = Amqp1KeptSubscription.QueueNameFor("client", "durable", isShared: true, isGlobal: false);
        var asked = new Amqp1Terminus("/topic/news", false, false) { ExpiryPolicy = "never", IsShared = true };
        var owner = bus.Connect("alice");
        owner.DeclareQueue(queueName, new BusQueueFlags(Durable: true), Amqp1KeptSubscription.Plan("client", "durable", asked, "news", NoLocal()).Description);
        owner.BindQueue(queueName, "news");

        var (written, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news") with { Source = null }));

        Assert.IsNull(Fields(((Amqp1Attach)written[2].Performative!).Source)[7]);
    }

    [TestMethod]
    public async Task ServeAsync_ReattachWithTheSameNoLocal_ResumesTheSubscriptionStillWithNoLocal()
    {
        var bus = Bus();
        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(NoLocalAttach(0, "never")), BusFrame(new Amqp1Detach(0)));
        bus.Publish(new BusMessage("news", "kept"u8.ToArray()));

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(NoLocalAttach(0, "never")),
            BusFrame(SenderAttach(1, "/topic/news")),
            Publication(1, 0, "own"),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)));
        var (lookup, _) = await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(DurableAttach(0, "/topic/news") with { Source = null }));

        Assert.AreEqual("kept", DataOf(Transfers(written).Single().Payload));
        var echoed = (Amqp1Map)Fields(((Amqp1Attach)lookup[2].Performative!).Source)[7]!;
        Assert.AreEqual(Amqp1SourceFilters.NoLocalFilter, ((Amqp1Described)echoed.Entries.Single().Value!).Descriptor);
    }

    [TestMethod]
    public async Task ServeAsync_ReattachWithoutNoLocal_ReplacesTheSubscriptionKeptWithIt()
    {
        var bus = Bus();
        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(NoLocalAttach(0, "never")), BusFrame(new Amqp1Detach(0)));
        bus.Publish(new BusMessage("news", "replaced away"u8.ToArray()));

        var (written, _) = await ServeOnBusAsync(
            bus,
            Open(),
            Begin(),
            BusFrame(DurableAttach(0, "/topic/news", "never")),
            BusFrame(SenderAttach(1, "/topic/news")),
            Publication(1, 0, "own"),
            BusFrame(Credit(0, deliveryCount: 0, credit: 10)));

        Assert.AreEqual("own", DataOf(Transfers(written).Single().Payload));
    }

    [TestMethod]
    public async Task ServeAsync_ReattachOfASubscriptionKeptExcludingNobody_ExcludesItsContainerIdFromThen()
    {
        var bus = Bus();
        var asked = new Amqp1Terminus("/topic/news", false, false) { ExpiryPolicy = "never" };
        var owner = bus.Connect("alice");
        owner.DeclareQueue(DurableQueue, new BusQueueFlags(Durable: true), Amqp1KeptSubscription.Plan("client", "durable", asked, "news", NoLocal()).Description);
        owner.BindQueue(DurableQueue, "news");

        await ServeOnBusAsync(bus, Open(), Begin(), BusFrame(NoLocalAttach(0, "never")), BusFrame(SenderAttach(1, "/topic/news")), Publication(1, 0, "own"));

        Assert.AreEqual(0, owner.InspectQueue(DurableQueue).ReadyMessageCount);
    }

    private static Amqp1Map NoLocal() =>
        new([new(new Amqp1Symbol("no-local"), new Amqp1Described(Amqp1SourceFilters.NoLocalFilter, new List<object?>()))]);

    private static Amqp1Attach NoLocalAttach(uint handle, string policy) =>
        DurableAttach(handle, "/topic/news", policy, "durable", NoLocal());

    private static byte[] OpenAs(string containerId) => Amqp1Frame.Carrying(0, new Amqp1Open(containerId)).Encode();

    private static byte[] Publication(uint handle, uint deliveryId, string body, bool settled = true) =>
        BusFrame(new Amqp1Transfer(handle) { DeliveryId = deliveryId, DeliveryTag = [(byte)deliveryId], Settled = settled }, ColoredMessage("red", body));
}
