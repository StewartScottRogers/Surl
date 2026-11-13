namespace Surl.Networking;

/// <summary>
/// Why <see cref="TlsClientConnector"/> or <see cref="TlsClientConnection"/> could not give a secured
/// connection to a named host.
/// </summary>
public enum TlsClientFailure
{
    /// <summary>The host name did not resolve, or resolved to no address.</summary>
    HostNotFound,

    /// <summary>The host refused the connection.</summary>
    Refused,

    /// <summary>The host could not be reached for any other socket reason.</summary>
    Unreachable,

    /// <summary>The connection (and, for TLS at once, the handshake) did not finish within the timeout.</summary>
    TimedOut,

    /// <summary>The certificate chain does not build to a system root or a given certificate.</summary>
    UntrustedCertificate,

    /// <summary>The certificate does not name the host (RFC 6125 DNS-ID, or an IP-address SAN for a literal).</summary>
    CertificateNameMismatch,

    /// <summary>The handshake failed for any other reason: no common version, an alert, the peer closing.</summary>
    HandshakeFailed,
}
