namespace Surl.Protocol.Mqtt;

/// <summary>
/// One MQTT control packet as read from the wire: its first byte and the bytes its remaining
/// length covers (MQTT 3.1.1, section 2).
/// </summary>
/// <param name="FirstByte">The fixed header's first byte: the packet type and its flags.</param>
/// <param name="Body">The variable header and payload, exactly the remaining length long.</param>
internal sealed record MqttPacket(byte FirstByte, byte[] Body)
{
    /// <summary>
    /// The packet type, from the high four bits of <see cref="FirstByte"/>.
    /// </summary>
    public MqttPacketType Type => (MqttPacketType)(FirstByte >> 4);

    /// <summary>
    /// The flags, the low four bits of <see cref="FirstByte"/>.
    /// </summary>
    public int Flags => FirstByte & 0x0F;
}
