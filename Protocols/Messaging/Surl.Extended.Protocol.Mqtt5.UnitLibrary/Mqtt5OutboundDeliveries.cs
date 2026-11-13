using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// Sends one connection's client the messages its session holds (ADR-0093 decisions 3 and 4): as
/// the sender of sections 4.3.2 and 4.3.3, first the <c>PUBLISH</c>es a resumed session still has
/// in flight, again with DUP 1, and its <c>PUBREL</c>s, in their original order
/// (<c>[MQTT-4.4.0-1]</c>); then the waiting messages in the order they arrived, never more
/// unacknowledged QoS 1 and 2 <c>PUBLISH</c>es than the client's Receive Maximum
/// (<c>[MQTT-4.9.0-1]</c>), with Packet Identifiers unique among those in flight
/// (<c>[MQTT-2.2.1-4]</c>), Topic Aliases as <see cref="Mqtt5OutboundTopicAliases"/> assigns them,
/// and the Message Expiry Interval left. A message that expired while it waited is dropped
/// (<c>[MQTT-3.3.2-5]</c>), as is one on a topic the client cannot name and one larger than the
/// client's Maximum Packet Size (<c>[MQTT-3.1.2-25]</c>).
/// </summary>
internal sealed class Mqtt5OutboundDeliveries
{
    private readonly Mqtt5Session session;
    private readonly SemaphoreSlim inFlightSlots;
    private readonly Mqtt5OutboundTopicAliases aliases;
    private readonly Func<byte[], CancellationToken, ValueTask> write;
    private readonly TopicNameLookup names;
    private readonly ExchangeContext context;
    private readonly long maxPacketBytes;

    // Slots a resumed session's in-flight PUBLISHes hold past the new Receive Maximum: the
    // acknowledgements that free them give no slot back.
    private int slotsOwed;

    /// <summary>
    /// Prepares to send to one connection's client.
    /// </summary>
    /// <param name="session">The client's session: what waits for it and what is in flight.</param>
    /// <param name="client">The client: its Receive Maximum, Topic Alias Maximum and Maximum Packet Size.</param>
    /// <param name="names">The bus's name lookup, which maps a canonical topic to the client's Topic Name.</param>
    /// <param name="context">The exchange's context: its scheme, clock and log.</param>
    /// <param name="write">Writes a packet's bytes to the connection, one write at a time.</param>
    public Mqtt5OutboundDeliveries(Mqtt5Session session, Mqtt5ConnectedClient client, TopicNameLookup names, ExchangeContext context, Func<byte[], CancellationToken, ValueTask> write)
    {
        int carried;
        lock (session.InFlightGate)
        {
            carried = session.InFlight.Count;
        }

        inFlightSlots = new SemaphoreSlim(Math.Max(0, client.ReceiveMaximum - carried), int.MaxValue);
        slotsOwed = Math.Max(0, carried - client.ReceiveMaximum);
        aliases = new Mqtt5OutboundTopicAliases(client.TopicAliasMaximum);
        maxPacketBytes = client.MaximumPacketSize;
        this.session = session;
        this.names = names;
        this.context = context;
        this.write = write;
    }

    /// <summary>
    /// Resends what the session has in flight, then sends its waiting messages, in order, until
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="cancellationToken">Ends the sending.</param>
    /// <returns>A task that completes when the sending has been cancelled.</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ResendInFlightAsync(cancellationToken);
            while (true)
            {
                await SendAsync(await session.TakeAsync(cancellationToken), cancellationToken);
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
        lock (session.InFlightGate)
        {
            if (!session.InFlight.TryGetValue(identifier, out var publish) || publish.Awaited != acknowledgement.ResponseType)
            {
                return acknowledgement.ResponseType == Mqtt5PacketType.PublishReceived
                    ? Release(identifier, Mqtt5ReasonCode.PacketIdentifierNotFound)
                    : null;
            }

            if (publish.Awaited == Mqtt5PacketType.PublishReceived && acknowledgement.ReasonCode < Mqtt5ReasonCode.UnspecifiedError)
            {
                session.InFlight[identifier] = publish with { Awaited = Mqtt5PacketType.PublishComplete };
                return Release(identifier, Mqtt5ReasonCode.Success);
            }
        }

        Forget(identifier);
        return null;
    }

    private static Mqtt5PublishResponsePacket Release(ushort identifier, Mqtt5ReasonCode reasonCode) =>
        new(Mqtt5PacketType.PublishRelease, identifier, reasonCode, []);

    private async ValueTask ResendInFlightAsync(CancellationToken cancellationToken)
    {
        List<KeyValuePair<ushort, Mqtt5InFlightPublish>> carried;
        lock (session.InFlightGate)
        {
            carried = [.. session.InFlight.OrderBy(entry => entry.Value.Sequence)];
        }

        foreach (var (identifier, publish) in carried)
        {
            if (publish.Awaited == Mqtt5PacketType.PublishComplete)
            {
                await write(Mqtt5PacketEncoder.Encode(Release(identifier, Mqtt5ReasonCode.Success)), cancellationToken);
            }
            else
            {
                await ResendAsync(publish.Delivery, identifier, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Sends one delivery now, as the sending loop does: waits for an in-flight slot when its QoS is above 0, then writes it.
    /// Only the one-shot read calls it, after the loop has stopped, so nothing else sends meanwhile.
    /// </summary>
    /// <param name="delivery">The delivery.</param>
    /// <param name="cancellationToken">Ends the wait and the write.</param>
    /// <returns>A task that completes when it is written or dropped.</returns>
    public async ValueTask SendAsync(Mqtt5Delivery delivery, CancellationToken cancellationToken)
    {
        if (delivery.QualityOfService > 0)
        {
            await inFlightSlots.WaitAsync(cancellationToken);
        }

        var now = context.TimeProvider.GetUtcNow();
        if (TopicNameToSend(delivery, now) is not { } topicName)
        {
            FreeSlot(delivery.QualityOfService);
            return;
        }

        await WriteAsync(delivery, topicName, NextPacketIdentifier(delivery), duplicate: false, now, cancellationToken);
    }

    private async ValueTask ResendAsync(Mqtt5Delivery delivery, ushort packetIdentifier, CancellationToken cancellationToken)
    {
        var now = context.TimeProvider.GetUtcNow();
        if (TopicNameToSend(delivery, now) is not { } topicName)
        {
            Forget(packetIdentifier);
            return;
        }

        await WriteAsync(delivery, topicName, packetIdentifier, duplicate: true, now, cancellationToken);
    }

    // The client's Topic Name for the message, or null to drop it: on a topic the client cannot
    // name, expired, or too large for the client.
    private string? TopicNameToSend(Mqtt5Delivery delivery, DateTimeOffset now)
    {
        var topic = names.FromCanonicalTopic(context.Scheme, delivery.Message.Topic);
        return topic.IsMapped && !(delivery.Message.ExpiresAt <= now) && Fits(delivery, topic.Name, now) ? topic.Name : null;
    }

    private async ValueTask WriteAsync(Mqtt5Delivery delivery, string topicName, ushort packetIdentifier, bool duplicate, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (nameOrEmpty, alias) = aliases.Assign(topicName);
        var properties = PropertiesOf(delivery, now);
        if (alias is not null)
        {
            properties.Add(alias);
        }

        await write(Mqtt5PacketEncoder.Encode(PublishOf(delivery, duplicate, nameOrEmpty, packetIdentifier, properties)), cancellationToken);
    }

    // [MQTT-3.1.2-25]: a PUBLISH larger than the client's Maximum Packet Size is dropped as if sent.
    private bool Fits(Mqtt5Delivery delivery, string topicName, DateTimeOffset now)
    {
        var size = Mqtt5PacketEncoder.Encode(PublishOf(delivery, false, topicName, 1, PropertiesOf(delivery, now))).Length;
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

    // Ends a PUBLISH in flight and frees its slot.
    private void Forget(ushort packetIdentifier)
    {
        lock (session.InFlightGate)
        {
            session.InFlight.Remove(packetIdentifier);
        }

        FreeSlot(1);
    }

    // A QoS 0 PUBLISH holds no slot; a slot owed past the Receive Maximum is not given back.
    private void FreeSlot(int qualityOfService)
    {
        if (qualityOfService == 0)
        {
            return;
        }

        bool owed;
        lock (session.InFlightGate)
        {
            owed = slotsOwed > 0;
            slotsOwed -= owed ? 1 : 0;
        }

        if (!owed)
        {
            inFlightSlots.Release();
        }
    }

    private ushort NextPacketIdentifier(Mqtt5Delivery delivery)
    {
        if (delivery.QualityOfService == 0)
        {
            return 0;
        }

        lock (session.InFlightGate)
        {
            do
            {
                session.LastPacketIdentifier = (ushort)((session.LastPacketIdentifier % ushort.MaxValue) + 1);
            }
            while (session.InFlight.ContainsKey(session.LastPacketIdentifier));

            var awaited = delivery.QualityOfService == 1 ? Mqtt5PacketType.PublishAcknowledgement : Mqtt5PacketType.PublishReceived;
            session.InFlight[session.LastPacketIdentifier] = new(++session.LastInFlightSequence, awaited, delivery);
            return session.LastPacketIdentifier;
        }
    }

    private static Mqtt5PublishPacket PublishOf(Mqtt5Delivery delivery, bool duplicate, string topicName, ushort packetIdentifier, IReadOnlyList<Mqtt5Property> properties) =>
        new(duplicate, delivery.QualityOfService, delivery.Retain, topicName, packetIdentifier, properties, delivery.Message.Payload.ToArray());
}
