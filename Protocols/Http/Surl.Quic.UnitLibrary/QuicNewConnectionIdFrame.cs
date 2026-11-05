namespace Surl.Quic;

/// <summary>
/// A <c>NEW_CONNECTION_ID</c> frame (type <c>0x18</c>, RFC 9000 section 19.15): a connection ID
/// the peer may use, with its stateless reset token. A connection ID of 0 or more than 20 bytes,
/// or a <see cref="RetirePriorTo"/> above <see cref="SequenceNumber"/>, is
/// <see cref="QuicTransportErrorCode.FrameEncodingError"/>.
/// </summary>
/// <param name="SequenceNumber">The connection ID's sequence number.</param>
/// <param name="RetirePriorTo">The connection IDs below this sequence number are to be retired.</param>
/// <param name="ConnectionId">The connection ID, 1 to 20 bytes.</param>
/// <param name="StatelessResetToken">The 16-byte stateless reset token.</param>
public sealed record QuicNewConnectionIdFrame(
    ulong SequenceNumber,
    ulong RetirePriorTo,
    ReadOnlyMemory<byte> ConnectionId,
    ReadOnlyMemory<byte> StatelessResetToken) : QuicFrame
{
    /// <summary>The length of a stateless reset token, in bytes.</summary>
    public const int StatelessResetTokenLength = 16;

    internal static QuicNewConnectionIdFrame ReadFrom(QuicReader reader, byte frameType)
    {
        var sequenceNumber = reader.ReadVariableLengthInteger();
        var retirePriorTo = reader.ReadVariableLengthInteger();
        if (retirePriorTo > sequenceNumber)
        {
            throw reader.Fault(QuicTransportErrorCode.FrameEncodingError, "A NEW_CONNECTION_ID frame retires beyond its own sequence number.");
        }

        var length = reader.ReadByte();
        if (length is 0 or > QuicConnectionIds.MaxLength)
        {
            throw reader.Fault(QuicTransportErrorCode.FrameEncodingError, $"A NEW_CONNECTION_ID frame carries a {length}-byte connection ID.");
        }

        return new QuicNewConnectionIdFrame(sequenceNumber, retirePriorTo, reader.ReadBytes(length), reader.ReadBytes(StatelessResetTokenLength));
    }

    internal override void WriteTo(QuicWriter writer)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(RetirePriorTo, SequenceNumber);
        ArgumentOutOfRangeException.ThrowIfZero(ConnectionId.Length, nameof(ConnectionId));
        QuicConnectionIds.RequireValidLength(ConnectionId, nameof(ConnectionId));
        ArgumentOutOfRangeException.ThrowIfNotEqual(StatelessResetToken.Length, StatelessResetTokenLength, nameof(StatelessResetToken));
        writer.WriteByte(QuicFrameTypes.NewConnectionId);
        writer.WriteVariableLengthInteger(SequenceNumber);
        writer.WriteVariableLengthInteger(RetirePriorTo);
        writer.WriteByte((byte)ConnectionId.Length);
        writer.WriteBytes(ConnectionId.Span);
        writer.WriteBytes(StatelessResetToken.Span);
    }
}
