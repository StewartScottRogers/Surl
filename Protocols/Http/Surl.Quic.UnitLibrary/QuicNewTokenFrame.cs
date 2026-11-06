namespace Surl.Quic;

/// <summary>
/// A <c>NEW_TOKEN</c> frame (type <c>0x07</c>, RFC 9000 section 19.7): a token the client may
/// send in a later connection's Initial packets. An empty token is
/// <see cref="QuicTransportErrorCode.FrameEncodingError"/>.
/// </summary>
/// <param name="Token">The token; at least one byte.</param>
public sealed record QuicNewTokenFrame(ReadOnlyMemory<byte> Token) : QuicFrame
{
    internal static QuicNewTokenFrame ReadFrom(QuicReader reader, byte frameType)
    {
        var token = reader.ReadLengthPrefixedBytes();
        return token.IsEmpty
            ? throw reader.Fault(QuicTransportErrorCode.FrameEncodingError, "A NEW_TOKEN frame carries an empty token.")
            : new QuicNewTokenFrame(token);
    }

    internal override void WriteTo(QuicWriter writer)
    {
        ArgumentOutOfRangeException.ThrowIfZero(Token.Length, nameof(Token));
        writer.WriteByte(QuicFrameTypes.NewToken);
        writer.WriteLengthPrefixedBytes(Token.Span);
    }
}
