namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// <c>SUBACK</c> or <c>UNSUBACK</c> (sections 3.9 and 3.11), which share one layout: a Packet
/// Identifier, properties, then one Reason Code per Topic Filter of the packet answered.
/// </summary>
/// <param name="AcknowledgementType">
/// <see cref="Mqtt5PacketType.SubscribeAcknowledgement"/> or
/// <see cref="Mqtt5PacketType.UnsubscribeAcknowledgement"/>.
/// </param>
/// <param name="PacketIdentifier">The identifier of the packet answered.</param>
/// <param name="Properties">Its properties.</param>
/// <param name="ReasonCodes">One Reason Code per Topic Filter, in order.</param>
internal sealed record Mqtt5ReasonCodeListPacket(
    Mqtt5PacketType AcknowledgementType,
    ushort PacketIdentifier,
    IReadOnlyList<Mqtt5Property> Properties,
    IReadOnlyList<Mqtt5ReasonCode> ReasonCodes) : Mqtt5Packet
{
    /// <inheritdoc />
    public override Mqtt5PacketType Type => AcknowledgementType;

    /// <summary>
    /// Reads a <c>SUBACK</c> or <c>UNSUBACK</c> body.
    /// </summary>
    /// <param name="reader">The body.</param>
    /// <param name="acknowledgementType">Which of the two it is.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="Mqtt5PacketFaultException">The body breaks MQTT 5.0.</exception>
    public static Mqtt5ReasonCodeListPacket Read(Mqtt5BodyReader reader, Mqtt5PacketType acknowledgementType)
    {
        var packetIdentifier = reader.ReadPacketIdentifier();
        var properties = reader.ReadProperties(Mqtt5BodyReader.ContextOf(acknowledgementType));
        var reasonCodes = reader.ReadRemaining().Select(reasonCode => (Mqtt5ReasonCode)reasonCode).ToList();
        return new(acknowledgementType, packetIdentifier, properties, reasonCodes);
    }

    /// <inheritdoc />
    public override void WriteBody(Mqtt5BodyWriter writer)
    {
        writer.WriteTwoByteInteger(PacketIdentifier);
        writer.WriteProperties(Properties);
        foreach (var reasonCode in ReasonCodes)
        {
            writer.WriteByte((byte)reasonCode);
        }
    }
}
