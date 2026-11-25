using System.Threading.Channels;
using Surl.MessageBus;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// The subscription one connection holds open on the <see cref="MessageBus.MessageBus"/> when the
/// server was given a hold count (ADR-0014 Amendment 1): its bus subscriptions, the publishes
/// waiting to be sent, and how many <c>PUBLISH</c> packets have been counted against the count.
/// </summary>
/// <remarks>
/// A message several filters match waits once. At most <see cref="MaxWaitingPublishes"/>
/// publishes and <see cref="MaxWaitingPayloadBytes"/> payload bytes wait; one more cancels
/// <see cref="Stopped"/> and nothing more is queued. The bus calls <see cref="Offer"/> from
/// whichever thread is making its deliveries, one call at a time; every other member is the
/// connection's alone.
/// </remarks>
internal sealed class MqttHeldSubscription : IAsyncDisposable
{
    /// <summary>The most publishes that wait to be sent (ADR-0093's session number).</summary>
    public const int MaxWaitingPublishes = 1000;

    /// <summary>The most payload bytes that wait to be sent (ADR-0093's session number).</summary>
    public const long MaxWaitingPayloadBytes = 16 * 1024 * 1024;

    private readonly MessageBus.MessageBus bus;
    private readonly string? account;
    private readonly string scheme;
    private readonly int publishCount;
    private readonly BusConnection busConnection;
    private readonly CancellationTokenSource stop;
    private readonly Channel<MqttLivePublish> waiting = Channel.CreateUnbounded<MqttLivePublish>();
    private readonly Dictionary<string, BusSubscription> subscriptionsByFilter = new(StringComparer.Ordinal);
    private readonly Lock gate = new();
    private long waitingPayloadBytes;
    private bool isClosedToPublishes;
    private int countedPublishes;
    private Task cancelling = Task.CompletedTask;
    private Task nextPublishArrives = Task.CompletedTask;

    /// <summary>
    /// Takes the connection's place on the bus, holding no subscription yet.
    /// </summary>
    /// <param name="bus">The bus the server publishes to.</param>
    /// <param name="account">The connection's checked account, or <see langword="null"/> for an anonymous caller.</param>
    /// <param name="scheme">The connection's scheme, which names a topic.</param>
    /// <param name="publishCount">How many <c>PUBLISH</c> packets end the subscription; 0 for no count.</param>
    /// <param name="exchangeEnded">The exchange's cancellation token.</param>
    public MqttHeldSubscription(MessageBus.MessageBus bus, string? account, string scheme, int publishCount, CancellationToken exchangeEnded)
    {
        this.bus = bus;
        this.account = account;
        this.scheme = scheme;
        this.publishCount = publishCount;
        stop = CancellationTokenSource.CreateLinkedTokenSource(exchangeEnded);
        busConnection = bus.Connect(account);
        busConnection.AfterMessageDelivered = Offer;
    }

    /// <summary>
    /// Cancelled when the exchange is cancelled or the waiting publishes pass a bound.
    /// </summary>
    public CancellationToken Stopped => stop.Token;

    /// <summary>
    /// Whether a publish arrived that would have taken the waiting publishes past a bound.
    /// </summary>
    public bool HasOverflowed { get; private set; }

    /// <summary>
    /// Adds a <c>SUBSCRIBE</c>'s filters, each subscribed on the bus before this returns, so no
    /// later publish is missed. A filter already held, as sent, stays subscribed once.
    /// </summary>
    /// <param name="filters">Each filter as sent and its canonical form.</param>
    /// <returns>Whether each filter was granted, in order, with the retained messages the granted
    /// ones match, each canonical topic once in ordinal order of topic; and how many filters were
    /// refused because the connection holds as many subscriptions as the bus allows.</returns>
    public (MqttRetainedReading Reading, int PastBoundCount) Subscribe(IReadOnlyList<(string Sent, string Canonical)> filters)
    {
        var matching = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var granted = new List<bool>(filters.Count);
        var pastBoundCount = 0;
        foreach (var (sent, canonical) in filters)
        {
            var (outcome, retained) = SubscribeOne(sent, canonical);
            granted.Add(outcome == MessageBusOutcome.Succeeded);
            pastBoundCount += outcome == MessageBusOutcome.TooManySubscriptions ? 1 : 0;
            foreach (var (topic, payload) in retained)
            {
                matching[topic] = payload;
            }
        }

        return (new(granted, [.. matching]), pastBoundCount);
    }

    /// <summary>
    /// Ends the subscription to each filter held; one that is not held is passed over.
    /// </summary>
    /// <param name="sentFilters">The filters as the <c>UNSUBSCRIBE</c> sent them.</param>
    public void Unsubscribe(IReadOnlyList<string> sentFilters)
    {
        foreach (var sent in sentFilters)
        {
            if (subscriptionsByFilter.Remove(sent, out var subscription))
            {
                subscription.Dispose();
            }
        }
    }

    /// <summary>
    /// Queues a live message the connection's subscriptions received, under the name the scheme
    /// gives its topic; one on a topic the scheme cannot name is left out. It does not block.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Offer(BusMessage message)
    {
        var mapping = bus.Names.FromCanonicalTopic(scheme, message.Topic);
        if (!mapping.IsMapped)
        {
            return;
        }

        lock (gate)
        {
            if (!isClosedToPublishes)
            {
                QueueWithinTheBounds(new MqttLivePublish(mapping.Name, message));
            }
        }
    }

    /// <summary>
    /// Takes the publish that has waited longest, if any waits.
    /// </summary>
    /// <param name="publish">The publish.</param>
    /// <returns>Whether one waited.</returns>
    public bool TryTake(out MqttLivePublish publish)
    {
        lock (gate)
        {
            var taken = waiting.Reader.TryRead(out publish);
            waitingPayloadBytes -= taken ? publish.Message.Payload.Length : 0;
            return taken;
        }
    }

    /// <summary>
    /// Completes when a publish waits, or is cancelled with <see cref="Stopped"/>. The same task
    /// is answered until it has completed.
    /// </summary>
    /// <returns>The task.</returns>
    public Task WaitForPublishAsync()
    {
        if (nextPublishArrives.IsCompleted)
        {
            nextPublishArrives = waiting.Reader.WaitToReadAsync(Stopped).AsTask();
        }

        return nextPublishArrives;
    }

    /// <summary>
    /// Counts one <c>PUBLISH</c> packet about to be sent, retained or live. At the count-th, no
    /// publish is queued afterwards, so no backlog can cancel the writes that end the
    /// subscription.
    /// </summary>
    /// <returns>Whether it is the count-th, which ends the subscription; never with a count of 0.</returns>
    public bool CountPublishToSend()
    {
        if (publishCount == 0 || ++countedPublishes < publishCount)
        {
            return false;
        }

        CloseToPublishes();
        return true;
    }

    /// <summary>
    /// Ends every subscription; no publish is queued afterwards.
    /// </summary>
    /// <returns>A task that completes when the bus connection is gone.</returns>
    public async ValueTask DisposeAsync()
    {
        CloseToPublishes();
        busConnection.Dispose();
        await cancelling;
        stop.Dispose();
    }

    private (MessageBusOutcome Outcome, IReadOnlyList<KeyValuePair<string, byte[]>> Retained) SubscribeOne(string sent, string canonical)
    {
        // Already held: the subscription goes on, so nothing published meanwhile is missed, and
        // only the retained messages are read again.
        if (subscriptionsByFilter.ContainsKey(sent))
        {
            return (MessageBusOutcome.Succeeded, MqttBusRetainedMessages.Read(bus, account, [canonical]).Matching);
        }

        var subscribed = busConnection.Subscribe(canonical, MqttBusRetainedMessages.IgnoreLiveMessage, includeRetained: true);
        if (subscribed.Subscription is { } subscription)
        {
            subscriptionsByFilter.Add(sent, subscription);
        }

        return (subscribed.Outcome, [.. subscribed.RetainedMessages.Select(message => KeyValuePair.Create(message.Topic, message.Payload.ToArray()))]);
    }

    private void CloseToPublishes()
    {
        lock (gate)
        {
            isClosedToPublishes = true;
        }
    }

    // Called with the gate held.
    private void QueueWithinTheBounds(MqttLivePublish publish)
    {
        var payloadBytes = waitingPayloadBytes + publish.Message.Payload.Length;
        if (waiting.Reader.Count == MaxWaitingPublishes || payloadBytes > MaxWaitingPayloadBytes)
        {
            isClosedToPublishes = true;
            HasOverflowed = true;
            cancelling = stop.CancelAsync();
            return;
        }

        waitingPayloadBytes = payloadBytes;
        waiting.Writer.TryWrite(publish);
    }
}
