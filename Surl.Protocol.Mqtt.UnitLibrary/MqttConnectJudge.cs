namespace Surl.Protocol.Mqtt;

/// <summary>
/// Decides how the MQTT server answers a <c>CONNECT</c> packet (MQTT 3.1.1, section 3.1).
/// </summary>
/// <remarks>
/// The protocol name and level are judged first, before any field whose layout depends on
/// the version, so a client of another MQTT version is always told
/// <see cref="MqttConnectVerdict.UnacceptableProtocolLevel"/>. The will, user name and
/// password that may follow the client identifier are not read: the server takes every
/// client, and keeps no session and no will.
/// </remarks>
internal static class MqttConnectJudge
{
    private const byte SupportedProtocolLevel = 4;
    private const byte ReservedConnectFlag = 0x01;
    private const byte CleanSessionFlag = 0x02;
    private const byte WillFlag = 0x04;
    private const byte WillQualityOfServiceMask = 0x18;
    private const byte WillRetainFlag = 0x20;
    private const byte PasswordFlag = 0x40;
    private const byte UserNameFlag = 0x80;

    /// <summary>
    /// Judges <paramref name="packet"/>, a <c>CONNECT</c>.
    /// </summary>
    /// <param name="packet">The <c>CONNECT</c> packet.</param>
    /// <returns>How the server answers it.</returns>
    public static MqttConnectVerdict Judge(MqttPacket packet)
    {
        var reader = new MqttBodyReader(packet.Body);
        if (!reader.TryReadString(out var protocolName) || !reader.TryReadByte(out var protocolLevel))
        {
            return MqttConnectVerdict.Malformed;
        }

        return protocolName switch
        {
            "MQTT" when protocolLevel == SupportedProtocolLevel => JudgeLevel4Fields(reader),
            "MQTT" or "MQIsdp" => MqttConnectVerdict.UnacceptableProtocolLevel,
            _ => MqttConnectVerdict.Malformed,
        };
    }

    private static MqttConnectVerdict JudgeLevel4Fields(MqttBodyReader reader)
    {
        return reader.TryReadByte(out var connectFlags) && AreValidConnectFlags(connectFlags)
            ? JudgeClientIdentifier(reader, connectFlags)
            : MqttConnectVerdict.Malformed;
    }

    /// <summary>
    /// Section 3.1.2's rules for the connect flags: the reserved bit clear, will QoS and will
    /// retain clear unless the will flag is set, will QoS never 3, and a password only with a
    /// user name.
    /// </summary>
    private static bool AreValidConnectFlags(byte connectFlags)
    {
        var hasWill = (connectFlags & WillFlag) != 0;
        var willQualityOfService = (connectFlags & WillQualityOfServiceMask) >> 3;
        var willSettingsAreValid = hasWill ? willQualityOfService < 3 : (connectFlags & (WillQualityOfServiceMask | WillRetainFlag)) == 0;
        var passwordHasUserName = (connectFlags & PasswordFlag) == 0 || (connectFlags & UserNameFlag) != 0;

        return (connectFlags & ReservedConnectFlag) == 0 && willSettingsAreValid && passwordHasUserName;
    }

    private static MqttConnectVerdict JudgeClientIdentifier(MqttBodyReader reader, byte connectFlags)
    {
        if (!reader.TryReadUInt16(out _) || !reader.TryReadString(out var clientIdentifier))
        {
            return MqttConnectVerdict.Malformed;
        }

        return clientIdentifier.Length == 0 && (connectFlags & CleanSessionFlag) == 0
            ? MqttConnectVerdict.IdentifierRejected
            : MqttConnectVerdict.Accepted;
    }
}
