namespace Surl.MessageBus;

/// <summary>
/// Where a topic's log starts and ends, as Kafka's ListOffsets answers "earliest" and "latest"
/// (Kafka protocol guide, ListOffsets).
/// </summary>
/// <param name="Outcome"><see cref="MessageBusOutcome.Succeeded"/>, or
/// <see cref="MessageBusOutcome.InvalidTopic"/> for a topic that is not canonical (both offsets
/// are then 0).</param>
/// <param name="LogStartOffset">The offset of the oldest record the log still holds; it rises as
/// retention drops the oldest. Equal to <paramref name="HighWatermark"/> when the log is empty.</param>
/// <param name="HighWatermark">The offset the next message published on the topic will get: one
/// past the newest record. 0 for a topic nothing was ever published on.</param>
public sealed record BusLogOffsets(MessageBusOutcome Outcome, long LogStartOffset, long HighWatermark);
