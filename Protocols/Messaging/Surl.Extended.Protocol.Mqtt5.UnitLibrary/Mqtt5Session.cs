using System.Threading.Channels;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// One client's session (MQTT 5.0 section 4.1, ADR-0093 decision 3), which outlives a connection
/// for its Session Expiry Interval: its subscriptions on the message bus, the messages waiting to
/// go to it, the QoS 1 and 2 <c>PUBLISH</c>es sent to it and not yet acknowledged, and the QoS 2
/// Packet Identifiers it sent whose <c>PUBREL</c> has not come. <see cref="Mqtt5BrokerState"/>
/// keeps it while no connection holds it.
/// </summary>
/// <remarks>
/// The waiting messages are bounded by count and by payload bytes, connected or not; a message
/// past either bound is dropped for this session. While no connection holds the session, a
/// message that would go at QoS 0 is not queued at all (section 4.1 allows it).
/// </remarks>
internal sealed class Mqtt5Session : IDisposable
{
    /// <summary>The most messages that wait for one session at once (ADR-0093 decision 3).</summary>
    public const int MaxQueuedMessages = 1000;

    /// <summary>The most payload bytes that wait for one session at once, 16 MiB (ADR-0093 decision 3).</summary>
    public const long MaxQueuedPayloadBytes = 16 * 1024 * 1024;

    private readonly Channel<Mqtt5Delivery> queued;
    private readonly long maxQueuedPayloadBytes;
    private long queuedPayloadBytes;
    private volatile bool isConnected = true;

    /// <summary>
    /// Starts a session held by the connection that opens it.
    /// </summary>
    /// <param name="clientIdentifier">The client identifier the session belongs to.</param>
    /// <param name="busConnection">The session's place on the bus, made for its client's account.</param>
    /// <param name="names">The bus's name lookup.</param>
    /// <param name="scheme">The scheme of the connection that opens it, which names the topic mapping.</param>
    /// <param name="maxQueuedMessages">The most messages that wait for it at once.</param>
    /// <param name="maxQueuedPayloadBytes">The most payload bytes that wait for it at once.</param>
    public Mqtt5Session(string clientIdentifier, BusConnection busConnection, TopicNameLookup names, string scheme, int maxQueuedMessages, long maxQueuedPayloadBytes)
    {
        ClientIdentifier = clientIdentifier;
        BusConnection = busConnection;
        this.maxQueuedPayloadBytes = maxQueuedPayloadBytes;
        queued = Channel.CreateBounded<Mqtt5Delivery>(new BoundedChannelOptions(maxQueuedMessages) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
        Subscriptions = new Mqtt5ClientSubscriptions(busConnection, names, scheme, Offer);
    }

    /// <summary>The client identifier the session belongs to.</summary>
    public string ClientIdentifier { get; }

    /// <summary>The account the session publishes and subscribes as; <see langword="null"/> for an anonymous client.</summary>
    public string? Account => BusConnection.Account;

    /// <summary>The session's place on the bus.</summary>
    public BusConnection BusConnection { get; }

    /// <summary>The session's subscriptions.</summary>
    public Mqtt5ClientSubscriptions Subscriptions { get; }

    /// <summary>The QoS 2 Packet Identifiers the client sent whose <c>PUBREL</c> has not come (method B of section 4.3.3).</summary>
    public HashSet<ushort> AwaitingRelease { get; } = [];

    /// <summary>The QoS 1 and 2 <c>PUBLISH</c>es sent to the client and not yet acknowledged, by Packet Identifier; guarded by <see cref="InFlightGate"/>.</summary>
    public Dictionary<ushort, Mqtt5InFlightPublish> InFlight { get; } = [];

    /// <summary>Guards <see cref="InFlight"/>, <see cref="LastPacketIdentifier"/> and <see cref="LastInFlightSequence"/>.</summary>
    public Lock InFlightGate { get; } = new();

    /// <summary>The Packet Identifier given last; the next is the first above it, after 65535 wrapping to 1, not in flight.</summary>
    public ushort LastPacketIdentifier { get; set; }

    /// <summary>The sending order of the <c>PUBLISH</c> put in flight last.</summary>
    public long LastInFlightSequence { get; set; }

    /// <summary>The Session Expiry Interval in force: the <c>CONNECT</c>'s, capped, or a <c>DISCONNECT</c>'s since.</summary>
    public TimeSpan Expiry { get; set; }

    /// <summary>Whether a connection holds the session.</summary>
    public bool IsConnected
    {
        get => isConnected;
        set => isConnected = value;
    }

    /// <summary>The timer that ends the session once its expiry passes with no connection holding it.</summary>
    public ITimer? ExpiryTimer { get; set; }

    /// <summary>
    /// Queues a message for the client without waiting; a bus delivery callback calls it.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="grantedQualityOfService">The QoS the subscription granted; the message goes at the lower of it and its own.</param>
    /// <param name="retain">The RETAIN flag the delivery carries.</param>
    /// <param name="subscriptionIdentifiers">The Subscription Identifiers the delivery carries.</param>
    public void Offer(BusMessage message, int grantedQualityOfService, bool retain, IReadOnlyList<uint> subscriptionIdentifiers)
    {
        var qualityOfService = Math.Min((int)message.QualityOfService, grantedQualityOfService);
        if (!isConnected && qualityOfService == 0)
        {
            return;
        }

        var payloadBytes = message.Payload.Length;
        if (Interlocked.Add(ref queuedPayloadBytes, payloadBytes) > maxQueuedPayloadBytes
            || !queued.Writer.TryWrite(new(message, qualityOfService, retain, subscriptionIdentifiers)))
        {
            Interlocked.Add(ref queuedPayloadBytes, -payloadBytes);
        }
    }

    /// <summary>
    /// Takes the next waiting message, waiting for one.
    /// </summary>
    /// <param name="cancellationToken">Ends the wait.</param>
    /// <returns>The message.</returns>
    public async ValueTask<Mqtt5Delivery> TakeAsync(CancellationToken cancellationToken)
    {
        var delivery = await queued.Reader.ReadAsync(cancellationToken);
        Interlocked.Add(ref queuedPayloadBytes, -delivery.Message.Payload.Length);
        return delivery;
    }

    /// <summary>
    /// Ends the session: its expiry timer and its subscriptions.
    /// </summary>
    public void Dispose()
    {
        ExpiryTimer?.Dispose();
        BusConnection.Dispose();
    }
}
