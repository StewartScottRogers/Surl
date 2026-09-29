namespace Surl.Protocol.Mqtt;

/// <summary>
/// The last message published to each topic, kept in memory for as long as the MQTT server
/// that holds it. Every connection the server answers shares it, so a message one client
/// publishes is what the next client to subscribe receives. Safe for concurrent use.
/// </summary>
/// <remarks>
/// It is bounded, because every peer can publish to it and it outlives each connection
/// (ADR-0014, decision 7): at most <see cref="MaxTopics"/> topics and
/// <see cref="MaxTotalPayloadBytes"/> payload bytes in all. A message that would take it past
/// either is not kept.
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
    private long totalPayloadBytes;

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
                messages.Remove(topic);
            }
            else if (IsOverBounds(replaced is null, totalPayloadBytes - replacedBytes + payload.Length))
            {
                return false;
            }
            else
            {
                messages[topic] = payload.ToArray();
            }

            totalPayloadBytes += payload.Length - replacedBytes;
            return true;
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
