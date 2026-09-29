namespace Surl.Protocol.Mqtt;

/// <summary>
/// What the MQTT server makes of a <c>CONNECT</c> packet.
/// </summary>
internal enum MqttConnectVerdict
{
    /// <summary>MQTT 3.1.1 (protocol level 4), well formed: answered <c>CONNACK</c> 0.</summary>
    Accepted,

    /// <summary>
    /// Protocol name <c>MQTT</c> with a level other than 4, or <c>MQIsdp</c> (MQTT 3.1):
    /// answered <c>CONNACK</c> 1, then closed (MQTT 3.1.1, section 3.1.2.2).
    /// </summary>
    UnacceptableProtocolLevel,

    /// <summary>
    /// An empty client identifier without <c>CleanSession</c>: answered <c>CONNACK</c> 2, then
    /// closed (MQTT 3.1.1, section 3.1.3.1).
    /// </summary>
    IdentifierRejected,

    /// <summary>
    /// Any other protocol name, connect flags section 3.1.2 forbids (the reserved bit, will
    /// QoS 3, will QoS or will retain without the will flag, a password without a user
    /// name), or a field cut short: closed with no reply (MQTT 3.1.1, sections 3.1.2 and 4.8).
    /// </summary>
    Malformed,
}
