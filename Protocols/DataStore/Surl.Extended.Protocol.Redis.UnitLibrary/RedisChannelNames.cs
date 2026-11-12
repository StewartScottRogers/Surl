using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Unicode;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Which Redis channels are bus topics (ADR-0100 decision 3.8): a channel whose bytes are valid
/// UTF-8 and whose text is a canonical topic name - non-empty, at most 65535 bytes, without
/// U+0000, <c>+</c> or <c>#</c> - is the topic of the same text, verbatim, unless a
/// <c>--topic-map</c> alias for the <c>redis</c> scheme names another topic. Any other channel
/// stays inside the Redis server.
/// </summary>
internal static class RedisChannelNames
{
    /// <summary>The canonical topic <paramref name="channel"/> is, when it is one, mapped through
    /// the bus's <see cref="TopicNameLookup"/> for the <c>redis</c> scheme (a <c>--topic-map</c> alias applies).</summary>
    /// <param name="channel">The channel's bytes.</param>
    /// <param name="names">The bus's name lookup.</param>
    /// <param name="topic">The topic, when the channel is one.</param>
    /// <returns>Whether the channel is a bus topic.</returns>
    public static bool TryGetTopic(byte[] channel, TopicNameLookup names, [NotNullWhen(true)] out string? topic)
    {
        topic = Utf8.IsValid(channel) && names.ToCanonicalTopic(Scheme, Encoding.UTF8.GetString(channel)) is { IsMapped: true } mapping
            ? mapping.Name
            : null;
        return topic is not null;
    }

    /// <summary>The channel a bus topic is published to Redis clients as, when its name reaches one.</summary>
    /// <param name="topic">The canonical topic.</param>
    /// <param name="names">The bus's name lookup.</param>
    /// <param name="channel">The channel's bytes, when there is one.</param>
    /// <returns>Whether a Redis client can name the topic.</returns>
    public static bool TryGetChannel(string topic, TopicNameLookup names, [NotNullWhen(true)] out byte[]? channel)
    {
        channel = names.FromCanonicalTopic(Scheme, topic) is { IsMapped: true } mapping ? Encoding.UTF8.GetBytes(mapping.Name) : null;
        return channel is not null;
    }

    private const string Scheme = "redis";
}
