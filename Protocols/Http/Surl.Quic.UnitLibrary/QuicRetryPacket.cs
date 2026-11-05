namespace Surl.Quic;

/// <summary>
/// A QUIC version 1 Retry packet (RFC 9000 section 17.2.5): a token the client is to echo in its
/// next Initial, and the Retry Integrity Tag of RFC 9001 section 5.8, which this record carries and
/// <see cref="QuicRetryIntegrity"/> computes and checks. Surl sends none (ADR-0082 decision 4); the type exists so the codec covers
/// every long header.
/// </summary>
/// <param name="DestinationConnectionId">The Destination Connection ID, at most 20 bytes.</param>
/// <param name="SourceConnectionId">The Source Connection ID, at most 20 bytes.</param>
/// <param name="RetryToken">The token; everything between the Source Connection ID and the tag.</param>
/// <param name="RetryIntegrityTag">The 16-byte Retry Integrity Tag.</param>
/// <param name="UnusedBits">The first byte's four low bits, which carry no meaning, 0 to 15.</param>
public sealed record QuicRetryPacket(
    ReadOnlyMemory<byte> DestinationConnectionId,
    ReadOnlyMemory<byte> SourceConnectionId,
    ReadOnlyMemory<byte> RetryToken,
    ReadOnlyMemory<byte> RetryIntegrityTag,
    byte UnusedBits) : QuicPacket
{
    /// <summary>The length of the Retry Integrity Tag, in bytes.</summary>
    public const int RetryIntegrityTagLength = 16;

    /// <inheritdoc/>
    public override ReadOnlyMemory<byte> DestinationConnectionId { get; } = DestinationConnectionId;

    /// <summary>Returns the packet's bytes.</summary>
    /// <returns>The packet's encoding.</returns>
    /// <exception cref="ArgumentException">A field holds a value RFC 9000 does not allow.</exception>
    public byte[] ToBytes()
    {
        QuicConnectionIds.RequireValidLength(DestinationConnectionId, nameof(DestinationConnectionId));
        QuicConnectionIds.RequireValidLength(SourceConnectionId, nameof(SourceConnectionId));
        ArgumentOutOfRangeException.ThrowIfNotEqual(RetryIntegrityTag.Length, RetryIntegrityTagLength, nameof(RetryIntegrityTag));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(UnusedBits, QuicPacketHeaderBits.RetryUnusedMask);
        var writer = new QuicWriter();
        writer.WriteByte((byte)(QuicPacketHeaderBits.HeaderForm | QuicPacketHeaderBits.FixedBit
            | (QuicPacketHeaderBits.RetryTypeBits << QuicPacketHeaderBits.LongPacketTypeShift) | UnusedBits));
        writer.WriteUInt32(QuicVersions.Version1);
        writer.WriteByte((byte)DestinationConnectionId.Length);
        writer.WriteBytes(DestinationConnectionId.Span);
        writer.WriteByte((byte)SourceConnectionId.Length);
        writer.WriteBytes(SourceConnectionId.Span);
        writer.WriteBytes(RetryToken.Span);
        writer.WriteBytes(RetryIntegrityTag.Span);
        return writer.ToArray();
    }

    internal static QuicRetryPacket ReadFrom(QuicReader reader, byte firstByte, ReadOnlyMemory<byte> destinationConnectionId, ReadOnlyMemory<byte> sourceConnectionId)
    {
        if (reader.Remaining < RetryIntegrityTagLength)
        {
            throw reader.Fault(QuicTransportErrorCode.ProtocolViolation, "A Retry packet ends before its 16-byte integrity tag.");
        }

        var token = reader.ReadBytes(reader.Remaining - RetryIntegrityTagLength);
        var tag = reader.ReadBytes(RetryIntegrityTagLength);
        return new QuicRetryPacket(destinationConnectionId, sourceConnectionId, token, tag, (byte)(firstByte & QuicPacketHeaderBits.RetryUnusedMask));
    }
}
