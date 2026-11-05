namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// Thrown inside the decoder when a packet's bytes break MQTT 5.0, and caught by
/// <see cref="Mqtt5PacketDecoder.Decode"/>, which reports its <see cref="Fault"/>. It never
/// leaves the decoder.
/// </summary>
/// <param name="fault">What is wrong with the packet.</param>
internal sealed class Mqtt5PacketFaultException(Mqtt5PacketFault fault) : Exception(fault.Statement)
{
    /// <summary>
    /// What is wrong with the packet.
    /// </summary>
    public Mqtt5PacketFault Fault { get; } = fault;
}
