namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// Decodes one MQTT 5.0 control packet from its first byte and body, or names what MQTT 5.0
/// section 4.13 makes of it.
/// </summary>
/// <remarks>
/// The decoder is structural: it refuses what the data representations (section 1.5), the
/// fixed header (section 2.1), table 2-4 and each packet's layout in chapter 3 forbid. Which
/// packets a client may send, and the meaning of each property's value, are the broker's
/// to judge (ADR-0093 decisions 1 to 6).
/// </remarks>
internal static class Mqtt5PacketDecoder
{
    private const int PacketTypeCount = 16;

    // Indexed by packet type; type 0 is reserved and has none.
    private static readonly Func<Mqtt5BodyReader, int, Mqtt5Packet>?[] BodyReaders = new Func<Mqtt5BodyReader, int, Mqtt5Packet>?[PacketTypeCount]
    {
        null,
        (reader, _) => Mqtt5ConnectPacket.Read(reader),
        (reader, _) => Mqtt5ConnectAcknowledgementPacket.Read(reader),
        Mqtt5PublishPacket.Read,
        (reader, _) => Mqtt5PublishResponsePacket.Read(reader, Mqtt5PacketType.PublishAcknowledgement),
        (reader, _) => Mqtt5PublishResponsePacket.Read(reader, Mqtt5PacketType.PublishReceived),
        (reader, _) => Mqtt5PublishResponsePacket.Read(reader, Mqtt5PacketType.PublishRelease),
        (reader, _) => Mqtt5PublishResponsePacket.Read(reader, Mqtt5PacketType.PublishComplete),
        (reader, _) => Mqtt5SubscribePacket.Read(reader),
        (reader, _) => Mqtt5ReasonCodeListPacket.Read(reader, Mqtt5PacketType.SubscribeAcknowledgement),
        (reader, _) => Mqtt5UnsubscribePacket.Read(reader),
        (reader, _) => Mqtt5ReasonCodeListPacket.Read(reader, Mqtt5PacketType.UnsubscribeAcknowledgement),
        (reader, _) => Mqtt5PingPacket.Read(reader, Mqtt5PacketType.PingRequest),
        (reader, _) => Mqtt5PingPacket.Read(reader, Mqtt5PacketType.PingResponse),
        (reader, _) => Mqtt5ReasonCodeAndPropertiesPacket.Read(reader, Mqtt5PacketType.Disconnect),
        (reader, _) => Mqtt5ReasonCodeAndPropertiesPacket.Read(reader, Mqtt5PacketType.Authentication),
    };

    /// <summary>
    /// Decodes one packet.
    /// </summary>
    /// <param name="firstByte">The fixed header's first byte.</param>
    /// <param name="body">The bytes its Remaining Length covers.</param>
    /// <returns>The packet, or the fault that refuses it.</returns>
    public static Mqtt5PacketDecodeResult Decode(byte firstByte, ReadOnlyMemory<byte> body)
    {
        var type = firstByte >> 4;
        var flags = firstByte & 0x0F;
        var bodyReader = BodyReaders[type];
        if (bodyReader is null)
        {
            return Mqtt5PacketDecodeResult.Refused(Mqtt5PacketFault.Malformed("2.1.2"));
        }

        if (!HasValidFlags((Mqtt5PacketType)type, flags))
        {
            return Mqtt5PacketDecodeResult.Refused(Mqtt5PacketFault.Malformed("[MQTT-2.1.3-1]"));
        }

        try
        {
            return Mqtt5PacketDecodeResult.Decoded(bodyReader(new Mqtt5BodyReader(body), flags));
        }
        catch (Mqtt5PacketFaultException exception)
        {
            return Mqtt5PacketDecodeResult.Refused(exception.Fault);
        }
    }

    /// <summary>
    /// Whether the fixed header's flags are those table 2-2 fixes for the type; a
    /// <c>PUBLISH</c>'s carry DUP, QoS and RETAIN and are checked when it is read.
    /// </summary>
    /// <param name="type">The packet type.</param>
    /// <param name="flags">The low four bits of the first byte.</param>
    /// <returns><see langword="true"/> when the flags are allowed.</returns>
    public static bool HasValidFlags(Mqtt5PacketType type, int flags) => type switch
    {
        Mqtt5PacketType.Publish => true,
        Mqtt5PacketType.PublishRelease or Mqtt5PacketType.Subscribe or Mqtt5PacketType.Unsubscribe => flags == 0x02,
        _ => flags == 0,
    };
}
