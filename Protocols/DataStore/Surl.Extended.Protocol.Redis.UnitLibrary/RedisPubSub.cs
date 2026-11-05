namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The server's channels and patterns (the "Pub/Sub" page; ADR-0106 decision 1): who subscribes
/// to each, shared by every connection of one <see cref="RedisServer"/>. A message published to a
/// channel reaches each connection subscribing to it, and each pattern subscription matching it,
/// so a connection subscribing both ways receives it twice. Until BL-678 delivery stays among
/// this server's connections.
/// </summary>
/// <param name="maxSubscriptionsPerConnection">The most channels and patterns one connection subscribes to.</param>
/// <param name="maxUnsentMessageBytes">The most bytes of messages unsent to one subscriber before it is closed.</param>
internal sealed class RedisPubSub(int maxSubscriptionsPerConnection, long maxUnsentMessageBytes)
{
    /// <summary>The most channels and patterns one connection subscribes to (ADR-0106 decision 6).</summary>
    public const int DefaultMaxSubscriptionsPerConnection = 10_000;

    /// <summary>The most bytes of unsent messages a subscriber may have: 32 MiB (ADR-0106 decision 6).</summary>
    public const long DefaultMaxUnsentMessageBytes = 32L * 1024 * 1024;

    private readonly Lock syncRoot = new();
    private readonly Dictionary<string, List<RedisSubscriber>> channelSubscribers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<RedisSubscriber>> patternSubscribers = new(StringComparer.Ordinal);

    /// <summary>Creates the registry with ADR-0106 decision 6's bounds.</summary>
    public RedisPubSub()
        : this(DefaultMaxSubscriptionsPerConnection, DefaultMaxUnsentMessageBytes)
    {
    }

    /// <summary>The most bytes of messages unsent to one subscriber before it is closed.</summary>
    public long MaxUnsentMessageBytes { get; } = maxUnsentMessageBytes;

    /// <summary>
    /// Subscribes <paramref name="subscriber"/> to <paramref name="names"/>, answering one
    /// confirmation each - its kind, the name and the connection's count - or, when that would pass
    /// the bound, <c>-ERR max number of subscriptions reached</c> with nothing subscribed.
    /// </summary>
    /// <param name="subscriber">The connection.</param>
    /// <param name="names">The channels or patterns.</param>
    /// <param name="isPattern">Whether they are patterns (<c>PSUBSCRIBE</c>).</param>
    /// <returns>The reply.</returns>
    public RespValue Subscribe(RedisSubscriber subscriber, IReadOnlyList<byte[]> names, bool isPattern)
    {
        var (own, registry, kind) = Side(subscriber, isPattern, "subscribe");
        lock (syncRoot)
        {
            var added = names.Select(RedisKeys.Name).Distinct(StringComparer.Ordinal).Count(name => !own.ContainsKey(name));
            if (subscriber.SubscriptionCount + added > maxSubscriptionsPerConnection)
            {
                return RedisErrorReplies.MaxNumberOfSubscriptionsReached;
            }

            var replies = new List<RespValue>();
            foreach (var name in names)
            {
                if (own.TryAdd(RedisKeys.Name(name), name))
                {
                    SubscribersOf(registry, RedisKeys.Name(name)).Add(subscriber);
                }

                replies.Add(Confirmation(subscriber, kind, new RespBulkString(name)));
            }

            return new RedisReplySequence(replies);
        }
    }

    /// <summary>
    /// Unsubscribes <paramref name="subscriber"/> from <paramref name="names"/>, or from every
    /// channel or pattern when none is named, answering one confirmation each; with none to leave,
    /// one confirmation with a null name.
    /// </summary>
    /// <param name="subscriber">The connection.</param>
    /// <param name="names">The channels or patterns; empty for all.</param>
    /// <param name="isPattern">Whether they are patterns (<c>PUNSUBSCRIBE</c>).</param>
    /// <returns>The reply.</returns>
    public RespValue Unsubscribe(RedisSubscriber subscriber, IReadOnlyList<byte[]> names, bool isPattern)
    {
        var (own, registry, kind) = Side(subscriber, isPattern, "unsubscribe");
        lock (syncRoot)
        {
            var leaving = names.Count > 0 ? names : [.. own.Values];
            if (leaving.Count == 0)
            {
                return new RedisReplySequence([Confirmation(subscriber, kind, subscriber.Session.NullReply)]);
            }

            var replies = new List<RespValue>();
            foreach (var name in leaving)
            {
                Leave(subscriber, own, registry, RedisKeys.Name(name));
                replies.Add(Confirmation(subscriber, kind, new RespBulkString(name)));
            }

            return new RedisReplySequence(replies);
        }
    }

    /// <summary>Unsubscribes <paramref name="subscriber"/> from everything, answering nothing: <c>RESET</c>, or the connection ending.</summary>
    /// <param name="subscriber">The connection.</param>
    public void UnsubscribeAll(RedisSubscriber subscriber)
    {
        lock (syncRoot)
        {
            foreach (var channel in subscriber.Channels.Keys.ToList())
            {
                Leave(subscriber, subscriber.Channels, channelSubscribers, channel);
            }

            foreach (var pattern in subscriber.Patterns.Keys.ToList())
            {
                Leave(subscriber, subscriber.Patterns, patternSubscribers, pattern);
            }
        }
    }

    /// <summary>
    /// Sends <paramref name="message"/> to every subscription <paramref name="channel"/> reaches:
    /// <c>message</c>, channel, payload to a channel's subscribers and <c>pmessage</c>, pattern,
    /// channel, payload to each matching pattern's.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="message">The payload.</param>
    /// <returns>How many subscriptions it was sent to (<c>PUBLISH</c>'s answer).</returns>
    public int Publish(byte[] channel, byte[] message)
    {
        var name = RedisKeys.Name(channel);
        List<(RedisSubscriber Subscriber, byte[]? Pattern)> targets;
        lock (syncRoot)
        {
            targets = [.. channelSubscribers.GetValueOrDefault(name, []).Select(subscriber => (subscriber, (byte[]?)null))];
            foreach (var (pattern, subscribers) in patternSubscribers.Where(entry => RedisGlobPattern.IsMatch(entry.Key, name)))
            {
                targets.AddRange(subscribers.Select(subscriber => (subscriber, (byte[]?)RedisKeys.Bytes(pattern))));
            }
        }

        foreach (var (subscriber, pattern) in targets)
        {
            if (pattern is null)
            {
                subscriber.Deliver(new RespBulkString("message"), new RespBulkString(channel), new RespBulkString(message));
            }
            else
            {
                subscriber.Deliver(new RespBulkString("pmessage"), new RespBulkString(pattern), new RespBulkString(channel), new RespBulkString(message));
            }
        }

        return targets.Count;
    }

    private (OrderedDictionary<string, byte[]> Own, Dictionary<string, List<RedisSubscriber>> Registry, string Kind) Side(
        RedisSubscriber subscriber, bool isPattern, string kind) =>
        isPattern ? (subscriber.Patterns, patternSubscribers, "p" + kind) : (subscriber.Channels, channelSubscribers, kind);

    private static RespValue Confirmation(RedisSubscriber subscriber, string kind, RespValue name) =>
        subscriber.PubSubReply(new RespBulkString(kind), name, new RespInteger(subscriber.SubscriptionCount));

    private static List<RedisSubscriber> SubscribersOf(Dictionary<string, List<RedisSubscriber>> registry, string name)
    {
        if (!registry.TryGetValue(name, out var subscribers))
        {
            subscribers = [];
            registry.Add(name, subscribers);
        }

        return subscribers;
    }

    private static void Leave(
        RedisSubscriber subscriber,
        OrderedDictionary<string, byte[]> own,
        Dictionary<string, List<RedisSubscriber>> registry,
        string name)
    {
        if (!own.Remove(name))
        {
            return;
        }

        var subscribers = registry[name];
        subscribers.Remove(subscriber);
        if (subscribers.Count == 0)
        {
            registry.Remove(name);
        }
    }
}
