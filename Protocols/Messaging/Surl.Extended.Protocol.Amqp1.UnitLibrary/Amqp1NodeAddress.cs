namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Maps a link's source or target address to the node it names (ADR-0107 decision 4; Part 3
/// sections 3.5.3 and 3.5.4 leave addresses opaque).
/// </summary>
public static class Amqp1NodeAddress
{
    /// <summary>The prefix of a canonical topic address.</summary>
    public const string TopicPrefix = "/topic/";

    /// <summary>The prefix of a canonical queue address.</summary>
    public const string QueuePrefix = "/queue/";

    private const string BareTopicPrefix = "topic/";

    private const string BareQueuePrefix = "queue/";

    /// <summary>
    /// The canonical address <c>/topic/&lt;t&gt;</c> or <c>/queue/&lt;q&gt;</c> an address names: one
    /// leading <c>/</c> is dropped, <c>topic/&lt;t&gt;</c> is the topic <c>&lt;t&gt;</c>,
    /// <c>queue/&lt;q&gt;</c> the queue <c>&lt;q&gt;</c>, and any other address the queue of that name.
    /// </summary>
    /// <param name="address">The address the client's source or target carries.</param>
    /// <param name="forSending">
    /// Whether the client sends to the node; a topic it sends to may not hold a wildcard (<c>+</c>, <c>#</c>).
    /// </param>
    /// <returns>The canonical address; <see langword="null"/> when the address names no node.</returns>
    public static string? Canonical(string? address, bool forSending)
    {
        var path = WithoutOneLeadingSlash(address ?? string.Empty);
        return path.StartsWith(BareTopicPrefix, StringComparison.Ordinal)
            ? CanonicalTopic(path[BareTopicPrefix.Length..], forSending)
            : CanonicalQueue(path);
    }

    private static string WithoutOneLeadingSlash(string address) => address.StartsWith('/') ? address[1..] : address;

    private static string? CanonicalTopic(string topic, bool forSending) =>
        topic.Length == 0 || (forSending && HasWildcard(topic)) ? null : TopicPrefix + topic;

    private static bool HasWildcard(string topic) => topic.AsSpan().IndexOfAny('+', '#') >= 0;

    private static string? CanonicalQueue(string path)
    {
        var queue = path.StartsWith(BareQueuePrefix, StringComparison.Ordinal) ? path[BareQueuePrefix.Length..] : path;
        return queue.Length == 0 ? null : QueuePrefix + queue;
    }

    /// <summary>Whether a canonical address names a topic.</summary>
    /// <param name="canonicalAddress">An address <see cref="Canonical"/> returned.</param>
    /// <returns><see langword="true"/> for <c>/topic/</c>; <see langword="false"/> for a queue.</returns>
    public static bool IsTopic(string canonicalAddress)
    {
        ArgumentNullException.ThrowIfNull(canonicalAddress);

        return canonicalAddress.StartsWith(TopicPrefix, StringComparison.Ordinal);
    }
}
