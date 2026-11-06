namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// <c>PUBLISH</c> (section 3.3): DUP, QoS and RETAIN in the fixed header, then the Topic Name,
/// the Packet Identifier above QoS 0, the PUBLISH Properties and the payload.
/// </summary>
/// <param name="Duplicate">The DUP flag (section 3.3.1.1).</param>
/// <param name="QualityOfService">The QoS, 0 to 2 (section 3.3.1.2).</param>
/// <param name="Retain">The RETAIN flag (section 3.3.1.3).</param>
/// <param name="TopicName">The Topic Name; empty when a Topic Alias stands for it (section 3.3.2.1).</param>
/// <param name="PacketIdentifier">The Packet Identifier; 0 at QoS 0, where there is none.</param>
/// <param name="Properties">The PUBLISH Properties (section 3.3.2.3).</param>
/// <param name="Payload">The Application Message (section 3.3.3).</param>
internal sealed record Mqtt5PublishPacket(
    bool Duplicate,
    int QualityOfService,
    bool Retain,
    string TopicName,
    ushort PacketIdentifier,
    IReadOnlyList<Mqtt5Property> Properties,
    byte[] Payload) : Mqtt5Packet
{
    private const int DuplicateFlag = 0x08;
    private const int RetainFlag = 0x01;

    /// <inheritdoc />
    public override Mqtt5PacketType Type => Mqtt5PacketType.Publish;

    /// <inheritdoc />
    public override int FixedHeaderFlags =>
        (Duplicate ? DuplicateFlag : 0) | (QualityOfService << 1) | (Retain ? RetainFlag : 0);

    /// <summary>
    /// The QoS a <c>PUBLISH</c>'s fixed header flags carry.
    /// </summary>
    /// <param name="flags">The low four bits of its first byte.</param>
    /// <returns>Bits 2 and 1, 0 to 3.</returns>
    public static int QualityOfServiceOf(int flags) => (flags >> 1) & 0x03;

    /// <summary>
    /// Reads a <c>PUBLISH</c> body. Both QoS bits set (<c>[MQTT-3.3.1-4]</c>) and a wildcard in
    /// the Topic Name (<c>[MQTT-3.3.2-2]</c>) are Malformed Packets; DUP at QoS 0
    /// (<c>[MQTT-3.3.1-2]</c>) is a Protocol Error.
    /// </summary>
    /// <param name="reader">The body.</param>
    /// <param name="flags">The fixed header's flags.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="Mqtt5PacketFaultException">The body breaks MQTT 5.0.</exception>
    public static Mqtt5PublishPacket Read(Mqtt5BodyReader reader, int flags)
    {
        var qualityOfService = QualityOfServiceOf(flags);
        var duplicate = (flags & DuplicateFlag) != 0;
        RequireValidFlags(qualityOfService, duplicate);
        var topicName = reader.ReadUtf8String();
        if (topicName.AsSpan().IndexOfAny('+', '#') >= 0)
        {
            throw Mqtt5BodyReader.Fault(Mqtt5PacketFault.Malformed("[MQTT-3.3.2-2]"));
        }

        var packetIdentifier = qualityOfService > 0 ? reader.ReadPacketIdentifier() : (ushort)0;
        var properties = reader.ReadProperties(Mqtt5PropertyContext.Publish);
        return new(duplicate, qualityOfService, (flags & RetainFlag) != 0, topicName, packetIdentifier, properties, reader.ReadRemaining());
    }

    /// <inheritdoc />
    public override void WriteBody(Mqtt5BodyWriter writer)
    {
        writer.WriteUtf8String(TopicName);
        if (QualityOfService > 0)
        {
            writer.WriteTwoByteInteger(PacketIdentifier);
        }

        writer.WriteProperties(Properties);
        writer.WriteBytes(Payload);
    }

    private static void RequireValidFlags(int qualityOfService, bool duplicate)
    {
        if (qualityOfService == 3)
        {
            throw Mqtt5BodyReader.Fault(Mqtt5PacketFault.Malformed("[MQTT-3.3.1-4]"));
        }

        if (duplicate && qualityOfService == 0)
        {
            throw Mqtt5BodyReader.Fault(Mqtt5PacketFault.ProtocolError("[MQTT-3.3.1-2]"));
        }
    }
}
