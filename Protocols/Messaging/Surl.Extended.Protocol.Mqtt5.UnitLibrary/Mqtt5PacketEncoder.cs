namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// Encodes an MQTT 5.0 control packet as the exact bytes that go on the wire: the fixed
/// header (section 2.1) - type, flags and Remaining Length - then the packet's body.
/// </summary>
internal static class Mqtt5PacketEncoder
{
    /// <summary>
    /// Encodes <paramref name="packet"/>.
    /// </summary>
    /// <param name="packet">The packet.</param>
    /// <returns>Its bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The body is longer than a Remaining Length can say.</exception>
    public static byte[] Encode(Mqtt5Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var body = new Mqtt5BodyWriter();
        packet.WriteBody(body);

        var whole = new Mqtt5BodyWriter();
        whole.WriteByte((byte)(((int)packet.Type << 4) | packet.FixedHeaderFlags));
        whole.WriteVariableByteInteger(body.Length);
        whole.WriteBytes(body.WrittenSpan);
        return whole.WrittenSpan.ToArray();
    }
}
