namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// <c>SUBSCRIBE</c> (section 3.8): a Packet Identifier, the SUBSCRIBE Properties, then one or
/// more Topic Filters, each with its Subscription Options.
/// </summary>
/// <param name="PacketIdentifier">The Packet Identifier.</param>
/// <param name="Properties">The SUBSCRIBE Properties (section 3.8.2.1).</param>
/// <param name="Subscriptions">The Topic Filters and their options, in order.</param>
internal sealed record Mqtt5SubscribePacket(
    ushort PacketIdentifier,
    IReadOnlyList<Mqtt5Property> Properties,
    IReadOnlyList<Mqtt5Subscription> Subscriptions) : Mqtt5Packet
{
    /// <inheritdoc />
    public override Mqtt5PacketType Type => Mqtt5PacketType.Subscribe;

    /// <summary>
    /// <c>0010</c> (<c>[MQTT-3.8.1-1]</c>).
    /// </summary>
    public override int FixedHeaderFlags => 0x02;

    /// <summary>
    /// Reads a <c>SUBSCRIBE</c> body. One with no Topic Filter is a Protocol Error
    /// (<c>[MQTT-3.8.3-2]</c>).
    /// </summary>
    /// <param name="reader">The body.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="Mqtt5PacketFaultException">The body breaks MQTT 5.0.</exception>
    public static Mqtt5SubscribePacket Read(Mqtt5BodyReader reader)
    {
        var packetIdentifier = reader.ReadPacketIdentifier();
        var properties = reader.ReadProperties(Mqtt5PropertyContext.Subscribe);
        if (reader.Remaining == 0)
        {
            throw Mqtt5BodyReader.Fault(Mqtt5PacketFault.ProtocolError("[MQTT-3.8.3-2]"));
        }

        var subscriptions = new List<Mqtt5Subscription>();
        while (reader.Remaining > 0)
        {
            subscriptions.Add(Mqtt5Subscription.Read(reader));
        }

        return new(packetIdentifier, properties, subscriptions);
    }

    /// <inheritdoc />
    public override void WriteBody(Mqtt5BodyWriter writer)
    {
        writer.WriteTwoByteInteger(PacketIdentifier);
        writer.WriteProperties(Properties);
        foreach (var subscription in Subscriptions)
        {
            writer.WriteUtf8String(subscription.TopicFilter);
            writer.WriteByte(subscription.Options);
        }
    }
}
