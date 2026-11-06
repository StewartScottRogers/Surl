namespace Surl.Tls13;

/// <summary>
/// The QUIC encryption level a handshake message travels at (RFC 9001 section 4.1.4): QUIC
/// carries it in a <c>CRYPTO</c> frame of the matching packet number space.
/// </summary>
public enum TlsEncryptionLevel
{
    /// <summary>Initial packets: ClientHello, ServerHello and HelloRetryRequest.</summary>
    Initial,

    /// <summary>Handshake packets: EncryptedExtensions to Finished, protected with the handshake traffic secrets.</summary>
    Handshake,

    /// <summary>1-RTT packets, protected with the application traffic secrets. A QUIC client sends no handshake message here.</summary>
    Application,
}
