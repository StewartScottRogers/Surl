using System.Text;
using Surl.MessageBus;

namespace Surl.Protocol.Mqtt;

[TestClass]
public sealed class MqttBusRetainedMessagesTests
{
    [TestMethod]
    public void Read_OverlappingAndRepeatedFilters_GivesEachTopicOnceInOrdinalOrder()
    {
        var bus = TestMessageBus.New();
        bus.Retain("b", "2"u8);
        bus.Retain("a/x", "1"u8);
        bus.Retain("c", "3"u8);

        var matching = MqttBusRetainedMessages.Read(bus, account: null, ["b", "a/+", "#", "b"]).Matching;

        CollectionAssert.AreEqual(new[] { "a/x", "b", "c" }, matching.Select(message => message.Key).ToArray());
        CollectionAssert.AreEqual(new[] { "1", "2", "3" }, matching.Select(message => Encoding.ASCII.GetString(message.Value)).ToArray());
    }

    [TestMethod]
    public void Read_MoreFiltersThanABusConnectionMaySubscribe_ReadsThemAll()
    {
        var bus = TestMessageBus.New();
        bus.Retain("t", "hi"u8);
        var filters = Enumerable.Range(0, MessageBus.MessageBus.DefaultMaxSubscriptionsPerConnection + 1).Select(index => $"f/{index}").Append("t").ToArray();

        var matching = MqttBusRetainedMessages.Read(bus, account: null, filters).Matching;

        Assert.AreEqual("t", matching.Single().Key);
    }

    [TestMethod]
    public void Read_LeavesNoSubscriptionBehind()
    {
        var bus = TestMessageBus.New();
        MqttBusRetainedMessages.Read(bus, account: null, ["t"]);

        var published = bus.Publish(new BusMessage("t", "hi"u8.ToArray()));

        Assert.AreEqual(0, published.DeliveredSubscriptionCount);
    }

    [TestMethod]
    public void Read_AnAccountGrantedOneFilterOfTwo_SaysWhichAndReadsOnlyTheGrantedOnesMessages()
    {
        var bus = new MessageBus.MessageBus(TimeProvider.System, configuration: new MessageBusConfiguration
        {
            AccessGrants = [new TopicAccessGrant("a/#", "ann", TopicAccessRights.Subscribe), new TopicAccessGrant("#", "admin", TopicAccessRights.Publish)],
        });
        bus.Publish(new BusMessage("a/b", "1"u8.ToArray()) { Retain = true }, "admin");
        bus.Publish(new BusMessage("c", "2"u8.ToArray()) { Retain = true }, "admin");

        var reading = MqttBusRetainedMessages.Read(bus, "ann", ["c", "a/b"]);

        CollectionAssert.AreEqual(new[] { false, true }, reading.Granted.ToArray());
        Assert.AreEqual("a/b", reading.Matching.Single().Key);
    }

    [TestMethod]
    public void IgnoreLiveMessage_AMessage_ReturnsWithoutThrowing()
    {
        MqttBusRetainedMessages.IgnoreLiveMessage(new BusMessage("t", "hi"u8.ToArray()));
    }
}
