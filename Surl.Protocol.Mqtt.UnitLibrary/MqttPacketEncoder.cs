using System.Buffers.Binary;
using System.Text;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// Builds the packets the MQTT server sends, each as the exact bytes that go on the wire
/// (MQTT 3.1.1, sections 2 and 3).
/// </summary>
internal static class MqttPacketEncoder
{
    /// <summary>
    /// The largest remaining length four bytes can encode (MQTT 3.1.1, section 2.2.3).
    /// </summary>
    public const int MaxRemainingLength = 268_435_455;

    /// <summary>
    /// <c>CONNACK</c> with return code 0, connection accepted, and no session present.
    /// </summary>
    public static byte[] ConnectAccepted { get; } = ConnectAcknowledgement(0x00);

    /// <summary>
    /// <c>CONNACK</c> with return code 1, unacceptable protocol version (section 3.2.2.3).
    /// </summary>
    public static byte[] ConnectRefusedUnacceptableProtocolLevel { get; } = ConnectAcknowledgement(0x01);

    /// <summary>
    /// <c>CONNACK</c> with return code 2, identifier rejected (section 3.2.2.3).
    /// </summary>
    public static byte[] ConnectRefusedIdentifierRejected { get; } = ConnectAcknowledgement(0x02);

    /// <summary>
    /// <c>PINGRESP</c> (section 3.13).
    /// </summary>
    public static byte[] PingResponse { get; } = [(byte)MqttPacketType.PingResponse << 4, 0x00];

    /// <summary>
    /// <c>DISCONNECT</c> (section 3.14), which upstream curl reads as the end of a subscribe.
    /// </summary>
    public static byte[] Disconnect { get; } = [(byte)MqttPacketType.Disconnect << 4, 0x00];

    /// <summary>
    /// A four-byte acknowledgement that carries only a packet identifier: <c>PUBACK</c>,
    /// <c>PUBREC</c>, <c>PUBCOMP</c> or <c>UNSUBACK</c>.
    /// </summary>
    /// <param name="type">The acknowledgement's packet type.</param>
    /// <param name="packetIdentifier">The identifier of the packet acknowledged.</param>
    /// <returns>The packet's bytes.</returns>
    public static byte[] PacketIdentifierAcknowledgement(MqttPacketType type, ushort packetIdentifier)
    {
        var packet = new byte[] { (byte)((int)type << 4), 0x02, 0x00, 0x00 };
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), packetIdentifier);
        return packet;
    }

    /// <summary>
    /// <c>SUBACK</c> (section 3.9): the subscribe's packet identifier, then one return code
    /// per topic filter, in order.
    /// </summary>
    /// <param name="packetIdentifier">The identifier of the <c>SUBSCRIBE</c> acknowledged.</param>
    /// <param name="returnCodes">One return code per topic filter.</param>
    /// <returns>The packet's bytes.</returns>
    public static byte[] SubscribeAcknowledgement(ushort packetIdentifier, IReadOnlyList<byte> returnCodes)
    {
        var body = new byte[sizeof(ushort) + returnCodes.Count];
        BinaryPrimitives.WriteUInt16BigEndian(body, packetIdentifier);
        for (var index = 0; index < returnCodes.Count; index++)
        {
            body[sizeof(ushort) + index] = returnCodes[index];
        }

        return Encode((byte)MqttPacketType.SubscribeAcknowledgement << 4, body);
    }

    /// <summary>
    /// A QoS 0 <c>PUBLISH</c> of a retained message, with its <c>RETAIN</c> flag set as
    /// section 3.3.1.3 requires for a message sent because a subscription was made.
    /// </summary>
    /// <param name="topic">The message's topic name.</param>
    /// <param name="payload">The message's payload.</param>
    /// <returns>The packet's bytes.</returns>
    public static byte[] RetainedPublish(string topic, ReadOnlySpan<byte> payload)
    {
        var topicBytes = Encoding.UTF8.GetBytes(topic);
        var body = new byte[sizeof(ushort) + topicBytes.Length + payload.Length];
        BinaryPrimitives.WriteUInt16BigEndian(body, (ushort)topicBytes.Length);
        topicBytes.CopyTo(body, sizeof(ushort));
        payload.CopyTo(body.AsSpan(sizeof(ushort) + topicBytes.Length));

        return Encode(((byte)MqttPacketType.Publish << 4) | 0x01, body);
    }

    /// <summary>
    /// A packet: its first byte, its remaining length in the variable-length encoding of
    /// section 2.2.3, and its body.
    /// </summary>
    /// <param name="firstByte">The packet type and flags.</param>
    /// <param name="body">The variable header and payload; at most <see cref="MaxRemainingLength"/> bytes.</param>
    /// <returns>The packet's bytes.</returns>
    public static byte[] Encode(byte firstByte, ReadOnlySpan<byte> body)
    {
        Span<byte> remainingLength = stackalloc byte[4];
        var lengthByteCount = EncodeRemainingLength(body.Length, remainingLength);

        var packet = new byte[1 + lengthByteCount + body.Length];
        packet[0] = firstByte;
        remainingLength[..lengthByteCount].CopyTo(packet.AsSpan(1));
        body.CopyTo(packet.AsSpan(1 + lengthByteCount));
        return packet;
    }

    /// <summary>
    /// Writes <paramref name="remainingLength"/> in the variable-length encoding of section
    /// 2.2.3: seven bits a byte, least significant first, the high bit set on every byte but
    /// the last.
    /// </summary>
    /// <param name="remainingLength">The length, 0 through <see cref="MaxRemainingLength"/>.</param>
    /// <param name="destination">At least four bytes.</param>
    /// <returns>How many bytes were written, 1 through 4.</returns>
    public static int EncodeRemainingLength(int remainingLength, Span<byte> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(remainingLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(remainingLength, MaxRemainingLength);

        var count = 0;
        do
        {
            var encodedByte = remainingLength % 128;
            remainingLength /= 128;
            destination[count++] = (byte)(remainingLength > 0 ? encodedByte | 0x80 : encodedByte);
        }
        while (remainingLength > 0);

        return count;
    }

    private static byte[] ConnectAcknowledgement(byte returnCode) =>
        [(byte)MqttPacketType.ConnectAcknowledgement << 4, 0x02, 0x00, returnCode];
}
