namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// <c>DISCONNECT</c> or <c>AUTH</c> (sections 3.14 and 3.15), which share one layout: a Reason
/// Code and properties, both of which may be left off.
/// </summary>
/// <param name="PacketType"><see cref="Mqtt5PacketType.Disconnect"/> or <see cref="Mqtt5PacketType.Authentication"/>.</param>
/// <param name="ReasonCode">Its Reason Code; Success (Normal disconnection) when the packet leaves it off.</param>
/// <param name="Properties">Its properties.</param>
internal sealed record Mqtt5ReasonCodeAndPropertiesPacket(
    Mqtt5PacketType PacketType,
    Mqtt5ReasonCode ReasonCode,
    IReadOnlyList<Mqtt5Property> Properties) : Mqtt5Packet
{
    /// <inheritdoc />
    public override Mqtt5PacketType Type => PacketType;

    /// <summary>
    /// Reads a <c>DISCONNECT</c> or <c>AUTH</c> body.
    /// </summary>
    /// <param name="reader">The body.</param>
    /// <param name="packetType">Which of the two it is.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="Mqtt5PacketFaultException">The body breaks MQTT 5.0.</exception>
    public static Mqtt5ReasonCodeAndPropertiesPacket Read(Mqtt5BodyReader reader, Mqtt5PacketType packetType)
    {
        var (reasonCode, properties) = reader.ReadOptionalReasonCodeAndProperties(Mqtt5BodyReader.ContextOf(packetType));
        reader.RequireEnd("3.14.2");
        return new(packetType, reasonCode, properties);
    }

    /// <inheritdoc />
    public override void WriteBody(Mqtt5BodyWriter writer) =>
        writer.WriteOptionalReasonCodeAndProperties(ReasonCode, Properties);
}
