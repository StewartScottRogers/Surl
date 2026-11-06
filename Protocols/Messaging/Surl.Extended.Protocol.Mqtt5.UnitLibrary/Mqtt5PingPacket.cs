namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// <c>PINGREQ</c> or <c>PINGRESP</c> (sections 3.12 and 3.13): a fixed header and nothing else.
/// </summary>
/// <param name="PingType"><see cref="Mqtt5PacketType.PingRequest"/> or <see cref="Mqtt5PacketType.PingResponse"/>.</param>
internal sealed record Mqtt5PingPacket(Mqtt5PacketType PingType) : Mqtt5Packet
{
    /// <inheritdoc />
    public override Mqtt5PacketType Type => PingType;

    /// <summary>
    /// Reads a ping's body, which must be empty (sections 3.12.2 and 3.13.2).
    /// </summary>
    /// <param name="reader">The body.</param>
    /// <param name="pingType">Which of the two it is.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="Mqtt5PacketFaultException">The body is not empty.</exception>
    public static Mqtt5PingPacket Read(Mqtt5BodyReader reader, Mqtt5PacketType pingType)
    {
        reader.RequireEnd("3.12.2");
        return new(pingType);
    }

    /// <inheritdoc />
    public override void WriteBody(Mqtt5BodyWriter writer)
    {
    }
}
