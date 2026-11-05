namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// One MQTT 5.0 control packet, decoded: its type, the flags of its fixed header (section
/// 2.1.3), and how it writes its body. <see cref="Mqtt5PacketEncoder"/> adds the fixed header.
/// </summary>
internal abstract record Mqtt5Packet
{
    /// <summary>
    /// The packet type.
    /// </summary>
    public abstract Mqtt5PacketType Type { get; }

    /// <summary>
    /// The low four bits of the first byte (table 2-2): <c>0010</c> for <c>PUBREL</c>,
    /// <c>SUBSCRIBE</c> and <c>UNSUBSCRIBE</c>, DUP, QoS and RETAIN for <c>PUBLISH</c>, 0
    /// otherwise.
    /// </summary>
    public virtual int FixedHeaderFlags => 0;

    /// <summary>
    /// Writes the variable header and payload.
    /// </summary>
    /// <param name="writer">Where to write them.</param>
    public abstract void WriteBody(Mqtt5BodyWriter writer);
}
