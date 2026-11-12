using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>Nodes on the shared message bus, and who may use them (ADR-0107 decision 4; ADR-0102 decision 3.4).</summary>
[TestClass]
public sealed class Amqp1BusNodeDirectoryTests
{
    [TestMethod]
    public void Resolve_DynamicNode_IsAnExclusiveAutoDeletedQueue()
    {
        var bus = Bus();
        var connection = bus.Connect("alice");

        var resolved = new Amqp1BusNodeDirectory(bus, connection, new UnitTestSeededIdentifierRandom(1)).Resolve(new(null, true, "alice"));

        StringAssert.Matches(resolved.CanonicalAddress, new System.Text.RegularExpressions.Regex("^/queue/surl-dynamic-[0-9a-f]{32}$"));
        var name = resolved.CanonicalAddress![Amqp1NodeAddress.QueuePrefix.Length..];
        Assert.AreEqual(MessageBusOutcome.QueueLocked, bus.Connect("bob").Consume(name, _ => { }).Outcome);
    }

    [TestMethod]
    public void DynamicQueue_SameSeededIdentifiers_GetsTheSameName()
    {
        var first = new Amqp1BusNodeDirectory(Bus(), Bus().Connect("alice"), new UnitTestSeededIdentifierRandom(3)).Resolve(new(null, true, "alice"));
        var second = new Amqp1BusNodeDirectory(Bus(), Bus().Connect("alice"), new UnitTestSeededIdentifierRandom(3)).Resolve(new(null, true, "alice"));
        var other = new Amqp1BusNodeDirectory(Bus(), Bus().Connect("alice"), new UnitTestSeededIdentifierRandom(4)).Resolve(new(null, true, "alice"));

        Assert.AreEqual(first.CanonicalAddress, second.CanonicalAddress);
        Assert.AreNotEqual(first.CanonicalAddress, other.CanonicalAddress);
    }

    [TestMethod]
    public void Resolve_DynamicNodePastTheQueueBound_IsResourceLimitExceeded()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), maxQueues: 1);
        var connection = bus.Connect("alice");
        connection.DeclareQueue("taken");

        Assert.AreEqual(Amqp1NodeResolution.ResourceLimitExceeded, new Amqp1BusNodeDirectory(bus, connection, new UnitTestSeededIdentifierRandom(1)).Resolve(new(null, true, "alice")));
    }

    [TestMethod]
    [DataRow("/topic/news", false, "/topic/news", DisplayName = "a topic to send to")]
    [DataRow("/topic/news/#", true, "/topic/news/#", DisplayName = "a filter to receive from")]
    [DataRow("/topic/alias", false, "/topic/real", DisplayName = "an aliased topic")]
    public void Resolve_Topic_IsItsCanonicalTopic(string address, bool clientIsReceiver, string expected)
    {
        var bus = Bus(new MessageBusConfiguration { TopicMaps = [new TopicMap("amqp1", "/topic/alias", "mqtt", "real")] });

        var resolved = new Amqp1BusNodeDirectory(bus, bus.Connect("alice"), new UnitTestSeededIdentifierRandom(1)).Resolve(new(address, clientIsReceiver, "alice"));

        Assert.AreEqual(Amqp1NodeResolution.Found(expected), resolved);
    }

    [TestMethod]
    public void Resolve_TopicNotCanonical_IsNotFound()
    {
        var bus = Bus();

        Assert.AreEqual(Amqp1NodeResolution.NotFound, new Amqp1BusNodeDirectory(bus, bus.Connect(), new UnitTestSeededIdentifierRandom(1)).Resolve(new("/topic/a+b", false, null)));
    }

    [TestMethod]
    public void Resolve_TopicTheCallerMayNotPublishOn_IsUnauthorized()
    {
        var bus = Bus(new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("#", "alice", TopicAccessRights.Subscribe)] });

        Assert.AreEqual(Amqp1NodeResolution.Unauthorized, new Amqp1BusNodeDirectory(bus, bus.Connect("alice"), new UnitTestSeededIdentifierRandom(1)).Resolve(new("/topic/news", false, "alice")));
    }

    [TestMethod]
    [DataRow(true, TopicAccessRights.Publish, DisplayName = "a receiver without sub")]
    [DataRow(false, TopicAccessRights.Subscribe, DisplayName = "a sender without pub")]
    public void Resolve_QueueTheCallerMayNotUse_IsUnauthorized(bool clientIsReceiver, TopicAccessRights granted)
    {
        var bus = Bus(new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("$queue/#", "alice", granted)] });

        Assert.AreEqual(Amqp1NodeResolution.Unauthorized, new Amqp1BusNodeDirectory(bus, bus.Connect("alice"), new UnitTestSeededIdentifierRandom(1)).Resolve(new("/queue/orders", clientIsReceiver, "alice")));
    }

    [TestMethod]
    public void Resolve_Queue_IsDeclaredDurableOnFirstAttach()
    {
        var bus = Bus();
        var directory = new Amqp1BusNodeDirectory(bus, bus.Connect("alice"), new UnitTestSeededIdentifierRandom(1));

        Assert.AreEqual(Amqp1NodeResolution.Found("/queue/orders"), directory.Resolve(new("/queue/orders", false, "alice")));
        Assert.AreEqual(Amqp1NodeResolution.Found("/queue/orders"), directory.Resolve(new("/queue/orders", true, "alice")));
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Connect().DeclareQueue("orders", new BusQueueFlags(Durable: true)).Outcome);
    }

    [TestMethod]
    public void Resolve_QueuePastTheQueueBound_IsResourceLimitExceeded()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), maxQueues: 1);
        var connection = bus.Connect("alice");
        connection.DeclareQueue("taken");

        Assert.AreEqual(Amqp1NodeResolution.ResourceLimitExceeded, new Amqp1BusNodeDirectory(bus, connection, new UnitTestSeededIdentifierRandom(1)).Resolve(new("/queue/orders", false, "alice")));
    }

    [TestMethod]
    public void ConstructorAndResolve_NullArgument_Throw()
    {
        var bus = Bus();

        Assert.ThrowsExactly<ArgumentNullException>(() => new Amqp1BusNodeDirectory(null!, bus.Connect(), new UnitTestSeededIdentifierRandom(1)));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Amqp1BusNodeDirectory(bus, null!, new UnitTestSeededIdentifierRandom(1)));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Amqp1BusNodeDirectory(bus, bus.Connect(), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Amqp1BusNodeDirectory(bus, bus.Connect(), new UnitTestSeededIdentifierRandom(1)).Resolve(null!));
    }

    private static MessageBus.MessageBus Bus(MessageBusConfiguration? configuration = null) =>
        new(new ManualTimeProvider(), configuration: configuration);
}
