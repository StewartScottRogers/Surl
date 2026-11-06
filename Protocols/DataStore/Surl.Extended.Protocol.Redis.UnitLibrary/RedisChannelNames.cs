using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Unicode;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Which Redis channels are bus topics (ADR-0100 decision 3.8): a channel whose bytes are valid
/// UTF-8 and whose text is a canonical topic name - non-empty, at most 65535 bytes, without
/// U+0000, <c>+</c> or <c>#</c> - is the topic of the same text, verbatim. Any other channel stays
/// inside the Redis server.
/// </summary>
internal static class RedisChannelNames
{
    /// <summary>The canonical topic <paramref name="channel"/> is, when it is one.</summary>
    /// <param name="channel">The channel's bytes.</param>
    /// <param name="topic">The topic, when the channel is one.</param>
    /// <returns>Whether the channel is a bus topic.</returns>
    public static bool TryGetTopic(byte[] channel, [NotNullWhen(true)] out string? topic)
    {
        topic = Utf8.IsValid(channel) ? Encoding.UTF8.GetString(channel) : null;
        return CanonicalTopicName.IsValidTopic(topic);
    }
}
