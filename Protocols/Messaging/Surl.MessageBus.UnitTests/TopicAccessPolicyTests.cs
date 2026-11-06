using System.Text;

namespace Surl.MessageBus;

[TestClass]
public sealed class TopicAccessPolicyTests
{
    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void Subscribe_AnAccountWithPubOnly_IsAccessDenied()
    {
        var bus = Bus(new TopicAccessGrant("shop/#", "alice", TopicAccessRights.Publish));

        var result = bus.Connect("alice").Subscribe("shop/orders", _ => { });

        Assert.AreEqual(MessageBusOutcome.AccessDenied, result.Outcome);
        Assert.IsNull(result.Subscription);
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Publish(Message("shop/orders"), "alice").Outcome);
    }

    [TestMethod]
    public void Publish_AnAccountWithSubOnly_IsAccessDeniedAndNothingIsRetainedDeliveredOrLogged()
    {
        var bus = Bus(new TopicAccessGrant("shop/#", "bob", TopicAccessRights.Subscribe));
        var received = new List<BusMessage>();
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Connect("bob").Subscribe("shop/#", received.Add).Outcome);

        var result = bus.Publish(Message("shop/orders") with { Retain = true }, "bob");

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.AccessDenied, 0), result);
        Assert.IsEmpty(received);
        Assert.IsEmpty(bus.GetLogTopics());
        Assert.IsEmpty(bus.Connect("bob").Subscribe("shop/#", _ => { }, includeRetained: true).RetainedMessages);
    }

    [TestMethod]
    public void PublishAndSubscribe_AnUnlistedOrAnonymousCaller_IsAccessDeniedOnceAnyGrantIsConfigured()
    {
        var bus = Bus(new TopicAccessGrant("#", "alice", TopicAccessRights.PublishAndSubscribe));

        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.Publish(Message("t"), "mallory").Outcome);
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.Publish(Message("t")).Outcome);
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.Connect("mallory").Subscribe("t", _ => { }).Outcome);
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.Connect().Subscribe("t", _ => { }).Outcome);
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Publish(Message("t"), "alice").Outcome);
    }

    [TestMethod]
    public void PublishAndSubscribe_NothingConfigured_EveryCallerIsAllowed()
    {
        var bus = new MessageBus(clock);

        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Publish(Message("t"), "anyone").Outcome);
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Connect().Subscribe("#", _ => { }).Outcome);
        Assert.IsNull(bus.Connect().Account);
        Assert.AreEqual("alice", bus.Connect("alice").Account);
    }

    [TestMethod]
    public void PublishAndSubscribe_GrantedToEveryone_AllowsAnonymousCallers()
    {
        var bus = Bus(new TopicAccessGrant("public/#", TopicAccessGrant.Everyone, TopicAccessRights.PublishAndSubscribe));

        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Connect().Subscribe("public/+", _ => { }).Outcome);
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Publish(Message("public/x")).Outcome);
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.Publish(Message("private/x")).Outcome);
    }

    [TestMethod]
    public void Subscribe_AFilterTheGrantDoesNotCover_IsAccessDenied()
    {
        var bus = Bus(
            new TopicAccessGrant("a/+", "alice", TopicAccessRights.Subscribe),
            new TopicAccessGrant("b/#", "alice", TopicAccessRights.Subscribe),
            new TopicAccessGrant("#", "carol", TopicAccessRights.Subscribe),
            new TopicAccessGrant("+/x", "dave", TopicAccessRights.Subscribe));

        Assert.AreEqual(MessageBusOutcome.Succeeded, Subscribe(bus, "alice", "a/x"));
        Assert.AreEqual(MessageBusOutcome.Succeeded, Subscribe(bus, "alice", "a/+"));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, Subscribe(bus, "alice", "a/#"));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, Subscribe(bus, "alice", "a"));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, Subscribe(bus, "alice", "a/x/y"));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, Subscribe(bus, "alice", "+/x"));
        Assert.AreEqual(MessageBusOutcome.Succeeded, Subscribe(bus, "alice", "b"));
        Assert.AreEqual(MessageBusOutcome.Succeeded, Subscribe(bus, "alice", "b/#"));
        Assert.AreEqual(MessageBusOutcome.Succeeded, Subscribe(bus, "alice", "b/+/c/#"));
        Assert.AreEqual(MessageBusOutcome.Succeeded, Subscribe(bus, "carol", "#"));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, Subscribe(bus, "carol", "$SYS/#"));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, Subscribe(bus, "dave", "$SYS/x"));
        Assert.AreEqual(MessageBusOutcome.Succeeded, Subscribe(bus, "dave", "y/x"));
        Assert.AreEqual(MessageBusOutcome.InvalidFilter, Subscribe(bus, "dave", "a#"));
    }

    [TestMethod]
    public void Publish_ADollarTopic_IsNotMatchedByAWildcardGrant()
    {
        var bus = Bus(new TopicAccessGrant("#", "alice", TopicAccessRights.Publish), new TopicAccessGrant("$SYS/#", "bob", TopicAccessRights.Publish));

        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.Publish(Message("$SYS/x"), "alice").Outcome);
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Publish(Message("$SYS/x"), "bob").Outcome);
        Assert.AreEqual(MessageBusOutcome.InvalidTopic, bus.Publish(Message("a/+"), "bob").Outcome);
    }

    [TestMethod]
    public void Queues_GrantedAsDollarQueue_AreSentToAndConsumedOnlyWithTheirRights()
    {
        var bus = new MessageBus(clock, configuration: new()
        {
            Queues = [new DeclaredQueue("orders")],
            AccessGrants =
            [
                new TopicAccessGrant("$queue/orders", "sender", TopicAccessRights.Publish),
                new TopicAccessGrant("$queue/+", "reader", TopicAccessRights.Subscribe),
                new TopicAccessGrant("#", "everything", TopicAccessRights.PublishAndSubscribe),
            ],
        });

        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("orders", Message("t"), "sender"));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.PublishToQueue("orders", Message("t"), "reader"));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.PublishToQueue("orders", Message("t"), "everything"));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.PublishToQueue("missing", Message("t"), "reader"));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.PublishToQueue("orders", Message("t")));
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Connect("reader").Consume("orders", _ => { }).Outcome);
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.Connect("sender").Consume("orders", _ => { }).Outcome);
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.Connect("everything").Consume("missing", _ => { }).Outcome);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.Connect("reader").Consume("missing", _ => { }).Outcome);
    }

    [TestMethod]
    public void BindQueue_AFilterTheCallerMayNotSubscribeTo_IsAccessDeniedButUnbindIsNot()
    {
        var bus = Bus(new TopicAccessGrant("shop/#", "alice", TopicAccessRights.Subscribe));
        using var connection = bus.Connect("alice");
        connection.DeclareQueue("q");

        Assert.AreEqual(MessageBusOutcome.Succeeded, connection.BindQueue("q", "shop/orders"));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, connection.BindQueue("q", "other"));
        Assert.AreEqual(MessageBusOutcome.AccessDenied, connection.BindQueue("missing", "other"));
        Assert.AreEqual(MessageBusOutcome.Succeeded, connection.UnbindQueue("q", "other"));
    }

    [TestMethod]
    public void MayUseGroup_DollarGroup_IsGrantedByALiteralOrPlusSubGrantNeverByHashOrPubAndOpenWithNoGrants()
    {
        var bus = Bus(
            new TopicAccessGrant("$group/orders", "literal", TopicAccessRights.Subscribe),
            new TopicAccessGrant("$group/+", "plus", TopicAccessRights.PublishAndSubscribe),
            new TopicAccessGrant("#", "hash", TopicAccessRights.PublishAndSubscribe),
            new TopicAccessGrant("$group/orders", "pub", TopicAccessRights.Publish),
            new TopicAccessGrant("$group/+", TopicAccessGrant.Everyone, TopicAccessRights.Publish));

        Assert.IsTrue(bus.MayUseGroup("literal", "orders"));
        Assert.IsFalse(bus.MayUseGroup("literal", "other"));
        Assert.IsTrue(bus.MayUseGroup("plus", "other"));
        Assert.IsTrue(bus.MayUseGroup("plus", "a/b"));
        Assert.IsFalse(bus.MayUseGroup("hash", "orders"));
        Assert.IsFalse(bus.MayUseGroup("pub", "orders"));
        Assert.IsFalse(bus.MayUseGroup(null, "orders"));
        Assert.IsTrue(new MessageBus(clock).MayUseGroup(null, "anything"));
        Assert.IsTrue(new MessageBus(clock).MayUseGroup("anyone", "a/b"));
    }

    private static BusMessage Message(string topic) => new(topic, Encoding.UTF8.GetBytes("x"));

    private static MessageBusOutcome Subscribe(MessageBus bus, string account, string filter) =>
        bus.Connect(account).Subscribe(filter, _ => { }).Outcome;

    private MessageBus Bus(params TopicAccessGrant[] grants) => new(clock, configuration: new() { AccessGrants = grants });
}
