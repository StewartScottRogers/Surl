namespace Surl.MessageBus;

/// <summary>
/// One record of a topic's log (ADR-0100, decision 3.3): a message published on the topic and
/// the offset the log gave it. The record's timestamp is <see cref="BusMessage.PublishedAt"/>,
/// its key <see cref="BusMessage.Key"/> and its headers <see cref="BusMessage.Headers"/>, as a
/// Kafka record carries them (Kafka protocol guide, Fetch).
/// </summary>
/// <param name="Offset">The record's place in its topic's log: 0 for the first message ever
/// published on the topic, then one more for each after it, with no gaps.</param>
/// <param name="Message">The message as the bus took it.</param>
public sealed record BusLogRecord(long Offset, BusMessage Message);
