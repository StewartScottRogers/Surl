namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// One of the four packets that answer a <c>PUBLISH</c> along its QoS flow - <c>PUBACK</c>,
/// <c>PUBREC</c>, <c>PUBREL</c>, <c>PUBCOMP</c> (sections 3.4 to 3.7) - which share one
/// layout: a Packet Identifier, then a Reason Code and properties that may be left off.
/// </summary>
/// <param name="ResponseType">Which of the four it is.</param>
/// <param name="PacketIdentifier">The identifier of the <c>PUBLISH</c> it answers.</param>
/// <param name="ReasonCode">Its Reason Code; Success when the packet leaves it off.</param>
/// <param name="Properties">Its properties.</param>
internal sealed record Mqtt5PublishResponsePacket(
    Mqtt5PacketType ResponseType,
    ushort PacketIdentifier,
    Mqtt5ReasonCode ReasonCode,
    IReadOnlyList<Mqtt5Property> Properties) : Mqtt5Packet
{
    /// <inheritdoc />
    public override Mqtt5PacketType Type => ResponseType;

    /// <summary>
    /// <c>0010</c> for <c>PUBREL</c> (<c>[MQTT-3.6.1-1]</c>), 0 for the others.
    /// </summary>
    public override int FixedHeaderFlags => ResponseType == Mqtt5PacketType.PublishRelease ? 0x02 : 0;

    /// <summary>
    /// Reads the body of one of the four.
    /// </summary>
    /// <param name="reader">The body.</param>
    /// <param name="responseType">Which of the four it is.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="Mqtt5PacketFaultException">The body breaks MQTT 5.0.</exception>
    public static Mqtt5PublishResponsePacket Read(Mqtt5BodyReader reader, Mqtt5PacketType responseType)
    {
        var packetIdentifier = reader.ReadPacketIdentifier();
        var (reasonCode, properties) = reader.ReadOptionalReasonCodeAndProperties(Mqtt5BodyReader.ContextOf(responseType));
        reader.RequireEnd("3.4.2");
        return new(responseType, packetIdentifier, reasonCode, properties);
    }

    /// <inheritdoc />
    public override void WriteBody(Mqtt5BodyWriter writer)
    {
        writer.WriteTwoByteInteger(PacketIdentifier);
        writer.WriteOptionalReasonCodeAndProperties(ReasonCode, Properties);
    }
}
