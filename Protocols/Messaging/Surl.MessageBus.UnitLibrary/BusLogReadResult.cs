namespace Surl.MessageBus;

/// <summary>
/// What <see cref="MessageBus.ReadLog"/> found: the records from the asked offset on, and where
/// the log starts and ends, as a Kafka Fetch answers one partition (Kafka protocol guide, Fetch).
/// </summary>
/// <param name="Outcome"><see cref="MessageBusOutcome.Succeeded"/>,
/// <see cref="MessageBusOutcome.InvalidTopic"/>, or <see cref="MessageBusOutcome.OffsetOutOfRange"/>
/// for an offset below <paramref name="LogStartOffset"/> or above <paramref name="HighWatermark"/>.</param>
/// <param name="LogStartOffset">The offset of the oldest record the log still holds.</param>
/// <param name="HighWatermark">The offset the next message published on the topic will get.</param>
/// <param name="Records">The records read, in offset order; empty when refused, and when the
/// offset asked is the high watermark (nothing newer has been published yet).</param>
public sealed record BusLogReadResult(
    MessageBusOutcome Outcome,
    long LogStartOffset,
    long HighWatermark,
    IReadOnlyList<BusLogRecord> Records);
