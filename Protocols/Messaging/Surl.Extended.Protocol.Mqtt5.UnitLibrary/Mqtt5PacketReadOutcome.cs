namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// How reading one MQTT 5.0 packet from a connection ended.
/// </summary>
internal enum Mqtt5PacketReadOutcome
{
    /// <summary>A whole packet was read and decoded.</summary>
    PacketRead,

    /// <summary>The client closed the connection between packets.</summary>
    ConnectionClosed,

    /// <summary>The client closed the connection part way through a packet.</summary>
    ConnectionClosedMidPacket,

    /// <summary>The packet is refused; the result's fault says why and with which reason code.</summary>
    PacketRefused,
}
