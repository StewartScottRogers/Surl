namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// Why a packet read from a client is refused: the reason code MQTT 5.0 section 4.13 (or the
/// section named) gives it, and the normative statement or section it breaks.
/// </summary>
/// <param name="ReasonCode">
/// The reason code the broker answers with: <see cref="Mqtt5ReasonCode.MalformedPacket"/>,
/// <see cref="Mqtt5ReasonCode.ProtocolError"/>, <see cref="Mqtt5ReasonCode.PacketTooLarge"/>
/// or <see cref="Mqtt5ReasonCode.UnsupportedProtocolVersion"/>.
/// </param>
/// <param name="Statement">The statement broken, e.g. <c>[MQTT-1.5.4-2]</c>, or its section.</param>
internal sealed record Mqtt5PacketFault(Mqtt5ReasonCode ReasonCode, string Statement)
{
    /// <summary>
    /// A <c>CONNECT</c> naming protocol <c>MQTT</c> or <c>MQIsdp</c> at a level other than 5
    /// (<c>[MQTT-3.1.2-2]</c>): the broker answers <c>CONNACK</c> <c>0x84</c>.
    /// </summary>
    public static Mqtt5PacketFault UnsupportedProtocolLevel { get; } =
        new(Mqtt5ReasonCode.UnsupportedProtocolVersion, "[MQTT-3.1.2-2]");

    /// <summary>
    /// A <c>CONNECT</c> naming a protocol that is not MQTT at all (<c>[MQTT-3.1.2-1]</c>):
    /// ADR-0093 decision 1 closes with no bytes, but a server may also answer <c>0x84</c>.
    /// </summary>
    public static Mqtt5PacketFault UnknownProtocolName { get; } =
        new(Mqtt5ReasonCode.UnsupportedProtocolVersion, "[MQTT-3.1.2-1]");

    /// <summary>
    /// A Malformed Packet (<c>0x81</c>).
    /// </summary>
    /// <param name="statement">The statement broken.</param>
    /// <returns>The fault.</returns>
    public static Mqtt5PacketFault Malformed(string statement) => new(Mqtt5ReasonCode.MalformedPacket, statement);

    /// <summary>
    /// A Protocol Error (<c>0x82</c>).
    /// </summary>
    /// <param name="statement">The statement broken.</param>
    /// <returns>The fault.</returns>
    public static Mqtt5PacketFault ProtocolError(string statement) => new(Mqtt5ReasonCode.ProtocolError, statement);
}
