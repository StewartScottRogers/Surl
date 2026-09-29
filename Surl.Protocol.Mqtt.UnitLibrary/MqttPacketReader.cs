using System.Buffers.Binary;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// Reads MQTT control packets from one connection, one after another (MQTT 3.1.1, section 2.2).
/// </summary>
/// <remarks>
/// The fixed header is read one byte at a time and the body with reads no longer than what
/// is left of it, so the reader never reads a byte past the packet it is reading. That is
/// what lets it refuse a packet over the packet limit from its fixed header alone, without
/// reading any of its body (ADR-0006, sections 1 and 5). The limit counts the whole packet,
/// fixed header included. A <c>PUBLISH</c> is also held to the payload limit: its body's
/// first two bytes, the topic name's length, are read on their own, and a payload - the
/// remaining length less the topic name and, above QoS 0, the packet identifier (section
/// 3.3) - over the limit is refused before any more of the body is read. The body's buffer
/// grows as its bytes arrive rather than being allocated at the announced length, so a
/// remaining length the peer never sends costs at most 64 KiB and at most twice what did
/// arrive. It is not safe for concurrent calls, and after any outcome but
/// <see cref="MqttPacketReadOutcome.PacketRead"/> the caller stops reading.
/// </remarks>
internal sealed class MqttPacketReader
{
    private const int MaxRemainingLengthBytes = 4;
    private const int TopicNameLengthBytes = 2;
    private const int PacketIdentifierBytes = 2;

    /// <summary>
    /// How much of a body is allocated before any of it arrives. The buffer doubles as bytes
    /// come, so a peer that announces a large remaining length and sends nothing costs no more.
    /// </summary>
    private const int InitialBodyBytes = 65536;

    private readonly IConnection connection;
    private readonly long maxPacketBytes;
    private readonly long maxPublishPayloadBytes;
    private readonly byte[] oneByte = new byte[1];

    /// <summary>
    /// Creates a reader over <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="maxPacketBytes">The most bytes a packet may hold, fixed header included; 0 means no limit.</param>
    /// <param name="maxPublishPayloadBytes">The most bytes a <c>PUBLISH</c> payload may hold; 0 means no limit.</param>
    public MqttPacketReader(IConnection connection, long maxPacketBytes, long maxPublishPayloadBytes)
    {
        this.connection = connection;
        this.maxPacketBytes = maxPacketBytes;
        this.maxPublishPayloadBytes = maxPublishPayloadBytes;
    }

    /// <summary>
    /// Reads the next packet.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The packet, or the named reason there is none.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<MqttPacketReadResult> ReadPacketAsync(CancellationToken cancellationToken)
    {
        var firstByte = await ReadByteAsync(cancellationToken);
        if (firstByte < 0)
        {
            return MqttPacketReadResult.NoPacket(MqttPacketReadOutcome.ConnectionClosed);
        }

        var (outcome, remainingLength, lengthByteCount) = await ReadRemainingLengthAsync(cancellationToken);
        if (outcome != MqttPacketReadOutcome.PacketRead)
        {
            return MqttPacketReadResult.NoPacket(outcome);
        }

        return IsOverLimit(maxPacketBytes, 1L + lengthByteCount + remainingLength)
            ? MqttPacketReadResult.NoPacket(MqttPacketReadOutcome.PacketTooLarge)
            : await ReadBodyAsync((byte)firstByte, remainingLength, cancellationToken);
    }

    private static bool IsOverLimit(long limit, long byteCount) => limit > 0 && byteCount > limit;

    // What is left of a PUBLISH body once the topic name and packet identifier are taken off;
    // negative when they do not fit, which the responder refuses as malformed.
    private static long PublishPayloadLength(byte firstByte, int remainingLength, byte[] body)
    {
        var qualityOfService = (firstByte >> 1) & 0x03;
        var topicNameLength = BinaryPrimitives.ReadUInt16BigEndian(body);
        var packetIdentifierLength = qualityOfService > 0 ? PacketIdentifierBytes : 0;

        return (long)remainingLength - TopicNameLengthBytes - topicNameLength - packetIdentifierLength;
    }

    private async ValueTask<(MqttPacketReadOutcome Outcome, int RemainingLength, int LengthByteCount)> ReadRemainingLengthAsync(
        CancellationToken cancellationToken)
    {
        var remainingLength = 0;
        for (var index = 0; index < MaxRemainingLengthBytes; index++)
        {
            var encodedByte = await ReadByteAsync(cancellationToken);
            if (encodedByte < 0)
            {
                return (MqttPacketReadOutcome.ConnectionClosedMidPacket, 0, 0);
            }

            remainingLength |= (encodedByte & 0x7F) << (7 * index);
            if ((encodedByte & 0x80) == 0)
            {
                return (MqttPacketReadOutcome.PacketRead, remainingLength, index + 1);
            }
        }

        return (MqttPacketReadOutcome.MalformedRemainingLength, 0, 0);
    }

    private async ValueTask<int> ReadByteAsync(CancellationToken cancellationToken) =>
        await connection.ReadAsync(oneByte, cancellationToken) == 0 ? -1 : oneByte[0];

    private async ValueTask<MqttPacketReadResult> ReadBodyAsync(byte firstByte, int remainingLength, CancellationToken cancellationToken)
    {
        var body = new byte[Math.Min(remainingLength, InitialBodyBytes)];
        var filled = 0;
        if (IsHeldToThePayloadLimit(firstByte, remainingLength))
        {
            if (await ReadUntilAsync(body, 0, TopicNameLengthBytes, cancellationToken) is null)
            {
                return MqttPacketReadResult.NoPacket(MqttPacketReadOutcome.ConnectionClosedMidPacket);
            }

            if (IsOverLimit(maxPublishPayloadBytes, PublishPayloadLength(firstByte, remainingLength, body)))
            {
                return MqttPacketReadResult.NoPacket(MqttPacketReadOutcome.PublishPayloadTooLarge);
            }

            filled = TopicNameLengthBytes;
        }

        var wholeBody = await ReadUntilAsync(body, filled, remainingLength, cancellationToken);
        return wholeBody is null
            ? MqttPacketReadResult.NoPacket(MqttPacketReadOutcome.ConnectionClosedMidPacket)
            : MqttPacketReadResult.Read(new MqttPacket(firstByte, wholeBody));
    }

    private bool IsHeldToThePayloadLimit(byte firstByte, int remainingLength) =>
        (MqttPacketType)(firstByte >> 4) == MqttPacketType.Publish
        && maxPublishPayloadBytes > 0
        && remainingLength >= TopicNameLengthBytes;

    // Fills body from filled up to target bytes, growing it as they arrive and never reading
    // past target; returns the filled buffer, or null when the client closed first.
    private async ValueTask<byte[]?> ReadUntilAsync(byte[] body, int filled, int target, CancellationToken cancellationToken)
    {
        while (filled < target)
        {
            if (filled == body.Length)
            {
                Array.Resize(ref body, (int)Math.Min(target, 2L * body.Length));
            }

            var read = await connection.ReadAsync(body.AsMemory(filled, Math.Min(body.Length, target) - filled), cancellationToken);
            if (read == 0)
            {
                return null;
            }

            filled += read;
        }

        return body;
    }
}
