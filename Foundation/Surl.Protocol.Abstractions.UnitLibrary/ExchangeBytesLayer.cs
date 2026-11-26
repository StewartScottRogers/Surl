namespace Surl.Protocol.Abstractions;

/// <summary>
/// Which layer of an exchange an <see cref="ExchangeBytesEvent"/>'s bytes belong to (ADR-0169, decision 2).
/// </summary>
public enum ExchangeBytesLayer
{
    /// <summary>
    /// The bytes as the exchange's connection or flow carried them: plaintext where the connection itself
    /// does TLS, protected packets included where the server's own code protects them, as with QUIC and DTLS.
    /// </summary>
    Wire = 0,

    /// <summary>
    /// Bytes in the clear, received or sent, as the reporting code holds them: a DTLS application-data
    /// record's plaintext, a QUIC <c>DATAGRAM</c> frame's payload, QUIC <c>STREAM</c> data (received data
    /// put back in stream order) and an HTTP/3 <c>DATA</c> frame's payload without its frame header, in the
    /// pieces it is read or written. An HTTP/3 <c>DATA</c> payload is therefore reported twice: within its
    /// QUIC stream's data and on its own.
    /// </summary>
    Decrypted,

    /// <summary>
    /// A header list rendered as text, such as an HTTP/3 request's or response's header lines. These bytes
    /// are Surl's rendering, not bytes either side sent.
    /// </summary>
    Decoded,
}
