using System.Text;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The server's channels and patterns (the "Pub/Sub" page; ADR-0106 decision 1; ADR-0100 decision
/// 3.8): who subscribes to each, shared by every connection of one <see cref="RedisServer"/>, and
/// the bus the messaging servers share. A channel that is a bus topic (<see cref="RedisChannelNames"/>)
/// is published and subscribed on the bus, so it reaches and hears every protocol's subscribers
/// and publishers of that topic; any other channel stays among this server's connections. A
/// connection with a pattern holds one bus subscription to <c>#</c> and matches each topic it is
/// handed against its patterns itself. A message reaches each connection subscribing to its
/// channel, and each pattern subscription matching it, so a connection subscribing both ways
/// receives it twice: the <c>message</c> first, then each <c>pmessage</c>.
/// </summary>
/// <param name="bus">The message bus the messaging servers of the process share.</param>
/// <param name="maxSubscriptionsPerConnection">The most channels and patterns one connection subscribes to.</param>
/// <param name="maxUnsentMessageBytes">The most bytes of messages unsent to one subscriber before it is closed.</param>
internal sealed class RedisPubSub(MessageBus.MessageBus bus, int maxSubscriptionsPerConnection, long maxUnsentMessageBytes)
{
    /// <summary>The most channels and patterns one connection subscribes to (ADR-0106 decision 6).</summary>
    public const int DefaultMaxSubscriptionsPerConnection = 10_000;

    /// <summary>The most bytes of unsent messages a subscriber may have: 32 MiB (ADR-0106 decision 6).</summary>
    public const long DefaultMaxUnsentMessageBytes = 32L * 1024 * 1024;

    /// <summary>The bus filter a connection with patterns subscribes to: every topic not beginning with <c>$</c>.</summary>
    private const string EveryTopic = "#";

    private readonly Lock syncRoot = new();
    private readonly Dictionary<string, List<RedisSubscriber>> channelSubscribers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<RedisSubscriber>> patternSubscribers = new(StringComparer.Ordinal);

    /// <summary>Creates the registry on <paramref name="bus"/> with ADR-0106 decision 6's bounds.</summary>
    /// <param name="bus">The message bus the messaging servers of the process share.</param>
    public RedisPubSub(MessageBus.MessageBus bus)
        : this(bus, DefaultMaxSubscriptionsPerConnection, DefaultMaxUnsentMessageBytes)
    {
    }

    /// <summary>The most bytes of messages unsent to one subscriber before it is closed.</summary>
    public long MaxUnsentMessageBytes { get; } = maxUnsentMessageBytes;

    /// <summary>
    /// Subscribes <paramref name="subscriber"/> to <paramref name="names"/>, answering one
    /// confirmation each - its kind, the name and the connection's count - or, with nothing
    /// subscribed, <c>-ERR max number of subscriptions reached</c> when that would pass the bound
    /// or the bus's, and <c>-NOPERM …</c> when the bus's grants refuse a bus subscription.
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
            var joined = new List<string>();
            foreach (var name in names)
            {
                var key = RedisKeys.Name(name);
                if (own.TryAdd(key, name))
                {
                    joined.Add(key);
                    var outcome = Join(subscriber, registry, key, name, isPattern);
                    if (outcome is not MessageBusOutcome.Succeeded)
                    {
                        joined.ForEach(joinedKey => Leave(subscriber, own, registry, joinedKey));
                        return outcome is MessageBusOutcome.AccessDenied
                            ? RedisErrorReplies.NoPermissionOnChannels
                            : RedisErrorReplies.MaxNumberOfSubscriptionsReached;
                    }
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
    /// Publishes <paramref name="message"/> on <paramref name="channel"/> for
    /// <paramref name="account"/>: on the bus when the channel is a bus topic, answering how many
    /// live subscriptions of any protocol it reached, each matching Redis pattern counted as one;
    /// otherwise to this server's channel and matching pattern subscriptions, answering how many.
    /// A bus refusal is <c>-NOPERM …</c> for the grants and <c>-OOM …</c> for a bound.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="message">The payload.</param>
    /// <param name="account">The connection's login, <see langword="null"/> for anonymous.</param>
    /// <returns><c>PUBLISH</c>'s answer.</returns>
    public RespValue Publish(byte[] channel, byte[] message, string? account)
    {
        if (!RedisChannelNames.TryGetTopic(channel, out var topic))
        {
            return new RespInteger(PublishAmongConnections(channel, message));
        }

        int patternMatchesBeyondOne;
        lock (syncRoot)
        {
            patternMatchesBeyondOne = topic.StartsWith('$')
                ? 0
                : PatternHolders().Sum(subscriber => MatchingPatterns(subscriber, topic).Count() - 1);
        }

        var result = bus.Publish(new BusMessage(topic, message), account);
        return result.Outcome switch
        {
            MessageBusOutcome.Succeeded => new RespInteger(result.DeliveredSubscriptionCount + patternMatchesBeyondOne),
            MessageBusOutcome.AccessDenied => RedisErrorReplies.NoPermissionOnChannels,
            _ => RedisErrorReplies.OutOfMemory,
        };
    }

    private int PublishAmongConnections(byte[] channel, byte[] message)
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

    // Each connection holding a pattern, once.
    private IEnumerable<RedisSubscriber> PatternHolders() =>
        patternSubscribers.Values.SelectMany(subscribers => subscribers).Distinct();

    // The connection's patterns matching the topic, by their bytes, in the order subscribed.
    private static IEnumerable<byte[]> MatchingPatterns(RedisSubscriber subscriber, string topic)
    {
        var name = RedisKeys.Name(Encoding.UTF8.GetBytes(topic));
        return subscriber.Patterns.Where(pattern => RedisGlobPattern.IsMatch(pattern.Key, name)).Select(pattern => pattern.Value);
    }

    // Registers a new name: a bus subscription for a channel that is a bus topic, the server's
    // registry otherwise, and for a pattern also the connection's one subscription to every topic.
    private MessageBusOutcome Join(RedisSubscriber subscriber, Dictionary<string, List<RedisSubscriber>> registry, string key, byte[] name, bool isPattern)
    {
        if (!isPattern && RedisChannelNames.TryGetTopic(name, out var topic))
        {
            var result = ConnectToBus(subscriber).Subscribe(
                topic,
                delivered => subscriber.Deliver(new RespBulkString("message"), new RespBulkString(name), new RespBulkString(delivered.Payload.ToArray())));
            if (result.Subscription is { } subscription)
            {
                subscriber.ChannelBusSubscriptions.Add(key, subscription);
            }

            return result.Outcome;
        }

        SubscribersOf(registry, key).Add(subscriber);
        return isPattern ? SubscribeToEveryTopic(subscriber) : MessageBusOutcome.Succeeded;
    }

    private MessageBusOutcome SubscribeToEveryTopic(RedisSubscriber subscriber)
    {
        if (subscriber.PatternBusSubscription is not null)
        {
            return MessageBusOutcome.Succeeded;
        }

        var result = ConnectToBus(subscriber).Subscribe(EveryTopic, delivered => subscriber.MessageReachingPatterns = delivered);
        subscriber.PatternBusSubscription = result.Subscription;
        return result.Outcome;
    }

    private BusConnection ConnectToBus(RedisSubscriber subscriber)
    {
        if (subscriber.BusConnection is { } connection)
        {
            return connection;
        }

        connection = bus.Connect(subscriber.Session.UserName);
        connection.AfterMessageDelivered = delivered => DeliverPatternMessages(subscriber, delivered);
        subscriber.BusConnection = connection;
        return connection;
    }

    // Sends a bus message's pmessages once the connection's channel subscriptions have had it.
    private void DeliverPatternMessages(RedisSubscriber subscriber, BusMessage delivered)
    {
        if (!ReferenceEquals(subscriber.MessageReachingPatterns, delivered))
        {
            return;
        }

        subscriber.MessageReachingPatterns = null;
        List<byte[]> patterns;
        lock (syncRoot)
        {
            patterns = [.. MatchingPatterns(subscriber, delivered.Topic)];
        }

        var channel = new RespBulkString(Encoding.UTF8.GetBytes(delivered.Topic));
        var payload = new RespBulkString(delivered.Payload.ToArray());
        foreach (var pattern in patterns)
        {
            subscriber.Deliver(new RespBulkString("pmessage"), new RespBulkString(pattern), channel, payload);
        }
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

        if (registry.TryGetValue(name, out var subscribers))
        {
            subscribers.Remove(subscriber);
            if (subscribers.Count == 0)
            {
                registry.Remove(name);
            }
        }

        LeaveBus(subscriber, name, own == subscriber.Patterns);
    }

    // Ends the name's bus subscription, the pattern one once the last pattern goes, and the
    // connection's place on the bus once it holds no bus subscription.
    private static void LeaveBus(RedisSubscriber subscriber, string name, bool isPattern)
    {
        if (!isPattern && subscriber.ChannelBusSubscriptions.Remove(name, out var subscription))
        {
            subscription.Dispose();
        }

        if (subscriber.Patterns.Count == 0 && subscriber.PatternBusSubscription is { } everyTopic)
        {
            everyTopic.Dispose();
            subscriber.PatternBusSubscription = null;
        }

        if (subscriber.ChannelBusSubscriptions.Count == 0 && subscriber.PatternBusSubscription is null && subscriber.BusConnection is { } connection)
        {
            connection.Dispose();
            subscriber.BusConnection = null;
        }
    }
}
