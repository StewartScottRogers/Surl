namespace Surl.MessageBus;

/// <summary>
/// Everything <see cref="MessageBusFiles"/> read at load, before the bus checks it against its
/// bounds and takes it.
/// </summary>
/// <param name="Retained">The retained messages, in the file's order.</param>
/// <param name="RetainedSource">The file they came from, and the reason that file is refused
/// with when it holds more than the bounds allow.</param>
/// <param name="RetainedMigrated">Whether they came from an old MQTT 3.1.1
/// <c>retained-messages</c> file, so the bus's own <c>retained</c> file is still to be written.</param>
/// <param name="Queues">The durable queues, in ordinal order of name.</param>
/// <param name="Logs">The topic logs, in order of file number.</param>
internal sealed record MessageBusFileContents(
    IReadOnlyList<BusMessage> Retained,
    PersistedFileSource RetainedSource,
    bool RetainedMigrated,
    IReadOnlyList<PersistedQueue> Queues,
    IReadOnlyList<PersistedLog> Logs);

/// <summary>A file read at load and the reason it is refused with when it holds too much.</summary>
internal sealed record PersistedFileSource(string FilePath, string MalformedReason);

/// <summary>One durable queue as <c>queues/index</c> holds it.</summary>
internal sealed record PersistedQueue(string Name, IReadOnlyList<string> Filters, IReadOnlyList<PersistedQueueMessage> Messages);

/// <summary>One message of a durable queue: its message file's number, whether a consumer was
/// handed it before, and the message the file holds.</summary>
internal sealed record PersistedQueueMessage(ulong FileNumber, bool Redelivered, BusMessage Message);

/// <summary>One durable queue as the bus writes it into <c>queues/index</c>: its name, its
/// filters in ordinal order, and each message's file number and redelivered mark in queue order.</summary>
internal sealed record QueueIndexEntry(string Name, IReadOnlyList<string> Filters, IReadOnlyList<(ulong FileNumber, bool Redelivered)> Messages);

/// <summary>One topic's log as <c>logs/&lt;number&gt;</c> holds it.</summary>
internal sealed record PersistedLog(ulong FileNumber, string FilePath, string Topic, long StartOffset, IReadOnlyList<BusMessage> Records);
