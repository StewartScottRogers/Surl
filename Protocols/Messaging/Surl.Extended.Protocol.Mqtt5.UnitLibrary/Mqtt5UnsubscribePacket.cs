namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// <c>UNSUBSCRIBE</c> (section 3.10): a Packet Identifier, the UNSUBSCRIBE Properties, then one
/// or more Topic Filters.
/// </summary>
/// <param name="PacketIdentifier">The Packet Identifier.</param>
/// <param name="Properties">The UNSUBSCRIBE Properties (section 3.10.2.1).</param>
/// <param name="TopicFilters">The Topic Filters, in order.</param>
internal sealed record Mqtt5UnsubscribePacket(
    ushort PacketIdentifier,
    IReadOnlyList<Mqtt5Property> Properties,
    IReadOnlyList<string> TopicFilters) : Mqtt5Packet
{
    /// <inheritdoc />
    public override Mqtt5PacketType Type => Mqtt5PacketType.Unsubscribe;

    /// <summary>
    /// <c>0010</c> (<c>[MQTT-3.10.1-1]</c>).
    /// </summary>
    public override int FixedHeaderFlags => 0x02;

    /// <summary>
    /// Reads an <c>UNSUBSCRIBE</c> body. One with no Topic Filter is a Protocol Error
    /// (<c>[MQTT-3.10.3-2]</c>).
    /// </summary>
    /// <param name="reader">The body.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="Mqtt5PacketFaultException">The body breaks MQTT 5.0.</exception>
    public static Mqtt5UnsubscribePacket Read(Mqtt5BodyReader reader)
    {
        var packetIdentifier = reader.ReadPacketIdentifier();
        var properties = reader.ReadProperties(Mqtt5PropertyContext.Unsubscribe);
        if (reader.Remaining == 0)
        {
            throw Mqtt5BodyReader.Fault(Mqtt5PacketFault.ProtocolError("[MQTT-3.10.3-2]"));
        }

        var topicFilters = new List<string>();
        while (reader.Remaining > 0)
        {
            topicFilters.Add(reader.ReadUtf8String());
        }

        return new(packetIdentifier, properties, topicFilters);
    }

    /// <inheritdoc />
    public override void WriteBody(Mqtt5BodyWriter writer)
    {
        writer.WriteTwoByteInteger(PacketIdentifier);
        writer.WriteProperties(Properties);
        foreach (var topicFilter in TopicFilters)
        {
            writer.WriteUtf8String(topicFilter);
        }
    }
}
