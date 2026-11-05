namespace Surl.MessageBus;

/// <summary>
/// The bus's topic logs (ADR-0100, decision 3.3): every message published on a topic is
/// appended to that topic's log at the next offset, whichever protocol published it, so a Kafka
/// consumer can fetch from an offset what any server published, and a Kafka producer's records
/// reach every other protocol's subscribers through <see cref="Publish(BusMessage, string?)"/>. One partition per
/// topic (ADR-0100, decision 3.2). Past <see cref="MaxLogBytesPerTopic"/> the oldest records
/// are dropped and the log start offset rises; past <see cref="MaxLogBytes"/> a publish is
/// refused.
/// </summary>
public sealed partial class MessageBus
{
    /// <summary>The default for <see cref="MaxLogBytesPerTopic"/>: 16777216 (16 MiB).</summary>
    public const long DefaultMaxLogBytesPerTopic = 16_777_216;

    /// <summary>The default for <see cref="MaxLogBytes"/>: 268435456 (256 MiB).</summary>
    public const long DefaultMaxLogBytes = 268_435_456;

    /// <summary>
    /// The bytes every log record counts beyond its payload, key and headers: its offset and
    /// its timestamp, eight bytes each, as a Kafka record holds them. So a record with an empty
    /// payload still counts, and no log can hold unboundedly many.
    /// </summary>
    public const int LogRecordOverheadBytes = 16;

    private readonly Dictionary<string, TopicLog> logs = new(StringComparer.Ordinal);
    private long logBytes;

    /// <summary>The most bytes one topic's log holds (<see cref="LogRecordBytes"/>); past it the
    /// oldest records are dropped, though the newest is always kept.</summary>
    public long MaxLogBytesPerTopic { get; }

    /// <summary>The most bytes all topics' logs hold together; past it a publish is refused with
    /// <see cref="MessageBusOutcome.StoreFull"/>.</summary>
    public long MaxLogBytes { get; }

    /// <summary>
    /// The bytes one record counts against <see cref="MaxLogBytesPerTopic"/> and
    /// <see cref="MaxLogBytes"/>: its payload, its key, the UTF-8 of every header name and
    /// value, and <see cref="LogRecordOverheadBytes"/>.
    /// </summary>
    /// <param name="message">The message the record holds.</param>
    /// <returns>The bytes it counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public static long LogRecordBytes(BusMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var headerBytes = message.Headers.Sum(header =>
            (long)System.Text.Encoding.UTF8.GetByteCount(header.Name) + System.Text.Encoding.UTF8.GetByteCount(header.Value));
        return LogRecordOverheadBytes + message.Payload.Length + (message.Key?.Length ?? 0) + headerBytes;
    }

    /// <summary>
    /// The topics that have a log - every topic a message was ever published on - in ordinal
    /// order, as a Kafka broker lists them in its Metadata answer.
    /// </summary>
    /// <returns>The canonical topic names.</returns>
    public IReadOnlyList<string> GetLogTopics()
    {
        lock (stateLock)
        {
            return [.. logs.Keys.Order(StringComparer.Ordinal)];
        }
    }

    /// <summary>
    /// Where <paramref name="topic"/>'s log starts and ends, Kafka's ListOffsets "earliest" and
    /// "latest". A topic nothing was ever published on has an empty log at offset 0.
    /// </summary>
    /// <param name="topic">The canonical topic name.</param>
    /// <returns>The offsets, or <see cref="MessageBusOutcome.InvalidTopic"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="topic"/> is <see langword="null"/>.</exception>
    public BusLogOffsets GetLogOffsets(string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        if (!CanonicalTopicName.IsValidTopic(topic))
        {
            return new(MessageBusOutcome.InvalidTopic, 0, 0);
        }

        lock (stateLock)
        {
            var log = logs.GetValueOrDefault(topic) ?? new TopicLog();
            return new(MessageBusOutcome.Succeeded, log.LogStartOffset, log.HighWatermark);
        }
    }

    /// <summary>
    /// Reads <paramref name="topic"/>'s log from <paramref name="offset"/> on, as a Kafka Fetch
    /// does: the records in offset order, as many as fit in <paramref name="maxBytes"/>
    /// (<see cref="LogRecordBytes"/>) but always the first, so a record larger than the limit
    /// can still be read. An offset at the high watermark reads nothing; one below the log start
    /// offset or above the high watermark is <see cref="MessageBusOutcome.OffsetOutOfRange"/>
    /// (Kafka's <c>OFFSET_OUT_OF_RANGE</c>).
    /// </summary>
    /// <param name="topic">The canonical topic name.</param>
    /// <param name="offset">The offset of the first record to read.</param>
    /// <param name="maxBytes">The most bytes the records read count together, though the first
    /// is read whatever it counts; at least 0.</param>
    /// <returns>The outcome, the log's offsets, and the records read.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="topic"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is negative.</exception>
    public BusLogReadResult ReadLog(string topic, long offset, long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        if (!CanonicalTopicName.IsValidTopic(topic))
        {
            return new(MessageBusOutcome.InvalidTopic, 0, 0, []);
        }

        lock (stateLock)
        {
            var log = logs.GetValueOrDefault(topic) ?? new TopicLog();
            var start = log.LogStartOffset;
            var end = log.HighWatermark;
            return offset < start || offset > end
                ? new(MessageBusOutcome.OffsetOutOfRange, start, end, [])
                : new(MessageBusOutcome.Succeeded, start, end, log.ReadFrom(offset, maxBytes));
        }
    }

    /// <summary>
    /// Gives <paramref name="topic"/> its empty log when it has none, as a Kafka broker creates a
    /// topic a Metadata request names (ADR-0105, decision 2), so the topic is listed from then
    /// on. A topic that already has a log is left as it is.
    /// </summary>
    /// <param name="topic">The canonical topic name.</param>
    /// <returns><see cref="MessageBusOutcome.Succeeded"/> when the topic has a log now;
    /// <see cref="MessageBusOutcome.InvalidTopic"/> for a name that is not canonical; or
    /// <see cref="MessageBusOutcome.TooManyTopics"/> when the topic is new and
    /// <see cref="MaxTopics"/> topics already have a log, nothing made.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="topic"/> is <see langword="null"/>.</exception>
    public MessageBusOutcome CreateLog(string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        if (!CanonicalTopicName.IsValidTopic(topic))
        {
            return MessageBusOutcome.InvalidTopic;
        }

        lock (stateLock)
        {
            if (logs.ContainsKey(topic))
            {
                return MessageBusOutcome.Succeeded;
            }

            if (logs.Count >= MaxTopics)
            {
                return MessageBusOutcome.TooManyTopics;
            }

            logs.Add(topic, new TopicLog());
            return MessageBusOutcome.Succeeded;
        }
    }

    /// <summary>
    /// Whether <paramref name="published"/> can be appended to its topic's log: refused with
    /// <see cref="MessageBusOutcome.TooManyTopics"/> when its topic is new and
    /// <see cref="MaxTopics"/> topics already have a log, and with
    /// <see cref="MessageBusOutcome.StoreFull"/> when, after its topic's retention, every log
    /// together would pass <see cref="MaxLogBytes"/>. Changes nothing.
    /// </summary>
    private (MessageBusOutcome Outcome, LogAppend Append) PlanLogAppend(BusMessage published)
    {
        var recordBytes = LogRecordBytes(published);
        if (!logs.TryGetValue(published.Topic, out var log))
        {
            return logs.Count >= MaxTopics
                ? (MessageBusOutcome.TooManyTopics, default)
                : WithinMaxLogBytes(new LogAppend(recordBytes, 0, 0));
        }

        var (dropCount, droppedBytes) = log.OldestToDrop(recordBytes, MaxLogBytesPerTopic);
        return WithinMaxLogBytes(new LogAppend(recordBytes, dropCount, droppedBytes));
    }

    private (MessageBusOutcome Outcome, LogAppend Append) WithinMaxLogBytes(LogAppend append) =>
        logBytes - append.DroppedBytes + append.RecordBytes > MaxLogBytes
            ? (MessageBusOutcome.StoreFull, default)
            : (MessageBusOutcome.Succeeded, append);

    /// <summary>Appends <paramref name="published"/> to its topic's log as
    /// <paramref name="append"/> planned, and returns the offset it got.</summary>
    private long AppendToLog(BusMessage published, LogAppend append)
    {
        if (!logs.TryGetValue(published.Topic, out var log))
        {
            log = new TopicLog();
            logs.Add(published.Topic, log);
        }

        log.DropOldest(append.DropCount, append.DroppedBytes);
        logBytes += append.RecordBytes - append.DroppedBytes;
        return log.Append(published, append.RecordBytes);
    }

    /// <summary>A planned log append: the bytes the new record counts, and how many of the
    /// topic's oldest records retention drops first, and the bytes they count.</summary>
    private readonly record struct LogAppend(long RecordBytes, int DropCount, long DroppedBytes);
}
