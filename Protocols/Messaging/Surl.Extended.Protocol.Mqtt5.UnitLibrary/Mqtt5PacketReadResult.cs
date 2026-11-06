namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The result of reading one MQTT 5.0 packet from a connection.
/// </summary>
/// <param name="Outcome">How the read ended.</param>
/// <param name="Packet">The packet; set only for <see cref="Mqtt5PacketReadOutcome.PacketRead"/>.</param>
/// <param name="Fault">Why it is refused; set only for <see cref="Mqtt5PacketReadOutcome.PacketRefused"/>.</param>
internal sealed record Mqtt5PacketReadResult(Mqtt5PacketReadOutcome Outcome, Mqtt5Packet? Packet, Mqtt5PacketFault? Fault)
{
    /// <summary>
    /// The type named by a refused packet's first byte, when its fixed header was readable;
    /// <see langword="null"/> for every other result, and for a Remaining Length that could not be read.
    /// </summary>
    public Mqtt5PacketType? RefusedPacketType { get; init; }

    /// <summary>
    /// The client closed the connection between packets.
    /// </summary>
    public static Mqtt5PacketReadResult ConnectionClosed { get; } = new(Mqtt5PacketReadOutcome.ConnectionClosed, null, null);

    /// <summary>
    /// The client closed the connection part way through a packet.
    /// </summary>
    public static Mqtt5PacketReadResult ConnectionClosedMidPacket { get; } =
        new(Mqtt5PacketReadOutcome.ConnectionClosedMidPacket, null, null);

    /// <summary>
    /// A refused packet.
    /// </summary>
    /// <param name="fault">Why it is refused.</param>
    /// <returns>The result.</returns>
    public static Mqtt5PacketReadResult Refused(Mqtt5PacketFault fault) => new(Mqtt5PacketReadOutcome.PacketRefused, null, fault);

    /// <summary>
    /// The result of decoding a whole packet.
    /// </summary>
    /// <param name="decoded">What the decoder gave.</param>
    /// <returns>The result.</returns>
    public static Mqtt5PacketReadResult From(Mqtt5PacketDecodeResult decoded) =>
        decoded.Packet is null ? Refused(decoded.Fault!) : new(Mqtt5PacketReadOutcome.PacketRead, decoded.Packet, null);
}
