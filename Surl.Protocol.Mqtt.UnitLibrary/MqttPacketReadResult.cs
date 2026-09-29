namespace Surl.Protocol.Mqtt;

/// <summary>
/// The result of reading one MQTT packet: the packet, or the reason there is none.
/// </summary>
/// <param name="Outcome">How the read ended.</param>
/// <param name="Packet">
/// The packet; <see langword="null"/> unless <paramref name="Outcome"/> is
/// <see cref="MqttPacketReadOutcome.PacketRead"/>.
/// </param>
internal sealed record MqttPacketReadResult(MqttPacketReadOutcome Outcome, MqttPacket? Packet)
{
    /// <summary>
    /// A result that carries a packet.
    /// </summary>
    /// <param name="packet">The packet read.</param>
    /// <returns>A <see cref="MqttPacketReadOutcome.PacketRead"/> result.</returns>
    public static MqttPacketReadResult Read(MqttPacket packet) => new(MqttPacketReadOutcome.PacketRead, packet);

    /// <summary>
    /// A result that carries no packet.
    /// </summary>
    /// <param name="outcome">Why there is no packet.</param>
    /// <returns>A result with <see cref="Packet"/> <see langword="null"/>.</returns>
    public static MqttPacketReadResult NoPacket(MqttPacketReadOutcome outcome) => new(outcome, null);
}
