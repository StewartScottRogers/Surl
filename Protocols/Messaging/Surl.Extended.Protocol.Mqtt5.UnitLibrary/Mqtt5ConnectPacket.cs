namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// <c>CONNECT</c> (section 3.1): protocol name <c>MQTT</c>, level 5, the Connect Flags, Keep
/// Alive and properties, then the payload the flags announce.
/// </summary>
/// <param name="CleanStart">The Clean Start flag (section 3.1.2.4).</param>
/// <param name="KeepAlive">The Keep Alive, in seconds (section 3.1.2.10).</param>
/// <param name="Properties">The CONNECT Properties (section 3.1.2.11).</param>
/// <param name="ClientIdentifier">The Client Identifier, possibly empty (section 3.1.3.1).</param>
/// <param name="Will">The Will Message; <see langword="null"/> when the Will Flag is clear.</param>
/// <param name="UserName">The User Name; <see langword="null"/> when its flag is clear.</param>
/// <param name="Password">The Password; <see langword="null"/> when its flag is clear.</param>
internal sealed record Mqtt5ConnectPacket(
    bool CleanStart,
    ushort KeepAlive,
    IReadOnlyList<Mqtt5Property> Properties,
    string ClientIdentifier,
    Mqtt5Will? Will,
    string? UserName,
    byte[]? Password) : Mqtt5Packet
{
    /// <summary>
    /// The protocol name MQTT 5.0 requires (<c>[MQTT-3.1.2-1]</c>).
    /// </summary>
    public const string ProtocolName = "MQTT";

    /// <summary>
    /// The protocol level of MQTT 5.0 (section 3.1.2.2).
    /// </summary>
    public const byte ProtocolLevel = 5;

    private const string ProtocolNameOfMqtt31 = "MQIsdp";
    private const int ReservedFlag = 0x01;
    private const int CleanStartFlag = 0x02;
    private const int WillFlag = 0x04;
    private const int WillQualityOfServiceShift = 3;
    private const int WillRetainFlag = 0x20;
    private const int PasswordFlag = 0x40;
    private const int UserNameFlag = 0x80;

    /// <inheritdoc />
    public override Mqtt5PacketType Type => Mqtt5PacketType.Connect;

    /// <summary>
    /// Reads a <c>CONNECT</c> body.
    /// </summary>
    /// <param name="reader">The body.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="Mqtt5PacketFaultException">The body breaks MQTT 5.0.</exception>
    public static Mqtt5ConnectPacket Read(Mqtt5BodyReader reader)
    {
        RequireMqttLevel5(reader.ReadUtf8String(), reader.ReadByte());
        var flags = reader.ReadByte();
        RequireValidConnectFlags(flags);
        var keepAlive = reader.ReadTwoByteInteger();
        var properties = reader.ReadProperties(Mqtt5PropertyContext.Connect);
        var clientIdentifier = reader.ReadUtf8String();
        var will = HasFlag(flags, WillFlag) ? ReadWill(reader, flags) : null;
        var userName = HasFlag(flags, UserNameFlag) ? reader.ReadUtf8String() : null;
        var password = HasFlag(flags, PasswordFlag) ? reader.ReadBinaryData() : null;
        reader.RequireEnd("[MQTT-3.1.3-1]");
        return new(HasFlag(flags, CleanStartFlag), keepAlive, properties, clientIdentifier, will, userName, password);
    }

    /// <inheritdoc />
    public override void WriteBody(Mqtt5BodyWriter writer)
    {
        writer.WriteUtf8String(ProtocolName);
        writer.WriteByte(ProtocolLevel);
        writer.WriteByte(ConnectFlags());
        writer.WriteTwoByteInteger(KeepAlive);
        writer.WriteProperties(Properties);
        writer.WriteUtf8String(ClientIdentifier);
        if (Will is not null)
        {
            writer.WriteProperties(Will.Properties);
            writer.WriteUtf8String(Will.Topic);
            writer.WriteBinaryData(Will.Payload);
        }

        if (UserName is not null)
        {
            writer.WriteUtf8String(UserName);
        }

        if (Password is not null)
        {
            writer.WriteBinaryData(Password);
        }
    }

    private static bool HasFlag(int flags, int flag) => (flags & flag) != 0;

    private static void RequireMqttLevel5(string protocolName, byte protocolLevel)
    {
        if (protocolName == ProtocolName && protocolLevel == ProtocolLevel)
        {
            return;
        }

        throw Mqtt5BodyReader.Fault(protocolName is ProtocolName or ProtocolNameOfMqtt31
            ? Mqtt5PacketFault.UnsupportedProtocolLevel
            : Mqtt5PacketFault.UnknownProtocolName);
    }

    private static void RequireValidConnectFlags(int flags)
    {
        var willQualityOfService = (flags >> WillQualityOfServiceShift) & 0x03;
        var statement = HasFlag(flags, ReservedFlag)
            ? "[MQTT-3.1.2-3]"
            : HasFlag(flags, WillFlag)
                ? (willQualityOfService == 3 ? "[MQTT-3.1.2-12]" : null)
                : StatementBrokenWithoutAWill(flags, willQualityOfService);

        if (statement is not null)
        {
            throw Mqtt5BodyReader.Fault(Mqtt5PacketFault.Malformed(statement));
        }
    }

    // With the Will Flag clear, Will QoS and Will Retain must be 0 ([MQTT-3.1.2-11], [MQTT-3.1.2-13]).
    private static string? StatementBrokenWithoutAWill(int flags, int willQualityOfService) =>
        willQualityOfService != 0
            ? "[MQTT-3.1.2-11]"
            : HasFlag(flags, WillRetainFlag) ? "[MQTT-3.1.2-13]" : null;

    private static Mqtt5Will ReadWill(Mqtt5BodyReader reader, int flags)
    {
        var properties = reader.ReadProperties(Mqtt5PropertyContext.Will);
        var topic = reader.ReadUtf8String();
        var payload = reader.ReadBinaryData();
        return new((flags >> WillQualityOfServiceShift) & 0x03, HasFlag(flags, WillRetainFlag), properties, topic, payload);
    }

    private byte ConnectFlags()
    {
        var flags = CleanStart ? CleanStartFlag : 0;
        if (Will is not null)
        {
            flags |= WillFlag | (Will.QualityOfService << WillQualityOfServiceShift) | (Will.Retain ? WillRetainFlag : 0);
        }

        flags |= UserName is null ? 0 : UserNameFlag;
        flags |= Password is null ? 0 : PasswordFlag;
        return (byte)flags;
    }
}
