using Surl.MessageBus;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// Converts between the broker's Application Messages and the shared message bus's messages
/// (ADR-0100 decision 3.4): the Payload Format Indicator rides as <see cref="BusMessage.Utf8Payload"/>,
/// the Message Expiry Interval as an absolute <see cref="BusMessage.ExpiresAt"/>, and Content Type,
/// Response Topic, Correlation Data and every User Property, in order, as the bus's own fields.
/// </summary>
internal static class Mqtt5BusMessages
{
    /// <summary>
    /// The bus message for <paramref name="message"/>, published at <paramref name="now"/>.
    /// </summary>
    /// <param name="message">The message, its topic already canonical.</param>
    /// <param name="now">When it is published; its Message Expiry Interval counts from here.</param>
    /// <returns>The bus message.</returns>
    public static BusMessage ToBusMessage(Mqtt5ApplicationMessage message, DateTimeOffset now)
    {
        var properties = message.Properties;
        return new BusMessage(message.Topic, message.Payload)
        {
            Retain = message.Retain,
            QualityOfService = (BusQualityOfService)message.QualityOfService,
            ExpiresAt = ExpiryOf(Find(properties, Mqtt5PropertyIdentifier.MessageExpiryInterval), now),
            ContentType = Find(properties, Mqtt5PropertyIdentifier.ContentType)?.Text,
            ResponseTopic = Find(properties, Mqtt5PropertyIdentifier.ResponseTopic)?.Text,
            CorrelationData = BinaryOf(Find(properties, Mqtt5PropertyIdentifier.CorrelationData)),
            Headers = [.. properties.Where(IsUserProperty).Select(property => new BusMessageHeader(property.Text, property.PairValue))],
            Utf8Payload = Find(properties, Mqtt5PropertyIdentifier.PayloadFormatIndicator)?.Integer == 1,
        };
    }

    /// <summary>
    /// The PUBLISH Properties a delivery of <paramref name="message"/> at <paramref name="now"/>
    /// carries, in this order: Payload Format Indicator 1 for a UTF-8 payload, the Message Expiry
    /// Interval left (whole seconds, rounded up; <c>[MQTT-3.3.2-6]</c>), Response Topic,
    /// Correlation Data, every User Property in order, and Content Type.
    /// </summary>
    /// <param name="message">The bus message, not yet expired.</param>
    /// <param name="now">When it is delivered.</param>
    /// <returns>The properties.</returns>
    public static List<Mqtt5Property> PropertiesOf(BusMessage message, DateTimeOffset now)
    {
        var properties = new List<Mqtt5Property>();
        if (message.Utf8Payload)
        {
            properties.Add(Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.PayloadFormatIndicator, 1));
        }

        if (message.ExpiresAt is { } expiresAt)
        {
            properties.Add(Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.MessageExpiryInterval, (uint)Math.Ceiling((expiresAt - now).TotalSeconds)));
        }

        if (message.ResponseTopic is { } responseTopic)
        {
            properties.Add(Mqtt5Property.FromText(Mqtt5PropertyIdentifier.ResponseTopic, responseTopic));
        }

        if (message.CorrelationData is { } correlationData)
        {
            properties.Add(Mqtt5Property.FromBinary(Mqtt5PropertyIdentifier.CorrelationData, correlationData.ToArray()));
        }

        properties.AddRange(message.Headers.Select(header => Mqtt5Property.FromUserProperty(header.Name, header.Value)));
        if (message.ContentType is { } contentType)
        {
            properties.Add(Mqtt5Property.FromText(Mqtt5PropertyIdentifier.ContentType, contentType));
        }

        return properties;
    }

    private static Mqtt5Property? Find(IReadOnlyList<Mqtt5Property> properties, Mqtt5PropertyIdentifier identifier) =>
        Mqtt5ConnectJudge.Find(properties, identifier);

    private static bool IsUserProperty(Mqtt5Property property) => property.Identifier == Mqtt5PropertyIdentifier.UserProperty;

    private static DateTimeOffset? ExpiryOf(Mqtt5Property? interval, DateTimeOffset now) =>
        interval is null ? null : now + TimeSpan.FromSeconds(interval.Integer);

    private static ReadOnlyMemory<byte>? BinaryOf(Mqtt5Property? property) =>
        // A bare null would convert to empty memory through the byte[] conversion, not to no memory.
        property is null ? (ReadOnlyMemory<byte>?)null : property.Binary;
}
