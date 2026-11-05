namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// <c>CONNACK</c> (section 3.2): the Connect Acknowledge Flags, the Connect Reason Code and
/// the CONNACK Properties.
/// </summary>
/// <param name="SessionPresent">The Session Present flag (section 3.2.2.1.1).</param>
/// <param name="ReasonCode">The Connect Reason Code (section 3.2.2.2).</param>
/// <param name="Properties">The CONNACK Properties (section 3.2.2.3).</param>
internal sealed record Mqtt5ConnectAcknowledgementPacket(
    bool SessionPresent,
    Mqtt5ReasonCode ReasonCode,
    IReadOnlyList<Mqtt5Property> Properties) : Mqtt5Packet
{
    /// <inheritdoc />
    public override Mqtt5PacketType Type => Mqtt5PacketType.ConnectAcknowledgement;

    /// <summary>
    /// Reads a <c>CONNACK</c> body. Bits 7 to 1 of its flags are reserved (<c>[MQTT-3.2.2-1]</c>).
    /// </summary>
    /// <param name="reader">The body.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="Mqtt5PacketFaultException">The body breaks MQTT 5.0.</exception>
    public static Mqtt5ConnectAcknowledgementPacket Read(Mqtt5BodyReader reader)
    {
        var flags = reader.ReadByte();
        if ((flags & 0xFE) != 0)
        {
            throw Mqtt5BodyReader.Fault(Mqtt5PacketFault.Malformed("[MQTT-3.2.2-1]"));
        }

        var reasonCode = (Mqtt5ReasonCode)reader.ReadByte();
        var properties = reader.ReadProperties(Mqtt5PropertyContext.ConnectAcknowledgement);
        reader.RequireEnd("3.2.3");
        return new(flags == 1, reasonCode, properties);
    }

    /// <inheritdoc />
    public override void WriteBody(Mqtt5BodyWriter writer)
    {
        writer.WriteByte(SessionPresent ? (byte)1 : (byte)0);
        writer.WriteByte((byte)ReasonCode);
        writer.WriteProperties(Properties);
    }
}
