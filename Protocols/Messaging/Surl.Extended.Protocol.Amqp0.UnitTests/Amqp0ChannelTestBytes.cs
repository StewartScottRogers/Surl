using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// The channel, exchange, queue and basic methods laid out by hand from the AMQP 0-9-1 XML's
/// classes 20, 40, 50 and 60 and section 4.2.6, independently of the library's own writer.
/// </summary>
internal static class Amqp0ChannelTestBytes
{
    /// <summary>An empty field table: a zero long size.</summary>
    public static readonly byte[] EmptyTable = [0, 0, 0, 0];

    public static byte[] LongLong(long value) => [.. Long(value >> 32), .. Long(value & 0xFFFFFFFF)];

    /// <summary>Packs bits into one octet, the first bit lowest.</summary>
    public static byte Bits(params bool[] bits)
    {
        var octet = 0;
        for (var index = 0; index < bits.Length; index++)
        {
            octet |= bits[index] ? 1 << index : 0;
        }

        return (byte)octet;
    }

    public static byte[] ChannelOpen(int channel) => Method(channel, 20, 10, ShortString(string.Empty));

    public static byte[] ChannelOpenOk(int channel) => Method(channel, 20, 11, Long(0));

    public static byte[] ChannelClose(int channel) => Method(channel, 20, 40, [.. Short(200), .. ShortString("bye"), .. Short(0), .. Short(0)]);

    public static byte[] ChannelCloseOk(int channel) => Method(channel, 20, 41);

    public static byte[] BrokerChannelClose(int channel, int replyCode, string replyText, int classId, int methodId) =>
        Method(channel, 20, 40, [.. Short(replyCode), .. ShortString(replyText), .. Short(classId), .. Short(methodId)]);

    public static byte[] Flow(int channel, bool active) => Method(channel, 20, 20, Bits(active));

    public static byte[] FlowOk(int channel, bool active) => Method(channel, 20, 21, Bits(active));

    public static byte[] ExchangeDeclare(int channel, string name, string type, byte bits = 0, byte[]? table = null) =>
        Method(channel, 40, 10, [.. Short(0), .. ShortString(name), .. ShortString(type), bits, .. table ?? EmptyTable]);

    public static byte[] ExchangeDeclareOk(int channel) => Method(channel, 40, 11);

    public static byte[] ExchangeDelete(int channel, string name, byte bits = 0) => Method(channel, 40, 20, [.. Short(0), .. ShortString(name), bits]);

    public static byte[] ExchangeDeleteOk(int channel) => Method(channel, 40, 21);

    /// <summary><c>queue.declare</c>; bits: passive 1, durable 2, exclusive 4, auto-delete 8, no-wait 16.</summary>
    public static byte[] QueueDeclare(int channel, string name, byte bits = 0) =>
        Method(channel, 50, 10, [.. Short(0), .. ShortString(name), bits, .. EmptyTable]);

    public static byte[] QueueDeclareOk(int channel, string name, int messages = 0, int consumers = 0) =>
        Method(channel, 50, 11, [.. ShortString(name), .. Long(messages), .. Long(consumers)]);

    public static byte[] QueueBind(int channel, string queue, string exchange, string key, bool noWait = false) =>
        Method(channel, 50, 20, [.. Short(0), .. ShortString(queue), .. ShortString(exchange), .. ShortString(key), Bits(noWait), .. EmptyTable]);

    public static byte[] QueueBindOk(int channel) => Method(channel, 50, 21);

    public static byte[] QueueUnbind(int channel, string queue, string exchange, string key) =>
        Method(channel, 50, 50, [.. Short(0), .. ShortString(queue), .. ShortString(exchange), .. ShortString(key), .. EmptyTable]);

    public static byte[] QueueUnbindOk(int channel) => Method(channel, 50, 51);

    public static byte[] QueuePurge(int channel, string queue, bool noWait = false) => Method(channel, 50, 30, [.. Short(0), .. ShortString(queue), Bits(noWait)]);

    public static byte[] QueuePurgeOk(int channel, int messages) => Method(channel, 50, 31, Long(messages));

    /// <summary><c>queue.delete</c>; bits: if-unused 1, if-empty 2, no-wait 4.</summary>
    public static byte[] QueueDelete(int channel, string queue, byte bits = 0) => Method(channel, 50, 40, [.. Short(0), .. ShortString(queue), bits]);

    public static byte[] QueueDeleteOk(int channel, int messages) => Method(channel, 50, 41, Long(messages));

    public static byte[] Qos(int channel, int prefetchCount) => Method(channel, 60, 10, [.. Long(0), .. Short(prefetchCount), 0]);

    public static byte[] QosOk(int channel) => Method(channel, 60, 11);

    /// <summary><c>basic.consume</c>; bits: no-local 1, no-ack 2, exclusive 4, no-wait 8.</summary>
    public static byte[] Consume(int channel, string queue, string tag, byte bits = 0) =>
        Method(channel, 60, 20, [.. Short(0), .. ShortString(queue), .. ShortString(tag), bits, .. EmptyTable]);

    public static byte[] ConsumeOk(int channel, string tag) => Method(channel, 60, 21, ShortString(tag));

    public static byte[] Cancel(int channel, string tag, bool noWait = false) => Method(channel, 60, 30, [.. ShortString(tag), Bits(noWait)]);

    public static byte[] CancelOk(int channel, string tag) => Method(channel, 60, 31, ShortString(tag));

    /// <summary><c>basic.publish</c>; bits: mandatory 1, immediate 2.</summary>
    public static byte[] Publish(int channel, string exchange, string routingKey, byte bits = 0) =>
        Method(channel, 60, 40, [.. Short(0), .. ShortString(exchange), .. ShortString(routingKey), bits]);

    public static byte[] Return(int channel, int replyCode, string replyText, string exchange, string routingKey) =>
        Method(channel, 60, 50, [.. Short(replyCode), .. ShortString(replyText), .. ShortString(exchange), .. ShortString(routingKey)]);

    public static byte[] Deliver(int channel, string consumerTag, long deliveryTag, bool redelivered, string exchange, string routingKey) =>
        Method(channel, 60, 60, [.. ShortString(consumerTag), .. LongLong(deliveryTag), Bits(redelivered), .. ShortString(exchange), .. ShortString(routingKey)]);

    public static byte[] Get(int channel, string queue, bool noAck = false) => Method(channel, 60, 70, [.. Short(0), .. ShortString(queue), Bits(noAck)]);

    public static byte[] GetOk(int channel, long deliveryTag, bool redelivered, string exchange, string routingKey, int messagesLeft) =>
        Method(channel, 60, 71, [.. LongLong(deliveryTag), Bits(redelivered), .. ShortString(exchange), .. ShortString(routingKey), .. Long(messagesLeft)]);

    public static byte[] GetEmpty(int channel) => Method(channel, 60, 72, ShortString(string.Empty));

    public static byte[] Ack(int channel, long deliveryTag, bool multiple = false) => Method(channel, 60, 80, [.. LongLong(deliveryTag), Bits(multiple)]);

    public static byte[] Reject(int channel, long deliveryTag, bool requeue) => Method(channel, 60, 90, [.. LongLong(deliveryTag), Bits(requeue)]);

    public static byte[] Nack(int channel, long deliveryTag, bool multiple, bool requeue) => Method(channel, 60, 120, [.. LongLong(deliveryTag), Bits(multiple, requeue)]);

    public static byte[] Recover(int channel, bool requeue) => Method(channel, 60, 110, Bits(requeue));

    public static byte[] RecoverAsync(int channel, bool requeue) => Method(channel, 60, 100, Bits(requeue));

    public static byte[] RecoverOk(int channel) => Method(channel, 60, 111);

    /// <summary>A content header of class 60: weight 0, the body size, the flags, the property list.</summary>
    public static byte[] ContentHeader(int channel, long bodySize, int propertyFlags = 0, byte[]? propertyList = null, int classId = 60) =>
        Frame(2, channel, [.. Short(classId), .. Short(0), .. LongLong(bodySize), .. Short(propertyFlags), .. propertyList ?? []]);

    public static byte[] Body(int channel, byte[] bytes) => Frame(3, channel, bytes);

    public static byte[] Body(int channel, string text) => Body(channel, System.Text.Encoding.UTF8.GetBytes(text));

    /// <summary>A publish of <paramref name="text"/> with no properties, header and one body frame.</summary>
    public static byte[] PublishText(int channel, string exchange, string routingKey, string text, byte bits = 0) =>
        Concat(Publish(channel, exchange, routingKey, bits), ContentHeader(channel, System.Text.Encoding.UTF8.GetByteCount(text)), Body(channel, text));

    public static byte[] TxSelect(int channel) => Method(channel, 90, 10);

    public static byte[] TxSelectOk(int channel) => Method(channel, 90, 11);

    public static byte[] TxCommit(int channel) => Method(channel, 90, 20);

    public static byte[] TxCommitOk(int channel) => Method(channel, 90, 21);

    public static byte[] TxRollback(int channel) => Method(channel, 90, 30);

    public static byte[] TxRollbackOk(int channel) => Method(channel, 90, 31);

    /// <summary>RabbitMQ's <c>confirm.select</c> (85.10) with its <c>nowait</c> bit.</summary>
    public static byte[] ConfirmSelect(int channel, bool noWait = false) => Method(channel, 85, 10, Bits(noWait));

    public static byte[] ConfirmSelectOk(int channel) => Method(channel, 85, 11);

    /// <summary>The broker's publisher confirm: <c>basic.ack</c> of the publish's sequence number, <c>multiple</c> clear.</summary>
    public static byte[] Confirm(int channel, long sequence) => Ack(channel, sequence);

    /// <summary>The broker's refusal of a publish in confirm mode: <c>basic.nack</c>, <c>multiple</c> and <c>requeue</c> clear.</summary>
    public static byte[] ConfirmRefused(int channel, long sequence) => Nack(channel, sequence, multiple: false, requeue: false);

    /// <summary>The header and body frames the broker writes for <paramref name="text"/> with no properties.</summary>
    public static byte[] TextContent(int channel, string text) =>
        Concat(ContentHeader(channel, System.Text.Encoding.UTF8.GetByteCount(text)), Body(channel, text));
}
