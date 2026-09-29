namespace Surl.Protocol.Mqtt;

/// <summary>
/// The last message published to each topic, kept in memory for as long as the MQTT server
/// that holds it. Every connection the server answers shares it, so a message one client
/// publishes is what the next client to subscribe receives. Safe for concurrent use.
/// </summary>
/// <remarks>
/// <para>
/// It is bounded, because every peer can publish to it and it outlives each connection
/// (ADR-0014, decision 7): at most <see cref="MaxTopics"/> topics and
/// <see cref="MaxTotalPayloadBytes"/> payload bytes in all. A message that would take it past
/// either is not kept.
/// </para>
/// <para>
/// A store made by the constructor lives in memory only. A store made by
/// <see cref="LoadAsync"/> starts with what its <see cref="MqttRetainedMessageFile"/> holds and
/// writes each change back to it when <see cref="SaveChangesAsync"/> is called, so its
/// messages survive a restart (ADR-0031, decision 6).
/// </para>
/// </remarks>
public sealed class MqttRetainedMessages
{
    /// <summary>
    /// The default for <see cref="MaxTopics"/>: 10000 topics.
    /// </summary>
    public const int DefaultMaxTopics = 10_000;

    /// <summary>
    /// The default for <see cref="MaxTotalPayloadBytes"/>: 104857600 bytes (100 MiB), the
    /// same as ADR-0006's upload default.
    /// </summary>
    public const long DefaultMaxTotalPayloadBytes = 104_857_600;

    private readonly Dictionary<string, byte[]> messages = new(StringComparer.Ordinal);
    private readonly Lock messagesLock = new();
    private readonly SemaphoreSlim saveLock = new(1, 1);
    private MqttRetainedMessageFile? file;
    private long totalPayloadBytes;
    private long changeCount;
    private long savedChangeCount;

    /// <summary>
    /// Creates an empty store with the given bounds.
    /// </summary>
    /// <param name="maxTopics">The value of <see cref="MaxTopics"/>; at least 1.</param>
    /// <param name="maxTotalPayloadBytes">The value of <see cref="MaxTotalPayloadBytes"/>; at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound is less than 1.</exception>
    public MqttRetainedMessages(int maxTopics = DefaultMaxTopics, long maxTotalPayloadBytes = DefaultMaxTotalPayloadBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTopics, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTotalPayloadBytes, 1);

        MaxTopics = maxTopics;
        MaxTotalPayloadBytes = maxTotalPayloadBytes;
    }

    /// <summary>
    /// Creates a store that holds what <paramref name="file"/> holds and writes every later
    /// change back to it through <see cref="SaveChangesAsync"/> (ADR-0031, decision 6). A
    /// missing file is an empty store.
    /// </summary>
    /// <param name="file">The file the store is read from now and written to after each change.</param>
    /// <param name="maxTopics">The value of <see cref="MaxTopics"/>; at least 1.</param>
    /// <param name="maxTotalPayloadBytes">The value of <see cref="MaxTotalPayloadBytes"/>; at least 1.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The loaded store.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A bound is less than 1.</exception>
    /// <exception cref="InvalidDataException">The file does not parse, or holds more topics or
    /// payload bytes than the bounds allow; nothing is loaded, and the message is
    /// <c>not a retained-message file</c>.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static async Task<MqttRetainedMessages> LoadAsync(
        MqttRetainedMessageFile file,
        int maxTopics = DefaultMaxTopics,
        long maxTotalPayloadBytes = DefaultMaxTotalPayloadBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var store = new MqttRetainedMessages(maxTopics, maxTotalPayloadBytes);
        foreach (var (topic, payload) in await file.ReadAsync(maxTopics, maxTotalPayloadBytes, cancellationToken))
        {
            store.messages.Add(topic, payload);
            store.totalPayloadBytes += payload.Length;
        }

        store.file = file;
        return store;
    }

    /// <summary>
    /// The most topics the store keeps a message for.
    /// </summary>
    public int MaxTopics { get; }

    /// <summary>
    /// The most payload bytes the store keeps, all topics together.
    /// </summary>
    public long MaxTotalPayloadBytes { get; }

    /// <summary>
    /// Keeps <paramref name="payload"/> as the message on <paramref name="topic"/>, replacing
    /// the one kept before. An empty payload removes the topic's message instead, as a
    /// zero-byte retained message does in MQTT 3.1.1 (section 3.3.1.3).
    /// </summary>
    /// <param name="topic">The topic name: at least one character, and no <c>+</c> or <c>#</c>.</param>
    /// <param name="payload">The message's payload.</param>
    /// <returns>
    /// <see langword="false"/> when keeping the message would take the store past
    /// <see cref="MaxTopics"/> or <see cref="MaxTotalPayloadBytes"/>; the store is then
    /// unchanged. <see langword="true"/> otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="topic"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="topic"/> is not a valid topic name.</exception>
    public bool Retain(string topic, ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(topic);
        if (!MqttTopicFilter.IsValidTopicName(topic))
        {
            throw new ArgumentException("A topic name has at least one character and no + or #.", nameof(topic));
        }

        lock (messagesLock)
        {
            var replacedBytes = messages.TryGetValue(topic, out var replaced) ? replaced.Length : 0;
            if (payload.IsEmpty)
            {
                changeCount += messages.Remove(topic) ? 1 : 0;
            }
            else if (IsOverBounds(replaced is null, totalPayloadBytes - replacedBytes + payload.Length))
            {
                return false;
            }
            else
            {
                messages[topic] = payload.ToArray();
                changeCount++;
            }

            totalPayloadBytes += payload.Length - replacedBytes;
            return true;
        }
    }

    /// <summary>
    /// Writes the store to its file when it has changed since the last write; does nothing for
    /// a store made without one, or when nothing has changed. Writes are serialised, and each
    /// writes the store as it is when the write starts, so the file always holds a state the
    /// store held and the last change wins.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write; the next save writes the change instead.</param>
    /// <returns>A task that completes when the file holds the store's latest state.</returns>
    /// <exception cref="IOException">The file cannot be written; the change stays in memory and
    /// the next save writes it.</exception>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (file is null)
        {
            return;
        }

        await saveLock.WaitAsync(cancellationToken);
        try
        {
            KeyValuePair<string, byte[]>[] snapshot;
            long snapshotChangeCount;
            lock (messagesLock)
            {
                if (changeCount == savedChangeCount)
                {
                    return;
                }

                snapshot = [.. messages];
                snapshotChangeCount = changeCount;
            }

            await file.WriteAsync(snapshot, cancellationToken);
            savedChangeCount = snapshotChangeCount;
        }
        finally
        {
            saveLock.Release();
        }
    }

    /// <summary>
    /// Every kept message whose topic any of <paramref name="filters"/> matches, each once,
    /// in ordinal order of topic.
    /// </summary>
    /// <param name="filters">Valid topic filters.</param>
    /// <returns>Each matching topic with its payload.</returns>
    internal IReadOnlyList<KeyValuePair<string, byte[]>> MatchingAny(IReadOnlyList<string> filters)
    {
        var filterLevels = filters.Distinct(StringComparer.Ordinal).Select(MqttTopicFilter.SplitLevels).ToArray();
        KeyValuePair<string, byte[]>[] snapshot;
        lock (messagesLock)
        {
            snapshot = [.. messages];
        }

        return snapshot
            .Where(message => MatchesAny(filterLevels, message.Key))
            .OrderBy(message => message.Key, StringComparer.Ordinal)
            .ToList();
    }

    private static bool MatchesAny(string[][] filterLevels, string topic)
    {
        var topicLevels = MqttTopicFilter.SplitLevels(topic);
        return filterLevels.Any(levels => MqttTopicFilter.Matches(levels, topicLevels));
    }

    private bool IsOverBounds(bool addsTopic, long newTotalPayloadBytes) =>
        (addsTopic && messages.Count >= MaxTopics) || newTotalPayloadBytes > MaxTotalPayloadBytes;
}
