using System.Globalization;
using System.Text;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// Turns a published 0-9-1 message into a <see cref="BusMessage"/> and a bus delivery back into
/// what <c>basic.deliver</c> and <c>get-ok</c> carry (ADR-0112 decision 6; ADR-0100 decision 3.4).
/// </summary>
internal static class Amqp0BusMessages
{
    /// <summary>The native form's format: <c>amqp0</c> (BL-706).</summary>
    public const string NativeFormat = "amqp0";

    private const int ShortStringMaxBytes = 255;
    private const byte HighPriorityFloor = 5;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The bus message for a publish: its properties onto the bus's fields, and beside them the
    /// native form - exchange and routing key as two shortstrs, then the property flags and list
    /// byte for byte.
    /// </summary>
    /// <exception cref="Amqp0ChannelException"><c>expiration</c> is not a count of milliseconds from 0 to 2147483647 (406).</exception>
    public static BusMessage ToBusMessage(string topic, string exchange, string routingKey, Amqp0ContentHeader header, Amqp0BasicProperties properties, byte[] body, DateTimeOffset now) =>
        new(topic, body)
        {
            QualityOfService = BusQualityOfService.AtLeastOnce,
            ContentType = properties.ContentType,
            Headers = HeadersOf(properties.Headers),
            ExpiresAt = ExpiresAt(properties.Expiration, now),
            ResponseTopic = MappedTopic(properties.ReplyTo),
            CorrelationData = properties.CorrelationId is { } correlationId ? Encoding.UTF8.GetBytes(correlationId) : null,
            HighPriority = properties.Priority >= HighPriorityFloor,
            NativeForm = new(NativeFormat, NativeBytes(exchange, routingKey, header)),
        };

    /// <summary>
    /// What a delivery of <paramref name="message"/> from <paramref name="queueName"/> carries: the
    /// native form unchanged when it has one, otherwise <c>amq.topic</c> and its mapped routing key
    /// (or the default exchange and the queue's name) and a header built from what it has.
    /// </summary>
    public static Amqp0OutgoingContent ToOutgoingContent(BusMessage message, string queueName, DateTimeOffset now)
    {
        if (message.NativeForm is { Format: NativeFormat } native)
        {
            var reader = new Amqp0WireReader(native.Bytes);
            var exchange = reader.ReadShortString();
            var routingKey = reader.ReadShortString();
            var flags = reader.ReadShort();
            return new(exchange, routingKey, flags, reader.ReadRemaining(), message.Payload);
        }

        var (propertyFlags, propertyList) = PropertiesOf(message, now).Encode();
        var mapped = AmqpTopicExchangeNames.FromCanonicalTopic(message.Topic);
        return mapped.IsMapped
            ? new(Amqp0ExchangeTable.TopicSpace, mapped.Name, propertyFlags, propertyList, message.Payload)
            : new(string.Empty, queueName, propertyFlags, propertyList, message.Payload);
    }

    private static List<BusMessageHeader> HeadersOf(Amqp0FieldTable? table) =>
        table is null
            ? []
            : table.Fields
                .Where(field => field.Value.Type == Amqp0FieldValueType.LongString)
                .Select(field => new BusMessageHeader(field.Name, Encoding.UTF8.GetString((byte[])field.Value.Value!)))
                .ToList();

    private static DateTimeOffset? ExpiresAt(string? expiration, DateTimeOffset now)
    {
        if (expiration is null)
        {
            return null;
        }

        if (!int.TryParse(expiration, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds))
        {
            throw new Amqp0ChannelException(Amqp0ReplyCode.PreconditionFailed, "PRECONDITION_FAILED - invalid expiration");
        }

        return now.AddMilliseconds(milliseconds);
    }

    private static string? MappedTopic(string? routingKey) =>
        routingKey is not null && AmqpTopicExchangeNames.ToCanonicalTopic(routingKey) is { IsMapped: true } mapped ? mapped.Name : null;

    private static byte[] NativeBytes(string exchange, string routingKey, Amqp0ContentHeader header)
    {
        var writer = new Amqp0WireWriter();
        writer.WriteShortString(exchange);
        writer.WriteShortString(routingKey);
        writer.WriteShort(header.PropertyFlags);
        writer.WriteBytes(header.PropertyList.Span);
        return writer.ToArray();
    }

    private static Amqp0BasicProperties PropertiesOf(BusMessage message, DateTimeOffset now) => new()
    {
        ContentType = FittingShortString(message.ContentType),
        Headers = message.Headers.Count == 0 ? null : TableOf(message.Headers),
        CorrelationId = FittingShortString(Utf8OrNull(message.CorrelationData)),
        ReplyTo = message.ResponseTopic is { } responseTopic ? MappedRoutingKey(responseTopic) : null,
        Expiration = message.ExpiresAt is { } expiresAt ? RemainingMilliseconds(expiresAt, now) : null,
        Timestamp = (ulong)Math.Max(0, message.PublishedAt.ToUnixTimeSeconds()),
    };

    private static Amqp0FieldTable TableOf(IReadOnlyList<BusMessageHeader> headers) =>
        new(headers
            .Where(header => FittingShortString(header.Name) is not null)
            .Select(header => new Amqp0Field(header.Name, Amqp0FieldValue.LongString(header.Value)))
            .ToList());

    private static string? MappedRoutingKey(string topic) =>
        AmqpTopicExchangeNames.FromCanonicalTopic(topic) is { IsMapped: true } mapped ? mapped.Name : null;

    private static string RemainingMilliseconds(DateTimeOffset expiresAt, DateTimeOffset now) =>
        ((long)Math.Max(0, Math.Ceiling((expiresAt - now).TotalMilliseconds))).ToString(CultureInfo.InvariantCulture);

    private static string? FittingShortString(string? text) =>
        text is not null && Encoding.UTF8.GetByteCount(text) <= ShortStringMaxBytes ? text : null;

    private static string? Utf8OrNull(ReadOnlyMemory<byte>? bytes)
    {
        if (bytes is not { } present)
        {
            return null;
        }

        try
        {
            return StrictUtf8.GetString(present.Span);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
