namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// Where a property list stands (table 2-4): bit <c>n</c> is the packet of
/// <see cref="Mqtt5PacketType"/> <c>n</c>, and bit 0 - packet type 0 is reserved - is the
/// Will Properties of a <c>CONNECT</c> payload, which table 2-4 lists as a place of its own.
/// </summary>
[Flags]
internal enum Mqtt5PropertyContext
{
    /// <summary>The Will Properties in a <c>CONNECT</c> payload (section 3.1.3.2).</summary>
    Will = 1 << 0,

    /// <summary>The variable header of <c>CONNECT</c>.</summary>
    Connect = 1 << (int)Mqtt5PacketType.Connect,

    /// <summary><c>CONNACK</c>.</summary>
    ConnectAcknowledgement = 1 << (int)Mqtt5PacketType.ConnectAcknowledgement,

    /// <summary><c>PUBLISH</c>.</summary>
    Publish = 1 << (int)Mqtt5PacketType.Publish,

    /// <summary><c>PUBACK</c>.</summary>
    PublishAcknowledgement = 1 << (int)Mqtt5PacketType.PublishAcknowledgement,

    /// <summary><c>PUBREC</c>.</summary>
    PublishReceived = 1 << (int)Mqtt5PacketType.PublishReceived,

    /// <summary><c>PUBREL</c>.</summary>
    PublishRelease = 1 << (int)Mqtt5PacketType.PublishRelease,

    /// <summary><c>PUBCOMP</c>.</summary>
    PublishComplete = 1 << (int)Mqtt5PacketType.PublishComplete,

    /// <summary><c>SUBSCRIBE</c>.</summary>
    Subscribe = 1 << (int)Mqtt5PacketType.Subscribe,

    /// <summary><c>SUBACK</c>.</summary>
    SubscribeAcknowledgement = 1 << (int)Mqtt5PacketType.SubscribeAcknowledgement,

    /// <summary><c>UNSUBSCRIBE</c>.</summary>
    Unsubscribe = 1 << (int)Mqtt5PacketType.Unsubscribe,

    /// <summary><c>UNSUBACK</c>.</summary>
    UnsubscribeAcknowledgement = 1 << (int)Mqtt5PacketType.UnsubscribeAcknowledgement,

    /// <summary><c>DISCONNECT</c>.</summary>
    Disconnect = 1 << (int)Mqtt5PacketType.Disconnect,

    /// <summary><c>AUTH</c>.</summary>
    Authentication = 1 << (int)Mqtt5PacketType.Authentication,
}
