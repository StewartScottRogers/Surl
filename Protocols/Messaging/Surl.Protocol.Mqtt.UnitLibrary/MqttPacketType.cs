namespace Surl.Protocol.Mqtt;

/// <summary>
/// The MQTT 3.1.1 control packet types (section 2.2.1): the high four bits of a packet's
/// first byte.
/// </summary>
internal enum MqttPacketType
{
    /// <summary>A client asks to connect.</summary>
    Connect = 1,

    /// <summary>The server acknowledges a connect.</summary>
    ConnectAcknowledgement = 2,

    /// <summary>A message on a topic, in either direction.</summary>
    Publish = 3,

    /// <summary>Acknowledges a QoS 1 publish.</summary>
    PublishAcknowledgement = 4,

    /// <summary>Receipt of a QoS 2 publish, its first step.</summary>
    PublishReceived = 5,

    /// <summary>Release of a QoS 2 publish, its second step.</summary>
    PublishRelease = 6,

    /// <summary>Completion of a QoS 2 publish, its third step.</summary>
    PublishComplete = 7,

    /// <summary>A client subscribes to topic filters.</summary>
    Subscribe = 8,

    /// <summary>The server acknowledges a subscribe.</summary>
    SubscribeAcknowledgement = 9,

    /// <summary>A client unsubscribes from topic filters.</summary>
    Unsubscribe = 10,

    /// <summary>The server acknowledges an unsubscribe.</summary>
    UnsubscribeAcknowledgement = 11,

    /// <summary>A client's keep-alive ping.</summary>
    PingRequest = 12,

    /// <summary>The server's answer to a ping.</summary>
    PingResponse = 13,

    /// <summary>The end of the session.</summary>
    Disconnect = 14,
}
