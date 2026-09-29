namespace Surl.Protocol.Mqtt;

/// <summary>
/// How reading one MQTT packet ended.
/// </summary>
internal enum MqttPacketReadOutcome
{
    /// <summary>A whole packet was read.</summary>
    PacketRead,

    /// <summary>The client closed the connection between packets.</summary>
    ConnectionClosed,

    /// <summary>The client closed the connection part way through a packet.</summary>
    ConnectionClosedMidPacket,

    /// <summary>The remaining length's fourth byte still had its continuation bit set (MQTT 3.1.1, section 2.2.3).</summary>
    MalformedRemainingLength,

    /// <summary>The fixed header announced a packet longer than the packet limit.</summary>
    PacketTooLarge,

    /// <summary>A <c>PUBLISH</c> announced a payload longer than the payload limit.</summary>
    PublishPayloadTooLarge,

    /// <summary>The first packet was not complete within the head timeout.</summary>
    HeadTimedOut,
}
