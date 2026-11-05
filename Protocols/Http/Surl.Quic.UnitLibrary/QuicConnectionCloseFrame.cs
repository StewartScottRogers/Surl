namespace Surl.Quic;

/// <summary>
/// A <c>CONNECTION_CLOSE</c> frame (RFC 9000 section 19.19): type <c>0x1c</c> closes with a
/// transport error and names the frame type that caused it, type <c>0x1d</c> closes with an
/// application error and carries no frame type.
/// </summary>
/// <param name="IsApplicationError">Whether this is type <c>0x1d</c>, an application protocol's error.</param>
/// <param name="ErrorCode">The error: a <see cref="QuicTransportErrorCode"/> value on type <c>0x1c</c>, the application's code on <c>0x1d</c>.</param>
/// <param name="FrameType">The type of the frame that caused the error, 0 when unknown; written on type <c>0x1c</c> only.</param>
/// <param name="ReasonPhrase">Why, in UTF-8 for a person to read; may be empty.</param>
public sealed record QuicConnectionCloseFrame(bool IsApplicationError, ulong ErrorCode, ulong FrameType, ReadOnlyMemory<byte> ReasonPhrase) : QuicFrame
{
    internal static QuicConnectionCloseFrame ReadFrom(QuicReader reader, byte frameType)
    {
        var isApplicationError = frameType == QuicFrameTypes.ConnectionCloseApplication;
        var errorCode = reader.ReadVariableLengthInteger();
        var causingFrameType = isApplicationError ? 0 : reader.ReadVariableLengthInteger();
        return new QuicConnectionCloseFrame(isApplicationError, errorCode, causingFrameType, reader.ReadLengthPrefixedBytes());
    }

    internal override void WriteTo(QuicWriter writer)
    {
        writer.WriteByte(IsApplicationError ? QuicFrameTypes.ConnectionCloseApplication : QuicFrameTypes.ConnectionCloseTransport);
        writer.WriteVariableLengthInteger(ErrorCode);
        if (!IsApplicationError)
        {
            writer.WriteVariableLengthInteger(FrameType);
        }

        writer.WriteLengthPrefixedBytes(ReasonPhrase.Span);
    }
}
