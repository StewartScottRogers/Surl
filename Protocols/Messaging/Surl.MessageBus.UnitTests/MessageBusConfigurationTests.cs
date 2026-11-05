using System.Text;

namespace Surl.MessageBus;

[TestClass]
public sealed class MessageBusConfigurationTests
{
    private readonly PersistedBusFixture fixture = new();

    [TestMethod]
    public void Constructor_NoConfiguration_IsNoneAndHoldsNothing()
    {
        var bus = new MessageBus(fixture.Clock);

        Assert.AreSame(MessageBusConfiguration.None, bus.Configuration);
        Assert.IsEmpty(bus.GetLogTopics());
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.PublishToQueue("q", Message("t", "x")));
    }

    [TestMethod]
    public void Publish_OnADeclaredRetainTopic_IsRetainedWithoutTheRetainFlag()
    {
        var bus = Bus(new() { Topics = [new DeclaredTopic("shop/#", retain: true)] });
        bus.Publish(Message("shop/orders", "a"));
        bus.Publish(Message("other", "b"));

        var retained = bus.Connect().Subscribe("#", _ => { }, includeRetained: true).RetainedMessages;

        Assert.HasCount(1, retained);
        Assert.AreEqual("shop/orders", retained[0].Topic);
        Assert.IsTrue(retained[0].Retain);
    }

    [TestMethod]
    public void Publish_OnADeclaredRetainTopicWithAnEmptyPayload_RemovesTheRetainedMessage()
    {
        var bus = Bus(new() { Topics = [new DeclaredTopic("t", retain: true)] });
        bus.Publish(Message("t", "a"));
        bus.Publish(new BusMessage("t", ReadOnlyMemory<byte>.Empty));

        Assert.IsEmpty(bus.Connect().Subscribe("t", _ => { }, includeRetained: true).RetainedMessages);
    }

    [TestMethod]
    public void Publish_OnADeclaredTopicWithAQosCeiling_IsCarriedAtNoMoreThanTheCeiling()
    {
        var bus = Bus(new()
        {
            Topics =
            [
                new DeclaredTopic("a/+", maximumQualityOfService: BusQualityOfService.AtLeastOnce),
                new DeclaredTopic("a/b", maximumQualityOfService: BusQualityOfService.AtMostOnce),
                new DeclaredTopic("a/#"),
            ],
        });
        var received = new List<BusMessage>();
        bus.Connect().Subscribe("#", received.Add);

        bus.Publish(Message("a/c", "1") with { QualityOfService = BusQualityOfService.ExactlyOnce });
        bus.Publish(Message("a/b", "2") with { QualityOfService = BusQualityOfService.ExactlyOnce });
        bus.Publish(Message("a/c", "3") with { QualityOfService = BusQualityOfService.AtMostOnce });
        bus.Publish(Message("z", "4") with { QualityOfService = BusQualityOfService.ExactlyOnce });

        CollectionAssert.AreEqual(
            new[] { BusQualityOfService.AtLeastOnce, BusQualityOfService.AtMostOnce, BusQualityOfService.AtMostOnce, BusQualityOfService.ExactlyOnce },
            received.Select(message => message.QualityOfService).ToArray());
        Assert.IsFalse(received[0].Retain);
    }

    [TestMethod]
    public void Constructor_DeclaredTopicsWithoutWildcards_HaveEmptyLogsFromStart()
    {
        var bus = Bus(new() { Topics = [new DeclaredTopic("orders"), new DeclaredTopic("orders", retain: true), new DeclaredTopic("shop/+")] });

        CollectionAssert.AreEqual(new[] { "orders" }, bus.GetLogTopics().ToArray());
        Assert.AreEqual(new BusLogOffsets(MessageBusOutcome.Succeeded, 0, 0), bus.GetLogOffsets("orders"));
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Publish(Message("undeclared", "x")).Outcome);
    }

    [TestMethod]
    public void Constructor_DeclaredQueues_ExistAtStartNeverExclusiveAndTakeMessages()
    {
        var bus = Bus(new() { Queues = [new DeclaredQueue("orders"), new DeclaredQueue("archive", durable: true)] });
        using var connection = bus.Connect();

        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("orders", Message("t", "x")));
        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.Succeeded, 1, 0), connection.DeclareQueue("orders"));
        Assert.AreEqual(MessageBusOutcome.Succeeded, connection.DeclareQueue("archive", new BusQueueFlags(Durable: true)).Outcome);
        Assert.AreEqual(MessageBusOutcome.QueueFlagsDiffer, connection.DeclareQueue("orders", new BusQueueFlags(Exclusive: true)).Outcome);
    }

    [TestMethod]
    public void DeleteQueue_ADeclaredQueue_IsAccessDeniedAndTheQueueStays()
    {
        var bus = Bus(new() { Queues = [new DeclaredQueue("orders")] });
        using var connection = bus.Connect();
        connection.DeclareQueue("mine");

        Assert.AreEqual(MessageBusOutcome.AccessDenied, connection.DeleteQueue("orders"));
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("orders", Message("t", "x")));
        Assert.AreEqual(MessageBusOutcome.Succeeded, connection.DeleteQueue("mine"));
    }

    [TestMethod]
    public async Task LoadAsync_ADurableDeclaredQueue_SurvivesAReloadWithItsMessages()
    {
        var configuration = new MessageBusConfiguration { Queues = [new DeclaredQueue("orders", durable: true), new DeclaredQueue("scratch")] };
        var bus = await LoadBusAsync(configuration);
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("orders", Message("t", "kept")));
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("scratch", Message("t", "lost")));
        await bus.SaveChangesAsync();

        var loaded = await LoadBusAsync(configuration);

        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.Succeeded, 1, 0), loaded.Connect().DeclareQueue("orders", new BusQueueFlags(Durable: true)));
        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.Succeeded, 0, 0), loaded.Connect().DeclareQueue("scratch"));
        var delivered = new List<BusQueueDelivery>();
        loaded.Connect().Consume("orders", delivered.Add).Consumer!.Start();
        Assert.AreEqual("kept", PersistedBusFixture.Text(delivered.Single().Message));
    }

    [TestMethod]
    public async Task LoadAsync_AQueuePersistedDurableButNowDeclaredNotDurable_IsMadeAgainEmpty()
    {
        var bus = await LoadBusAsync(new() { Queues = [new DeclaredQueue("orders", durable: true)] });
        bus.PublishToQueue("orders", Message("t", "old"));
        await bus.SaveChangesAsync();

        var loaded = await LoadBusAsync(new() { Queues = [new DeclaredQueue("orders")] });

        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.Succeeded, 0, 0), loaded.Connect().DeclareQueue("orders"));
        Assert.IsEmpty(fixture.Entries(Path.Join(PersistedBusFixture.SurlFolderPath, "bus", "queues", "messages")));
    }

    [TestMethod]
    public async Task LoadAsync_ADeclaredTopicWithASavedLog_KeepsTheLog()
    {
        var configuration = new MessageBusConfiguration { Topics = [new DeclaredTopic("orders")] };
        var bus = await LoadBusAsync(configuration);
        bus.Publish(Message("orders", "a"));
        await bus.SaveChangesAsync();

        var loaded = await LoadBusAsync(configuration);

        Assert.AreEqual(new BusLogOffsets(MessageBusOutcome.Succeeded, 0, 1), loaded.GetLogOffsets("orders"));
    }

    [TestMethod]
    public async Task LoadAsync_ABusHoldingMoreThanItsEmptyDeclarations_Throws()
    {
        var configuration = new MessageBusConfiguration { Topics = [new DeclaredTopic("t")], Queues = [new DeclaredQueue("q")] };
        var undeclaredLog = Bus(configuration);
        undeclaredLog.Publish(Message("u", "x"));
        var declaredLogFilled = Bus(configuration);
        declaredLogFilled.Publish(Message("t", "x"));
        var undeclaredQueue = Bus(configuration);
        undeclaredQueue.Connect().DeclareQueue("other");
        var declaredQueueFilled = Bus(configuration);
        declaredQueueFilled.PublishToQueue("q", Message("t", "x"));

        foreach (var bus in new[] { undeclaredLog, declaredLogFilled, undeclaredQueue, declaredQueueFilled })
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => bus.LoadAsync(fixture.Files));
        }
    }

    [TestMethod]
    public async Task LoadAsync_SavedTopicsOrQueuesWithTheDeclaredOnesPastTheBounds_Throws()
    {
        var bus = await fixture.LoadBusAsync();
        bus.Publish(Message("saved", "x"));
        bus.Connect().DeclareQueue("saved", new BusQueueFlags(Durable: true));
        await bus.SaveChangesAsync();

        var topics = new MessageBus(fixture.Clock, maxTopics: 1, configuration: new() { Topics = [new DeclaredTopic("declared")] });
        var queues = new MessageBus(fixture.Clock, maxQueues: 1, configuration: new() { Queues = [new DeclaredQueue("declared")] });

        await fixture.LoadFailureAsync(topics);
        await fixture.LoadFailureAsync(queues);
    }

    [TestMethod]
    public void Constructor_DeclarationsPastTheBoundsOrAQueueTwice_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MessageBus(fixture.Clock, maxTopics: 1, configuration: new() { Topics = [new DeclaredTopic("a"), new DeclaredTopic("b")] }));
        Assert.ThrowsExactly<ArgumentException>(() => new MessageBus(fixture.Clock, maxQueues: 1, configuration: new() { Queues = [new DeclaredQueue("a"), new DeclaredQueue("b")] }));
        Assert.ThrowsExactly<ArgumentException>(() => Bus(new() { Queues = [new DeclaredQueue("a"), new DeclaredQueue("a", durable: true)] }));
        Assert.ThrowsExactly<ArgumentException>(() => Bus(new() { TopicMaps = [new TopicMap("amqp", "a", "smtp", "b")] }));
    }

    [TestMethod]
    public void Declarations_InvalidValues_Throw()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DeclaredTopic("a/b#"));
        Assert.ThrowsExactly<ArgumentException>(() => new DeclaredQueue(string.Empty));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DeclaredQueue(null!));
        Assert.ThrowsExactly<ArgumentException>(() => new TopicAccessGrant(string.Empty, "alice", TopicAccessRights.Publish));
        Assert.ThrowsExactly<ArgumentException>(() => new TopicAccessGrant("t", string.Empty, TopicAccessRights.Publish));
        Assert.ThrowsExactly<ArgumentException>(() => new TopicAccessGrant("t", "alice", TopicAccessRights.None));
        Assert.ThrowsExactly<ArgumentException>(() => new TopicAccessGrant("t", "alice", (TopicAccessRights)4));
    }

    [TestMethod]
    public void GetTopicAccess_NoGrants_AllowsEverything()
    {
        var bus = Bus(MessageBusConfiguration.None);

        Assert.AreEqual(TopicAccessRights.PublishAndSubscribe, bus.GetTopicAccess(null, "t"));
        Assert.ThrowsExactly<ArgumentNullException>(() => bus.GetTopicAccess(null, null!));
    }

    [TestMethod]
    public void GetTopicAccess_Grants_AnswerEachRightApart()
    {
        var bus = Bus(new MessageBusConfiguration
        {
            AccessGrants =
            [
                new TopicAccessGrant("a/#", "alice", TopicAccessRights.Publish),
                new TopicAccessGrant("a/b", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe),
            ],
        });

        Assert.AreEqual(TopicAccessRights.PublishAndSubscribe, bus.GetTopicAccess("alice", "a/b"));
        Assert.AreEqual(TopicAccessRights.Publish, bus.GetTopicAccess("alice", "a/c"));
        Assert.AreEqual(TopicAccessRights.Subscribe, bus.GetTopicAccess(null, "a/b"));
        Assert.AreEqual(TopicAccessRights.None, bus.GetTopicAccess("bob", "x"));
    }

    [TestMethod]
    public void Declarations_ValidValues_KeepWhatTheyWereGiven()
    {
        var topic = new DeclaredTopic("a/+", retain: true, BusQualityOfService.AtLeastOnce);
        var queue = new DeclaredQueue("q", durable: true);
        var grant = new TopicAccessGrant("a/#", TopicAccessGrant.Everyone, TopicAccessRights.PublishAndSubscribe);

        Assert.AreEqual(("a/+", true, (BusQualityOfService?)BusQualityOfService.AtLeastOnce), (topic.Filter, topic.Retain, topic.MaximumQualityOfService));
        Assert.AreEqual(("q", true), (queue.Name, queue.Durable));
        Assert.AreEqual(("a/#", "@everyone", TopicAccessRights.PublishAndSubscribe), (grant.Filter, grant.Member, grant.Rights));
    }

    private static BusMessage Message(string topic, string payload) => new(topic, Encoding.UTF8.GetBytes(payload));

    private MessageBus Bus(MessageBusConfiguration configuration) => new(fixture.Clock, configuration: configuration);

    private async Task<MessageBus> LoadBusAsync(MessageBusConfiguration configuration)
    {
        var bus = Bus(configuration);
        Assert.AreEqual(BusSaveResult.Saved, await bus.LoadAsync(fixture.Files));
        return bus;
    }
}
