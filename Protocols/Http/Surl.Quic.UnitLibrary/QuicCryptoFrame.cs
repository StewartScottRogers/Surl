namespace Surl.Quic;

/// <summary>
/// A <c>CRYPTO</c> frame (type <c>0x06</c>, RFC 9000 section 19.6): TLS handshake bytes at an
/// offset in the encryption level's handshake stream. An offset plus length above 2^62-1 is
/// <see cref="QuicTransportErrorCode.CryptoBufferExceeded"/>.
/// </summary>
/// <param name="Offset">Where <paramref name="Data"/> starts in the handshake stream.</param>
/// <param name="Data">The handshake bytes; when read, a slice of the payload.</param>
public sealed record QuicCryptoFrame(ulong Offset, ReadOnlyMemory<byte> Data) : QuicFrame
{
    internal static QuicCryptoFrame ReadFrom(QuicReader reader, byte frameType)
    {
        var offset = reader.ReadVariableLengthInteger();
        var data = reader.ReadLengthPrefixedBytes();
        return offset + (ulong)data.Length > QuicVariableLengthInteger.MaxValue
            ? throw reader.Fault(QuicTransportErrorCode.CryptoBufferExceeded, "A CRYPTO frame ends beyond offset 2^62-1.")
            : new QuicCryptoFrame(offset, data);
    }

    internal override void WriteTo(QuicWriter writer)
    {
        writer.WriteByte(QuicFrameTypes.Crypto);
        writer.WriteVariableLengthInteger(Offset);
        writer.WriteLengthPrefixedBytes(Data.Span);
    }
}
