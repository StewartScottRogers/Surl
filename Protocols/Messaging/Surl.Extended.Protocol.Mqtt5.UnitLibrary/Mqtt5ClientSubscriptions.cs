using Surl.MessageBus;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// One session's subscriptions (ADR-0093 decision 5), each a subscription on the message
/// bus, keyed by the Topic Filter exactly as the client sent it. It subscribes with the options of
/// section 3.8.3.1, replaces a subscription to an identical filter (<c>[MQTT-3.8.4-3]</c>),
/// unsubscribes (section 3.10), and hands what the subscriptions receive to
/// the session's queue: a message matching several non-shared subscriptions once,
/// at the highest QoS they grant, with every Subscription Identifier they carry
/// (<c>[MQTT-3.3.4-2]</c>, <c>-4</c>); a shared subscription's delivery on its own.
/// </summary>
internal sealed class Mqtt5ClientSubscriptions
{
    private readonly Dictionary<string, ClientSubscription> subscriptionsByFilter = new(StringComparer.Ordinal);
    private readonly BusConnection busConnection;
    private readonly TopicNameLookup names;
    private readonly string scheme;
    private readonly Action<BusMessage, int, bool, IReadOnlyList<uint>> offer;

    // The non-shared subscriptions a message has reached so far; the bus hands a message to all
    // of a connection's subscriptions, one call at a time, before AfterMessageDelivered.
    private GatheredDelivery? gathered;

    /// <summary>
    /// Prepares the client's subscriptions on <paramref name="busConnection"/>, and takes its
    /// <see cref="BusConnection.AfterMessageDelivered"/>.
    /// </summary>
    /// <param name="busConnection">The client's place on the bus, made for its account.</param>
    /// <param name="names">The bus's name lookup.</param>
    /// <param name="scheme">The connection's scheme, which names the mapping.</param>
    /// <param name="offer">Queues what the subscriptions receive for the client: the message, the QoS granted, the RETAIN flag and the Subscription Identifiers.</param>
    public Mqtt5ClientSubscriptions(BusConnection busConnection, TopicNameLookup names, string scheme, Action<BusMessage, int, bool, IReadOnlyList<uint>> offer)
    {
        this.busConnection = busConnection;
        this.names = names;
        this.scheme = scheme;
        this.offer = offer;
        busConnection.AfterMessageDelivered = SendGathered;
    }

    /// <summary>
    /// Subscribes to one Topic Filter, replacing the client's subscription to an identical one.
    /// Retain Handling 0 offers the matching retained messages at every subscribe, 1 only when the
    /// subscription is new, 2 never; a shared subscription never (section 4.8.2).
    /// </summary>
    /// <param name="subscription">The Topic Filter and its options.</param>
    /// <param name="subscriptionIdentifier">The <c>SUBSCRIBE</c>'s Subscription Identifier, or <see langword="null"/>.</param>
    /// <param name="retained">Receives the retained messages to send after the <c>SUBACK</c>.</param>
    /// <returns>The filter's <c>SUBACK</c> Reason Code: the granted QoS, <c>0x8F</c> for a filter
    /// the bus cannot take, <c>0x87</c> when topic access refuses it, <c>0x97</c> past the bus's
    /// subscriptions a connection.</returns>
    public Mqtt5ReasonCode Subscribe(Mqtt5Subscription subscription, uint? subscriptionIdentifier, List<Mqtt5Delivery> retained)
    {
        var filter = names.ToCanonicalFilter(scheme, subscription.TopicFilter);
        if (!filter.IsMapped)
        {
            return Mqtt5ReasonCode.TopicFilterInvalid;
        }

        var isNew = !subscriptionsByFilter.Remove(subscription.TopicFilter, out var replaced);
        replaced?.BusSubscription!.Dispose();
        var clientSubscription = new ClientSubscription(subscription, subscriptionIdentifier, filter.Group is not null);
        var includeRetained = filter.Group is null && SendsRetainedMessages(subscription.RetainHandling, isNew);
        var result = busConnection.Subscribe(filter.Name!, message => Receive(clientSubscription, message), filter.Group, includeRetained, subscription.NoLocal);
        if (result.Subscription is null)
        {
            return result.Outcome == MessageBusOutcome.AccessDenied ? Mqtt5ReasonCode.NotAuthorized : Mqtt5ReasonCode.QuotaExceeded;
        }

        clientSubscription.BusSubscription = result.Subscription;
        subscriptionsByFilter.Add(subscription.TopicFilter, clientSubscription);
        var granted = subscription.MaximumQualityOfService;
        retained.AddRange(result.RetainedMessages.Select(message => new Mqtt5Delivery(message, granted, Retain: true, clientSubscription.Identifiers)));
        return (Mqtt5ReasonCode)granted;
    }

    /// <summary>
    /// Deletes the client's subscription to a Topic Filter identical to <paramref name="topicFilter"/>.
    /// </summary>
    /// <param name="topicFilter">The Topic Filter as the <c>UNSUBSCRIBE</c> sent it.</param>
    /// <returns><c>0x00</c> when one was deleted, <c>0x11</c> No subscription existed when none was
    /// (<c>[MQTT-3.10.4-5]</c>), <c>0x8F</c> for an invalid filter.</returns>
    public Mqtt5ReasonCode Unsubscribe(string topicFilter)
    {
        if (!names.ToCanonicalFilter(scheme, topicFilter).IsMapped)
        {
            return Mqtt5ReasonCode.TopicFilterInvalid;
        }

        if (!subscriptionsByFilter.Remove(topicFilter, out var removed))
        {
            return Mqtt5ReasonCode.NoSubscriptionExisted;
        }

        removed.BusSubscription!.Dispose();
        return Mqtt5ReasonCode.Success;
    }

    // [MQTT-3.3.1-9] to -11: Retain Handling 0 at every subscribe, 1 only for a new subscription, 2 never.
    private static bool SendsRetainedMessages(int retainHandling, bool isNew) =>
        retainHandling == 0 || (retainHandling == 1 && isNew);

    // Retain As Published keeps the RETAIN flag of a live message; otherwise it goes as 0
    // ([MQTT-3.3.1-12], -13).
    private void Receive(ClientSubscription subscription, BusMessage message)
    {
        var retain = subscription.Options.RetainAsPublished && message.Retain;
        if (subscription.IsShared)
        {
            offer(message, subscription.Options.MaximumQualityOfService, retain, subscription.Identifiers);
            return;
        }

        gathered ??= new GatheredDelivery();
        gathered.Add(subscription.Options.MaximumQualityOfService, retain, subscription.Identifiers);
    }

    private void SendGathered(BusMessage message)
    {
        if (gathered is not { } delivery)
        {
            return;
        }

        gathered = null;
        offer(message, delivery.QualityOfService, delivery.Retain, delivery.Identifiers);
    }

    private sealed class ClientSubscription(Mqtt5Subscription options, uint? identifier, bool isShared)
    {
        public Mqtt5Subscription Options { get; } = options;

        public IReadOnlyList<uint> Identifiers { get; } = identifier is { } present ? [present] : [];

        public bool IsShared { get; } = isShared;

        public BusSubscription? BusSubscription { get; set; }
    }

    // The overlap rule: the highest granted QoS, RETAIN when any subscription keeps it, and every identifier.
    private sealed class GatheredDelivery
    {
        private readonly List<uint> identifiers = [];

        public int QualityOfService { get; private set; }

        public bool Retain { get; private set; }

        public IReadOnlyList<uint> Identifiers => identifiers;

        public void Add(int qualityOfService, bool retain, IReadOnlyList<uint> subscriptionIdentifiers)
        {
            QualityOfService = Math.Max(QualityOfService, qualityOfService);
            Retain |= retain;
            identifiers.AddRange(subscriptionIdentifiers);
        }
    }
}
