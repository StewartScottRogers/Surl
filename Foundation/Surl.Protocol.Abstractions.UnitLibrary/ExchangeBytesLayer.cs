namespace Surl.Protocol.Abstractions;

/// <summary>
/// Which layer of an exchange an <see cref="ExchangeBytesEvent"/>'s bytes belong to (ADR-0169, decision 2).
/// </summary>
public enum ExchangeBytesLayer
{
    /// <summary>
    /// The bytes as they crossed the connection or flow, protected packets included.
    /// </summary>
    Wire = 0,

    /// <summary>
    /// The plaintext the QUIC or DTLS layer recovered, never reordered or reframed.
    /// </summary>
    Decrypted,

    /// <summary>
    /// Rendered text decoded from the decrypted bytes, such as an HTTP/3 request's header lines.
    /// </summary>
    Decoded,
}
