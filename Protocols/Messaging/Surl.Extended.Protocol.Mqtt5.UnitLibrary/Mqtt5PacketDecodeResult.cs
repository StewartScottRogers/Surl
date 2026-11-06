namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// What decoding one packet gave: the packet, or the fault that refuses it.
/// </summary>
/// <param name="Packet">The packet; <see langword="null"/> when <paramref name="Fault"/> is set.</param>
/// <param name="Fault">Why the packet is refused; <see langword="null"/> when it decoded.</param>
internal sealed record Mqtt5PacketDecodeResult(Mqtt5Packet? Packet, Mqtt5PacketFault? Fault)
{
    /// <summary>
    /// A packet that decoded.
    /// </summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The result.</returns>
    public static Mqtt5PacketDecodeResult Decoded(Mqtt5Packet packet) => new(packet, null);

    /// <summary>
    /// A packet that is refused.
    /// </summary>
    /// <param name="fault">Why.</param>
    /// <returns>The result.</returns>
    public static Mqtt5PacketDecodeResult Refused(Mqtt5PacketFault fault) => new(null, fault);
}
