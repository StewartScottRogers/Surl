namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// One connection as pub/sub sees it (the "Pub/Sub" page; ADR-0106 decision 1): the channels and
/// patterns it subscribes to, in the order subscribed, and how a message reaches it. Its sets are
/// changed only under <see cref="RedisPubSub"/>'s lock.
/// </summary>
/// <param name="session">The connection's state, whose RESP version shapes each message.</param>
/// <param name="writer">Writes the connection's bytes.</param>
internal sealed class RedisSubscriber(RedisSession session, RedisReplyWriter writer)
{
    /// <summary>The connection's state.</summary>
    public RedisSession Session { get; } = session;

    /// <summary>The channels subscribed to, by name (one character per byte), in the order subscribed.</summary>
    public OrderedDictionary<string, byte[]> Channels { get; } = new(StringComparer.Ordinal);

    /// <summary>The patterns subscribed to, by pattern (one character per byte), in the order subscribed.</summary>
    public OrderedDictionary<string, byte[]> Patterns { get; } = new(StringComparer.Ordinal);

    /// <summary>How many channels and patterns the connection subscribes to: the count confirmations carry.</summary>
    public int SubscriptionCount => Channels.Count + Patterns.Count;

    /// <summary>Whether the connection subscribes to any channel or pattern.</summary>
    public bool IsSubscribed => SubscriptionCount > 0;

    /// <summary>
    /// A confirmation or message of <paramref name="elements"/>: push data in RESP3, an array in
    /// RESP2 (ADR-0106 decision 2).
    /// </summary>
    /// <param name="elements">The kind, then the rest.</param>
    /// <returns>The reply.</returns>
    public RespValue PubSubReply(params RespValue[] elements) =>
        Session.ProtocolVersion == 3 ? new RespPush(elements) : new RespArray(elements);

    /// <summary>Sends the message of <paramref name="elements"/> without waiting for it to be written.</summary>
    /// <param name="elements">The kind, then the rest.</param>
    public void Deliver(params RespValue[] elements) => writer.Deliver(PubSubReply(elements).Encode());
}
