using System.Buffers.Binary;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// Reads MQTT 5.0 control packets from one connection, one after another, however the bytes
/// are split across reads, and decodes each with <see cref="Mqtt5PacketDecoder"/>.
/// </summary>
/// <remarks>
/// Copied from the MQTT 3.1.1 server's <c>MqttPacketReader</c> (ADR-0093 decision 0) and
/// changed for MQTT 5.0: a Remaining Length that is not minimal is refused
/// (<c>[MQTT-1.5.5-1]</c>), and a <c>PUBLISH</c>'s payload length is found past its properties.
/// The fixed header is read one byte at a time and the body with reads no longer than what is
/// left of it, so the reader never reads a byte past the packet it is reading. A packet over
/// the packet limit (ADR-0093 decision 8: the whole packet, fixed header included) is refused
/// <c>0x95</c> from its fixed header alone, without reading its body. A <c>PUBLISH</c> is also
/// held to the payload limit: its Topic Name, Packet Identifier and Property Length are read
/// first, and a payload over the limit is refused <c>0x95</c> before any more is read. The body
/// buffer grows as bytes arrive, so a Remaining Length the peer never sends costs at most
/// 64 KiB and at most twice what did arrive. It is not safe for concurrent calls, and after any
/// outcome but <see cref="Mqtt5PacketReadOutcome.PacketRead"/> the caller stops reading.
/// </remarks>
/// <param name="connection">The connection to read from.</param>
/// <param name="maxPacketBytes">The most bytes a packet may hold, fixed header included; 0 means no limit.</param>
/// <param name="maxPublishPayloadBytes">The most bytes a <c>PUBLISH</c> payload may hold; 0 means no limit.</param>
internal sealed class Mqtt5PacketReader(IConnection connection, long maxPacketBytes, long maxPublishPayloadBytes)
{
    private const int InitialBodyBytes = 65536;
    private const int TopicNameLengthBytes = 2;
    private const int PacketIdentifierBytes = 2;

    private readonly byte[] oneByte = new byte[1];

    /// <summary>
    /// Reads and decodes the next packet.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The packet, or the named reason there is none.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<Mqtt5PacketReadResult> ReadPacketAsync(CancellationToken cancellationToken)
    {
        var firstByte = await ReadByteAsync(cancellationToken);
        if (firstByte < 0)
        {
            return Mqtt5PacketReadResult.ConnectionClosed;
        }

        var (failure, remainingLength, lengthByteCount) = await ReadRemainingLengthAsync(cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        return IsOverLimit(maxPacketBytes, 1L + lengthByteCount + remainingLength)
            ? Mqtt5PacketReadResult.Refused(new(Mqtt5ReasonCode.PacketTooLarge, "3.2.2.3.6"))
            : await ReadBodyAsync((byte)firstByte, remainingLength, cancellationToken);
    }

    private static bool IsOverLimit(long limit, long byteCount) => limit > 0 && byteCount > limit;

    private async ValueTask<(Mqtt5PacketReadResult? Failure, int RemainingLength, int LengthByteCount)> ReadRemainingLengthAsync(
        CancellationToken cancellationToken)
    {
        var remainingLength = 0;
        for (var index = 0; index < Mqtt5BodyReader.MaxVariableByteIntegerBytes; index++)
        {
            var encodedByte = await ReadByteAsync(cancellationToken);
            if (encodedByte < 0)
            {
                return (Mqtt5PacketReadResult.ConnectionClosedMidPacket, 0, 0);
            }

            remainingLength |= (encodedByte & 0x7F) << (7 * index);
            if ((encodedByte & 0x80) == 0)
            {
                return Mqtt5BodyReader.IsNotMinimal(index, encodedByte)
                    ? (Mqtt5PacketReadResult.Refused(Mqtt5PacketFault.Malformed("[MQTT-1.5.5-1]")), 0, 0)
                    : (null, remainingLength, index + 1);
            }
        }

        return (Mqtt5PacketReadResult.Refused(Mqtt5PacketFault.Malformed("1.5.5")), 0, 0);
    }

    private async ValueTask<int> ReadByteAsync(CancellationToken cancellationToken) =>
        await connection.ReadAsync(oneByte, cancellationToken) == 0 ? -1 : oneByte[0];

    private async ValueTask<Mqtt5PacketReadResult> ReadBodyAsync(byte firstByte, int remainingLength, CancellationToken cancellationToken)
    {
        var body = new GrowingBody(remainingLength);
        if (IsHeldToThePayloadLimit(firstByte)
            && await ReadPublishPayloadLengthAsync(body, firstByte, cancellationToken) is long payloadLength
            && IsOverLimit(maxPublishPayloadBytes, payloadLength))
        {
            return Mqtt5PacketReadResult.Refused(new(Mqtt5ReasonCode.PacketTooLarge, "ADR-0093 decision 8"));
        }

        return await FillAsync(body, remainingLength, cancellationToken)
            ? Mqtt5PacketReadResult.From(Mqtt5PacketDecoder.Decode(firstByte, body.Bytes.AsMemory(0, remainingLength)))
            : Mqtt5PacketReadResult.ConnectionClosedMidPacket;
    }

    private bool IsHeldToThePayloadLimit(byte firstByte) =>
        (Mqtt5PacketType)(firstByte >> 4) == Mqtt5PacketType.Publish && maxPublishPayloadBytes > 0;

    // The payload length of a PUBLISH: the Remaining Length less the Topic Name, the Packet
    // Identifier above QoS 0 and the property list (section 3.3.2). Null when the client closed
    // first or the prefix does not fit; the whole-body read then reports either.
    private async ValueTask<long?> ReadPublishPayloadLengthAsync(GrowingBody body, byte firstByte, CancellationToken cancellationToken)
    {
        if (body.Target < TopicNameLengthBytes || !await FillAsync(body, TopicNameLengthBytes, cancellationToken))
        {
            return null;
        }

        var packetIdentifierLength = Mqtt5PublishPacket.QualityOfServiceOf(firstByte & 0x0F) > 0 ? PacketIdentifierBytes : 0;
        var propertyLengthStart = TopicNameLengthBytes + BinaryPrimitives.ReadUInt16BigEndian(body.Bytes) + packetIdentifierLength;
        return await ReadPayloadLengthAfterPropertyLengthAsync(body, propertyLengthStart, cancellationToken);
    }

    private async ValueTask<long?> ReadPayloadLengthAfterPropertyLengthAsync(
        GrowingBody body,
        int propertyLengthStart,
        CancellationToken cancellationToken)
    {
        var propertiesLength = 0;
        for (var index = 0; index < Mqtt5BodyReader.MaxVariableByteIntegerBytes; index++)
        {
            var at = propertyLengthStart + index;
            if (at >= body.Target || !await FillAsync(body, at + 1, cancellationToken))
            {
                return null;
            }

            var encodedByte = body.Bytes[at];
            propertiesLength |= (encodedByte & 0x7F) << (7 * index);
            if ((encodedByte & 0x80) == 0)
            {
                return (long)body.Target - (at + 1) - propertiesLength;
            }
        }

        return null;
    }

    // Fills the body up to count bytes (never past its target), growing it as they arrive;
    // false when the client closed first.
    private async ValueTask<bool> FillAsync(GrowingBody body, int count, CancellationToken cancellationToken)
    {
        while (body.Filled < count)
        {
            if (body.Filled == body.Bytes.Length)
            {
                body.Grow();
            }

            var read = await connection.ReadAsync(body.Bytes.AsMemory(body.Filled, Math.Min(body.Bytes.Length, count) - body.Filled), cancellationToken);
            if (read == 0)
            {
                return false;
            }

            body.Filled += read;
        }

        return true;
    }

    private sealed class GrowingBody(int target)
    {
        public int Target { get; } = target;

        public byte[] Bytes { get; private set; } = new byte[Math.Min(target, InitialBodyBytes)];

        public int Filled { get; set; }

        public void Grow()
        {
            var bytes = Bytes;
            Array.Resize(ref bytes, (int)Math.Min(Target, 2L * bytes.Length));
            Bytes = bytes;
        }
    }
}
