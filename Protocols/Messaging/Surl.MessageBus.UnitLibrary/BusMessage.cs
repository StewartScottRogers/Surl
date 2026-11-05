namespace Surl.MessageBus;

/// <summary>
/// One message on the bus (ADR-0100, decision 3.4): a canonical topic, the payload bytes
/// (always exact), and the properties that survive translation between protocols. A
/// publisher builds one and hands it to <see cref="MessageBus.Publish(BusMessage, string?)"/>, which keeps its own
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
    /// The publisher's own encoding of the message, tagged with its format (ADR-0107 decision 6);
    /// <see langword="null"/> when it has none. Its bytes count with <see cref="Payload"/> toward
    /// <see cref="MessageBus.MaxMessageBytes"/>, <see cref="MessageBus.MaxQueuedBytes"/> and
    /// <see cref="MessageBus.LogRecordBytes"/>.
    /// </summary>
    public BusNativeForm? NativeForm { get; init; }

    /// <summary>
    /// Whether the message is high priority: a named queue hands out its high-priority messages
    /// before its normal ones, each level in publish order (AMQP 0-9-1 <c>priority</c> 5 to 9, basic
    /// rules 06 and 08; ADR-0112 decision 6). Topic fan-out and logs ignore it.
    /// </summary>
    public bool HighPriority { get; init; }

    /// <summary>The bytes the message counts toward <see cref="MessageBus.MaxMessageBytes"/> and
    /// <see cref="MessageBus.MaxQueuedBytes"/>: its payload and its native form's bytes.</summary>
    internal long PayloadAndNativeFormBytes => Payload.Length + (NativeForm?.Bytes.Length ?? 0L);

    /// <summary>
    /// When the bus took the message, from its <see cref="TimeProvider"/>; whatever a publisher
    /// sets here is replaced.
    /// </summary>
    public DateTimeOffset PublishedAt { get; init; }
}
