namespace Surl.Dtls;

/// <summary>
/// One whole DTLS handshake message: its type, its <c>message_seq</c> and its body, as
/// <see cref="DtlsHandshakeReassembler"/> hands it over and <see cref="DtlsHandshakeFragmenter"/> takes it.
/// </summary>
/// <param name="MessageType">What the body is.</param>
/// <param name="MessageSeq">The message's sequence number within the handshake (RFC 6347 section 4.2.2).</param>
/// <param name="Body">The message body, at most 2^24 - 1 bytes.</param>
public sealed record DtlsHandshakeMessage(DtlsHandshakeType MessageType, ushort MessageSeq, byte[] Body)
{
    /// <summary>
    /// The message as one unfragmented fragment - the 12-byte header with offset 0 and fragment
    /// length equal to the length, then the body - which is what the handshake hash covers
    /// (RFC 6347 section 4.2.6, ADR-0087 decision 5).
    /// </summary>
    /// <returns>The header and the body.</returns>
    public byte[] ToUnfragmentedBytes()
    {
        var bytes = new byte[DtlsHandshakeHeader.Size + Body.Length];
        new DtlsHandshakeHeader(MessageType, Body.Length, MessageSeq, 0, Body.Length).Write(bytes);
        Body.CopyTo(bytes, DtlsHandshakeHeader.Size);
        return bytes;
    }
}
