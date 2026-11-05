namespace Surl.MessageBus;

/// <summary>
/// One message on the bus (ADR-0100, decision 3.4): a canonical topic, the payload bytes
/// (always exact), and the properties that survive translation between protocols. A
/// publisher builds one and hands it to <see cref="MessageBus.Publish"/>, which keeps its own
/// copy of every byte and stamps <see cref="PublishedAt"/>; subscribers receive that copy.
/// </summary>
/// <param name="Topic">The canonical topic name it is published on (<see cref="CanonicalTopicName"/>).</param>
/// <param name="Payload">The payload bytes.</param>
public sealed record BusMessage(string Topic, ReadOnlyMemory<byte> Payload)
{
    /// <summary>
    /// Whether the message is retained: it replaces the topic's retained message, or removes it
    /// when <see cref="Payload"/> is empty (MQTT 3.1.1 and 5.0 section 3.3.1.3).
    /// </summary>
    public bool Retain { get; init; }

    /// <summary>
    /// The quality of service the publisher sent it with.
    /// </summary>
    public BusQualityOfService QualityOfService { get; init; }

    /// <summary>
    /// When the message expires; <see langword="null"/> when it never does. An expired message
    /// is never delivered and is dropped from the retained slot when read.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// The payload's content type; <see langword="null"/> when the publisher gave none.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// The topic a reply goes to; <see langword="null"/> when the publisher gave none.
    /// </summary>
    public string? ResponseTopic { get; init; }

    /// <summary>
    /// The correlation bytes a reply carries back; <see langword="null"/> when the publisher
    /// gave none.
    /// </summary>
    public ReadOnlyMemory<byte>? CorrelationData { get; init; }

    /// <summary>
    /// The message's key (Kafka's record key); <see langword="null"/> when it has none.
    /// </summary>
    public ReadOnlyMemory<byte>? Key { get; init; }

    /// <summary>
    /// The message's string headers, in order; a name may repeat.
    /// </summary>
    public IReadOnlyList<BusMessageHeader> Headers { get; init; } = [];

    /// <summary>
    /// Whether the publisher marked the payload as UTF-8 text (MQTT 5's Payload Format Indicator).
    /// </summary>
    public bool Utf8Payload { get; init; }

    /// <summary>
    /// When the bus took the message, from its <see cref="TimeProvider"/>; whatever a publisher
    /// sets here is replaced.
    /// </summary>
    public DateTimeOffset PublishedAt { get; init; }
}
