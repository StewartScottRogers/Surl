namespace Surl.MessageBus;

/// <summary>
/// An opaque, in-memory copy of one <see cref="MessageBus"/>'s contents (ADR-0124, decision 3):
/// its retained messages, every topic log with its offsets, and each queue that does not belong
/// to one connection, with its bindings and every message it held, ready or unacknowledged. Made by
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
        IReadOnlyList<BusQueueSnapshot> queues)
    {
        Bus = bus;
        ByteCount = byteCount;
        Retained = retained;
        Logs = logs;
        Queues = queues;
    }

    /// <summary>
    /// The bytes the snapshot holds, as the bus's bounds count them: the retained payloads, every
    /// log record's <see cref="MessageBus.LogRecordBytes"/>, and every queued message's payload
    /// and native form.
    /// </summary>
    public long ByteCount { get; }

    /// <summary>The bus that made it, the only one that restores it.</summary>
    internal MessageBus Bus { get; }

    internal IReadOnlyList<BusMessage> Retained { get; }

    internal IReadOnlyList<BusLogSnapshot> Logs { get; }

    internal IReadOnlyList<BusQueueSnapshot> Queues { get; }
}
