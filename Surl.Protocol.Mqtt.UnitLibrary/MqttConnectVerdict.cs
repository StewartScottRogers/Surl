namespace Surl.Protocol.Mqtt;

/// <summary>
/// What the MQTT server makes of a <c>CONNECT</c> packet.
/// </summary>
internal enum MqttConnectVerdict
{
    /// <summary>
    /// MQTT 3.1.1 (protocol level 4), well formed: its user name and password go to the
    /// authentication policy, whose verdict is one of <see cref="Accepted"/>,
    /// <see cref="BadUserNameOrPassword"/> and <see cref="NotAuthorized"/> (ADR-0032, decision 5).
    /// </summary>
    LoginToCheck,

    /// <summary>The login was accepted: answered <c>CONNACK</c> 0.</summary>
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
    /// A user name that matches no account, or a password that does not match it: answered
    /// <c>CONNACK</c> 4, then closed (MQTT 3.1.1, section 3.2.2.3; ADR-0032, decision 5).
    /// </summary>
    BadUserNameOrPassword,

    /// <summary>
    /// No user name, or a password over an unencrypted connection, with no password checked:
    /// answered <c>CONNACK</c> 5, then closed (MQTT 3.1.1, section 3.2.2.3; ADR-0032, decision 5).
    /// </summary>
    NotAuthorized,

    /// <summary>
    /// Any other protocol name, connect flags section 3.1.2 forbids (the reserved bit, will
    /// QoS 3, will QoS or will retain without the will flag, a password without a user
    /// name), or a field cut short - the will topic, will message, user name and password the
    /// flags announce included: closed with no reply (MQTT 3.1.1, sections 3.1.2 and 4.8).
    /// </summary>
    Malformed,
}
