namespace Surl.Dtls;

/// <summary>
/// The alert descriptions ADR-0087 decision 10 sends or names (RFC 5246 section 7.2).
/// </summary>
public enum DtlsAlertDescription : byte
{
    /// <summary><c>close_notify</c> (0).</summary>
    CloseNotify = 0,

    /// <summary><c>unexpected_message</c> (10): a message the state machine does not expect.</summary>
    UnexpectedMessage = 10,

    /// <summary><c>handshake_failure</c> (40): no live suite, group or signature scheme, or a limit hit.</summary>
    HandshakeFailure = 40,

    /// <summary><c>bad_certificate</c> (42).</summary>
    BadCertificate = 42,

    /// <summary><c>unsupported_certificate</c> (43).</summary>
    UnsupportedCertificate = 43,

    /// <summary><c>certificate_expired</c> (45).</summary>
    CertificateExpired = 45,

    /// <summary><c>illegal_parameter</c> (47): no <c>null</c> compression, no uncompressed points, a bad point.</summary>
    IllegalParameter = 47,

    /// <summary><c>unknown_ca</c> (48).</summary>
    UnknownCa = 48,

    /// <summary><c>decode_error</c> (50): a malformed handshake message.</summary>
    DecodeError = 50,

    /// <summary><c>decrypt_error</c> (51): a <c>Finished</c> or <c>CertificateVerify</c> that does not verify.</summary>
    DecryptError = 51,

    /// <summary><c>protocol_version</c> (70): a <c>client_version</c> below DTLS 1.2.</summary>
    ProtocolVersion = 70,

    /// <summary><c>no_renegotiation</c> (100), a warning.</summary>
    NoRenegotiation = 100,
}
