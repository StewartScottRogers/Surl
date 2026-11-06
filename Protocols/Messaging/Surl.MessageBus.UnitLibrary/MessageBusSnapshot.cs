namespace Surl.MessageBus;

/// <summary>
/// An opaque, in-memory copy of one <see cref="MessageBus"/>'s contents (ADR-0124, decision 3):
/// its retained messages, every topic log with its offsets, each queue that does not belong
/// to one connection, with its bindings and every message it held, ready or unacknowledged, and
/// every committed offset (ADR-0145, decision 4). Made by
/// <see cref="MessageBus.TakeSnapshot(long)"/> and given back by
/// <see cref="MessageBus.Restore(MessageBusSnapshot)"/> to the bus that made it, and to no other.
/// </summary>
public sealed class MessageBusSnapshot
{
    internal MessageBusSnapshot(
        MessageBus bus,
        long byteCount,
        IReadOnlyList<BusMessage> retained,
        IReadOnlyList<BusLogSnapshot> logs,
        IReadOnlyList<BusQueueSnapshot> queues,
        IReadOnlyList<BusCommittedOffset> committedOffsets)
    {
        Bus = bus;
        ByteCount = byteCount;
        Retained = retained;
        Logs = logs;
        Queues = queues;
        CommittedOffsets = committedOffsets;
    }

    /// <summary>
    /// The bytes the snapshot holds, as the bus's bounds count them: the retained payloads, every
    /// log record's <see cref="MessageBus.LogRecordBytes"/>, every queued message's payload
    /// and native form, and for every committed offset the UTF-8 of its group, topic and metadata
    /// and 20 bytes for its offset, leader epoch and commit time.
    /// </summary>
    public long ByteCount { get; }

    /// <summary>The bus that made it, the only one that restores it.</summary>
    internal MessageBus Bus { get; }

    internal IReadOnlyList<BusMessage> Retained { get; }

    internal IReadOnlyList<BusLogSnapshot> Logs { get; }

    internal IReadOnlyList<BusQueueSnapshot> Queues { get; }

    internal IReadOnlyList<BusCommittedOffset> CommittedOffsets { get; }
}
