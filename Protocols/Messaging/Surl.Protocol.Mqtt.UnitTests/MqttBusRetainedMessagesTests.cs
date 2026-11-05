using System.Text;
using Surl.MessageBus;

namespace Surl.Protocol.Mqtt;

[TestClass]
public sealed class MqttBusRetainedMessagesTests
{
    [TestMethod]
    public void MatchingAny_OverlappingAndRepeatedFilters_GivesEachTopicOnceInOrdinalOrder()
    {
        var bus = TestMessageBus.New();
        bus.Retain("b", "2"u8);
        bus.Retain("a/x", "1"u8);
        bus.Retain("c", "3"u8);

        var matching = MqttBusRetainedMessages.MatchingAny(bus, ["b", "a/+", "#", "b"]);

        CollectionAssert.AreEqual(new[] { "a/x", "b", "c" }, matching.Select(message => message.Key).ToArray());
        CollectionAssert.AreEqual(new[] { "1", "2", "3" }, matching.Select(message => Encoding.ASCII.GetString(message.Value)).ToArray());
    }

    [TestMethod]
    public void MatchingAny_MoreFiltersThanABusConnectionMaySubscribe_ReadsThemAll()
    {
        var bus = TestMessageBus.New();
        bus.Retain("t", "hi"u8);
        var filters = Enumerable.Range(0, MessageBus.MessageBus.DefaultMaxSubscriptionsPerConnection + 1).Select(index => $"f/{index}").Append("t").ToArray();

        var matching = MqttBusRetainedMessages.MatchingAny(bus, filters);

        Assert.AreEqual("t", matching.Single().Key);
    }

    [TestMethod]
    public void MatchingAny_LeavesNoSubscriptionBehind()
    {
        var bus = TestMessageBus.New();
        MqttBusRetainedMessages.MatchingAny(bus, ["t"]);

        var published = bus.Publish(new BusMessage("t", "hi"u8.ToArray()));

        Assert.AreEqual(0, published.DeliveredSubscriptionCount);
    }

    [TestMethod]
    public void IgnoreLiveMessage_AMessage_ReturnsWithoutThrowing()
    {
        MqttBusRetainedMessages.IgnoreLiveMessage(new BusMessage("t", "hi"u8.ToArray()));
    }
}
