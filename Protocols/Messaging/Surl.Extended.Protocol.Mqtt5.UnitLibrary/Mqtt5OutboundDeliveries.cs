using System.Threading.Channels;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// Sends one client the messages its subscriptions receive (ADR-0093 decision 4): as the sender of
/// sections 4.3.2 and 4.3.3, in the order they arrive, never more unacknowledged QoS 1 and 2
/// <c>PUBLISH</c>es than the client's Receive Maximum (<c>[MQTT-4.9.0-1]</c>), with Packet
/// Identifiers unique among those in flight (<c>[MQTT-2.2.1-4]</c>), Topic Aliases as
/// <see cref="Mqtt5OutboundTopicAliases"/> assigns them, and the Message Expiry Interval left.
/// A message that expires while it waits is dropped (<c>[MQTT-3.3.2-5]</c>), as is one on a topic
/// the client cannot name and one larger than the client's Maximum Packet Size
/// (<c>[MQTT-3.1.2-25]</c>).
/// </summary>
internal sealed class Mqtt5OutboundDeliveries
{
    /// <summary>
    /// The most messages that wait for one client at once; a message past it is dropped for that
    /// client, as ADR-0006 bounds every store a peer can fill.
    /// </summary>
    public const int MaxWaitingDeliveries = 1000;

    private readonly Channel<Mqtt5Delivery> waiting = Channel.CreateBounded<Mqtt5Delivery>(
        new BoundedChannelOptions(MaxWaitingDeliveries) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    private readonly Dictionary<ushort, Mqtt5PacketType> inFlight = [];
    private readonly Lock inFlightGate = new();
    private readonly SemaphoreSlim inFlightSlots;
    private readonly Mqtt5OutboundTopicAliases aliases;
    private readonly Func<byte[], CancellationToken, ValueTask> write;
    private readonly TopicNameLookup names;
    private readonly ExchangeContext context;
    private readonly long maxPacketBytes;


    /// <summary>
    /// Prepares to send to one client.
    /// </summary>
    /// <param name="client">The client: its Receive Maximum, Topic Alias Maximum and Maximum Packet Size.</param>
    /// <param name="names">The bus's name lookup, which maps a canonical topic to the client's Topic Name.</param>
    /// <param name="context">The exchange's context: its scheme, clock and log.</param>
    /// <param name="write">Writes a packet's bytes to the connection, one write at a time.</param>
    public Mqtt5OutboundDeliveries(Mqtt5ConnectedClient client, TopicNameLookup names, ExchangeContext context, Func<byte[], CancellationToken, ValueTask> write)
    {
        inFlightSlots = new SemaphoreSlim(client.ReceiveMaximum, client.ReceiveMaximum);
        aliases = new Mqtt5OutboundTopicAliases(client.TopicAliasMaximum);
        maxPacketBytes = client.MaximumPacketSize;
        this.names = names;
        this.context = context;
        this.write = write;
    }

    /// <summary>
    /// The Packet Identifier given last; the next is the first above it, after 65535 wrapping to 1, not in flight.
    /// </summary>
    internal ushort LastPacketIdentifier { get; set; }

    /// <summary>
    /// Queues <paramref name="message"/> for the client without waiting; a bus delivery callback calls it.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="grantedQualityOfService">The QoS the subscription granted.</param>
    /// <param name="retain">The RETAIN flag the delivery carries.</param>
    /// <param name="subscriptionIdentifiers">The Subscription Identifiers the delivery carries.</param>
    public void Offer(BusMessage message, int grantedQualityOfService, bool retain, IReadOnlyList<uint> subscriptionIdentifiers) =>
        waiting.Writer.TryWrite(new(message, Math.Min((int)message.QualityOfService, grantedQualityOfService), retain, subscriptionIdentifiers));

    /// <summary>
    /// Sends the queued messages, in order, until <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="cancellationToken">Ends the sending.</param>
    /// <returns>A task that completes when the sending has been cancelled.</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await SendAsync(await waiting.Reader.ReadAsync(cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// Takes the client's <c>PUBACK</c>, <c>PUBREC</c> or <c>PUBCOMP</c> for a <c>PUBLISH</c> the
    /// broker sent. One for no such <c>PUBLISH</c> is ignored, but a <c>PUBREC</c> is then answered
    /// <c>PUBREL</c> <c>0x92</c>; a <c>PUBREC</c> of <c>0x80</c> or above ends the message
    /// (<c>[MQTT-4.4.0-2]</c>), any other is answered <c>PUBREL</c>.
    /// </summary>
    /// <param name="acknowledgement">The client's packet.</param>
    /// <returns>The <c>PUBREL</c> to send, or <see langword="null"/>.</returns>
    public Mqtt5PublishResponsePacket? Acknowledge(Mqtt5PublishResponsePacket acknowledgement)
    {
        var identifier = acknowledgement.PacketIdentifier;
        lock (inFlightGate)
        {
            if (!inFlight.TryGetValue(identifier, out var awaited) || awaited != acknowledgement.ResponseType)
            {
                return acknowledgement.ResponseType == Mqtt5PacketType.PublishReceived
                    ? Release(identifier, Mqtt5ReasonCode.PacketIdentifierNotFound)
                    : null;
            }

            if (awaited == Mqtt5PacketType.PublishReceived && acknowledgement.ReasonCode < Mqtt5ReasonCode.UnspecifiedError)
            {
                inFlight[identifier] = Mqtt5PacketType.PublishComplete;
                return Release(identifier, Mqtt5ReasonCode.Success);
            }

            inFlight.Remove(identifier);
        }

        inFlightSlots.Release();
        return null;
    }

    private static Mqtt5PublishResponsePacket Release(ushort identifier, Mqtt5ReasonCode reasonCode) =>
        new(Mqtt5PacketType.PublishRelease, identifier, reasonCode, []);

    private async ValueTask SendAsync(Mqtt5Delivery delivery, CancellationToken cancellationToken)
    {
        if (delivery.QualityOfService > 0)
        {
            await inFlightSlots.WaitAsync(cancellationToken);
        }

        var topic = names.FromCanonicalTopic(context.Scheme, delivery.Message.Topic);
        var now = context.TimeProvider.GetUtcNow();
        if (!topic.IsMapped || delivery.Message.ExpiresAt <= now || !Fits(delivery, topic.Name, now))
        {
            ReleaseSlot(delivery.QualityOfService);
            return;
        }

        var (topicName, alias) = aliases.Assign(topic.Name);
        var properties = PropertiesOf(delivery, now);
        if (alias is not null)
        {
            properties.Add(alias);
        }

        await write(Mqtt5PacketEncoder.Encode(PublishOf(delivery, topicName, NextPacketIdentifier(delivery.QualityOfService), properties)), cancellationToken);
    }

    // [MQTT-3.1.2-25]: a PUBLISH larger than the client's Maximum Packet Size is dropped as if sent.
    private bool Fits(Mqtt5Delivery delivery, string topicName, DateTimeOffset now)
    {
        var size = Mqtt5PacketEncoder.Encode(PublishOf(delivery, topicName, 1, PropertiesOf(delivery, now))).Length;
        if (maxPacketBytes == 0 || size <= maxPacketBytes)
        {
            return true;
        }

        context.Log.Note($"A message on {topicName} is larger than the client's Maximum Packet Size; not sent.");
        return false;
    }

    // The message's own properties, then a Subscription Identifier for each subscription it is sent for.
    private static List<Mqtt5Property> PropertiesOf(Mqtt5Delivery delivery, DateTimeOffset now)
    {
        var properties = Mqtt5BusMessages.PropertiesOf(delivery.Message, now);
        properties.AddRange(delivery.SubscriptionIdentifiers.Select(identifier => Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.SubscriptionIdentifier, identifier)));
        return properties;
    }

    private void ReleaseSlot(int qualityOfService)
    {
        if (qualityOfService > 0)
        {
            inFlightSlots.Release();
        }
    }

    private ushort NextPacketIdentifier(int qualityOfService)
    {
        if (qualityOfService == 0)
        {
            return 0;
        }

        lock (inFlightGate)
        {
            do
            {
                LastPacketIdentifier = (ushort)((LastPacketIdentifier % ushort.MaxValue) + 1);
            }
            while (inFlight.ContainsKey(LastPacketIdentifier));

            inFlight[LastPacketIdentifier] = qualityOfService == 1 ? Mqtt5PacketType.PublishAcknowledgement : Mqtt5PacketType.PublishReceived;
            return LastPacketIdentifier;
        }
    }

    private static Mqtt5PublishPacket PublishOf(Mqtt5Delivery delivery, string topicName, ushort packetIdentifier, IReadOnlyList<Mqtt5Property> properties) =>
        new(false, delivery.QualityOfService, delivery.Retain, topicName, packetIdentifier, properties, delivery.Message.Payload.ToArray());
}
