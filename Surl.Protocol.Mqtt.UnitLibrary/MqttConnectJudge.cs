namespace Surl.Protocol.Mqtt;

/// <summary>
/// Decides how the MQTT server answers a <c>CONNECT</c> packet (MQTT 3.1.1, section 3.1), up to
/// the login the authentication policy judges (ADR-0032, decision 5).
/// </summary>
/// <remarks>
/// The protocol name and level are judged first, before any field whose layout depends on
/// the version, so a client of another MQTT version is always told
/// <see cref="MqttConnectVerdict.UnacceptableProtocolLevel"/>. Then the connect flags, and
/// every field they announce is read in order: the client identifier, the will topic and
/// will message (read and skipped: the server keeps no will), the user name and the
/// password (sections 3.1.3.1 to 3.1.3.5). A field cut short is
/// <see cref="MqttConnectVerdict.Malformed"/>; an empty client identifier without
/// <c>CleanSession</c> is <see cref="MqttConnectVerdict.IdentifierRejected"/>.
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

    private static readonly MqttConnectJudgement MalformedJudgement = new(MqttConnectVerdict.Malformed);

    /// <summary>
    /// Judges <paramref name="packet"/>, a <c>CONNECT</c>.
    /// </summary>
    /// <param name="packet">The <c>CONNECT</c> packet.</param>
    /// <returns>How the server answers it, and the user name and password it carried.</returns>
    public static MqttConnectJudgement Judge(MqttPacket packet)
    {
        var reader = new MqttBodyReader(packet.Body);
        if (!reader.TryReadString(out var protocolName) || !reader.TryReadByte(out var protocolLevel))
        {
            return MalformedJudgement;
        }

        return protocolName switch
        {
            "MQTT" when protocolLevel == SupportedProtocolLevel => JudgeLevel4Fields(reader),
            "MQTT" or "MQIsdp" => new MqttConnectJudgement(MqttConnectVerdict.UnacceptableProtocolLevel),
            _ => MalformedJudgement,
        };
    }

    private static MqttConnectJudgement JudgeLevel4Fields(MqttBodyReader reader)
    {
        return reader.TryReadByte(out var connectFlags) && AreValidConnectFlags(connectFlags)
            ? JudgePayload(reader, connectFlags)
            : MalformedJudgement;
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

    private static MqttConnectJudgement JudgePayload(MqttBodyReader reader, byte connectFlags)
    {
        if (!TryReadClientIdentifierAndSkipWill(reader, connectFlags, out var clientIdentifier)
            || !TryReadUserName(reader, connectFlags, out var userName)
            || !TryReadPassword(reader, connectFlags, out var password))
        {
            return MalformedJudgement;
        }

        return IsRejectedIdentifier(clientIdentifier, connectFlags)
            ? new MqttConnectJudgement(MqttConnectVerdict.IdentifierRejected)
            : new MqttConnectJudgement(MqttConnectVerdict.LoginToCheck, userName, password);
    }

    // The keep alive comes first; the server reads it and keeps no session to apply it to.
    private static bool TryReadClientIdentifierAndSkipWill(MqttBodyReader reader, byte connectFlags, out string clientIdentifier)
    {
        clientIdentifier = string.Empty;
        return reader.TryReadUInt16(out _) && reader.TryReadString(out clientIdentifier) && TrySkipWill(reader, connectFlags);
    }

    private static bool IsRejectedIdentifier(string clientIdentifier, byte connectFlags) =>
        clientIdentifier.Length == 0 && (connectFlags & CleanSessionFlag) == 0;

    private static bool TrySkipWill(MqttBodyReader reader, byte connectFlags) =>
        (connectFlags & WillFlag) == 0 || (reader.TryReadString(out _) && reader.TryReadBinary(out _));

    private static bool TryReadUserName(MqttBodyReader reader, byte connectFlags, out string? userName)
    {
        userName = null;
        if ((connectFlags & UserNameFlag) == 0)
        {
            return true;
        }

        var wasRead = reader.TryReadString(out var value);
        userName = value;
        return wasRead;
    }

    private static bool TryReadPassword(MqttBodyReader reader, byte connectFlags, out ReadOnlyMemory<byte>? password)
    {
        password = null;
        if ((connectFlags & PasswordFlag) == 0)
        {
            return true;
        }

        var wasRead = reader.TryReadBinary(out var value);
        password = value;
        return wasRead;
    }
}
