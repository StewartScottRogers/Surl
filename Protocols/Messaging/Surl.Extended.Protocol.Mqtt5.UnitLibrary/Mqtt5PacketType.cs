namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The MQTT 5.0 control packet types (section 2.1.2): the high four bits of a packet's first
/// byte. Value 0 is reserved and is never a packet.
/// </summary>
internal enum Mqtt5PacketType
{
    /// <summary><c>CONNECT</c>: a client asks to connect (section 3.1).</summary>
    Connect = 1,

    /// <summary><c>CONNACK</c>: the server answers a connect (section 3.2).</summary>
    ConnectAcknowledgement = 2,

    /// <summary><c>PUBLISH</c>: a message on a topic, in either direction (section 3.3).</summary>
    Publish = 3,

    /// <summary><c>PUBACK</c>: acknowledges a QoS 1 publish (section 3.4).</summary>
    PublishAcknowledgement = 4,

    /// <summary><c>PUBREC</c>: receipt of a QoS 2 publish, its first step (section 3.5).</summary>
    PublishReceived = 5,

    /// <summary><c>PUBREL</c>: release of a QoS 2 publish, its second step (section 3.6).</summary>
    PublishRelease = 6,

    /// <summary><c>PUBCOMP</c>: completion of a QoS 2 publish, its third step (section 3.7).</summary>
    PublishComplete = 7,

    /// <summary><c>SUBSCRIBE</c>: a client subscribes to topic filters (section 3.8).</summary>
    Subscribe = 8,

    /// <summary><c>SUBACK</c>: the server answers a subscribe (section 3.9).</summary>
    SubscribeAcknowledgement = 9,

    /// <summary><c>UNSUBSCRIBE</c>: a client unsubscribes from topic filters (section 3.10).</summary>
    Unsubscribe = 10,

    /// <summary><c>UNSUBACK</c>: the server answers an unsubscribe (section 3.11).</summary>
    UnsubscribeAcknowledgement = 11,

    /// <summary><c>PINGREQ</c>: a client's keep-alive ping (section 3.12).</summary>
    PingRequest = 12,

    /// <summary><c>PINGRESP</c>: the server's answer to a ping (section 3.13).</summary>
    PingResponse = 13,

    /// <summary><c>DISCONNECT</c>: the end of the connection, from either side (section 3.14).</summary>
    Disconnect = 14,

    /// <summary><c>AUTH</c>: an enhanced authentication exchange (section 3.15).</summary>
    Authentication = 15,
}
