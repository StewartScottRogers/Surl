using System.Text;
using Surl.MessageBus;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// Another protocol's bus message as <c>basic.deliver</c> carries it (ADR-0112 decision 6): the
/// exchange and routing key, and the header built from what it has.
/// </summary>
[TestClass]
public sealed class Amqp0BusMessagesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void ToOutgoingContent_TopicWithNoRoutingKey_IsTheDefaultExchangeAndTheQueueName()
    {
        var message = new BusMessage("a.b/c", Encoding.ASCII.GetBytes("x")) { PublishedAt = Now };

        var content = Amqp0BusMessages.ToOutgoingContent(message, "orders", Now);

        Assert.AreEqual(string.Empty, content.Exchange);
        Assert.AreEqual("orders", content.RoutingKey);
        Assert.AreEqual(0x0040, content.PropertyFlags);
        CollectionAssert.AreEqual(LongLong(Now.ToUnixTimeSeconds()), content.PropertyList.ToArray());
    }

    [TestMethod]
    public void ToOutgoingContent_EveryFieldThatFits_IsInTheHeader()
    {
        var message = new BusMessage("sensors/hall", Encoding.ASCII.GetBytes("x"))
        {
            PublishedAt = Now,
            ContentType = "text/plain",
            Headers = [new("h", "v"), new(new string('n', 256), "dropped")],
            CorrelationData = Encoding.UTF8.GetBytes("c-1"),
            ResponseTopic = "replies/alice",
            ExpiresAt = Now.AddMilliseconds(1500),
        };

        var content = Amqp0BusMessages.ToOutgoingContent(message, "q", Now);

        Assert.AreEqual("amq.topic", content.Exchange);
        Assert.AreEqual("sensors.hall", content.RoutingKey);
        var properties = Amqp0BasicProperties.Read(content.PropertyFlags, content.PropertyList);
        Assert.AreEqual("text/plain", properties.ContentType);
        Assert.AreEqual("h", properties.Headers!.Fields.Single().Name);
        Assert.AreEqual("c-1", properties.CorrelationId);
        Assert.AreEqual("replies.alice", properties.ReplyTo);
        Assert.AreEqual("1500", properties.Expiration);
    }

    [TestMethod]
    public void ToOutgoingContent_FieldsThatDoNotFit_AreLeftOut()
    {
        var message = new BusMessage("t", Encoding.ASCII.GetBytes("x"))
        {
            PublishedAt = Now,
            ContentType = new string('c', 256),
            CorrelationData = new byte[] { 0xFF },
            ResponseTopic = "a.b/c",
            ExpiresAt = Now.AddSeconds(-1),
        };

        var properties = ReadProperties(Amqp0BusMessages.ToOutgoingContent(message, "q", Now));

        Assert.IsNull(properties.ContentType);
        Assert.IsNull(properties.CorrelationId);
        Assert.IsNull(properties.ReplyTo);
        Assert.AreEqual("0", properties.Expiration);
    }

    [TestMethod]
    public void ToBusMessage_Properties_AreTheBusFields()
    {
        var properties = new Amqp0BasicProperties
        {
            Headers = new([new("h", Amqp0FieldValue.LongString("v")), new("n", Amqp0FieldValue.Int32(1))]),
            ReplyTo = "a/b",
            Priority = 5,
            Expiration = "10",
        };
        var (flags, list) = properties.Encode();

        var message = Amqp0BusMessages.ToBusMessage("t", "amq.topic", "t", new(60, 1, flags, list), properties, [1], Now);

        Assert.AreEqual("h", message.Headers.Single().Name);
        Assert.IsNull(message.ResponseTopic);
        Assert.IsTrue(message.HighPriority);
        Assert.AreEqual(Now.AddMilliseconds(10), message.ExpiresAt);
        Assert.AreEqual(BusQualityOfService.AtLeastOnce, message.QualityOfService);
        Assert.AreEqual("amqp0", message.NativeForm!.Format);
    }

    private static Amqp0BasicProperties ReadProperties(Amqp0OutgoingContent content) =>
        Amqp0BasicProperties.Read(content.PropertyFlags, content.PropertyList);
}
