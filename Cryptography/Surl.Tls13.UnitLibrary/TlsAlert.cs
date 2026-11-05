namespace Surl.Tls13;

/// <summary>
/// The TLS alert descriptions of RFC 8446 section 6. Surl's TLS 1.3 sends no alert on the wire
/// (QUIC has no record layer, ADR-0082 decision 2): an alert is reported to the caller, which
/// closes the QUIC connection with error <c>0x0100</c> plus the alert number (RFC 9001 section 4.8).
/// </summary>
public enum TlsAlert : byte
{
    /// <summary>close_notify (0).</summary>
    CloseNotify = 0,

    /// <summary>unexpected_message (10): an inappropriate message, such as the wrong handshake message, was received.</summary>
    UnexpectedMessage = 10,

    /// <summary>bad_record_mac (20).</summary>
    BadRecordMac = 20,

    /// <summary>record_overflow (22).</summary>
    RecordOverflow = 22,

    /// <summary>handshake_failure (40): no acceptable set of security parameters could be negotiated.</summary>
    HandshakeFailure = 40,

    /// <summary>bad_certificate (42).</summary>
    BadCertificate = 42,

    /// <summary>unsupported_certificate (43).</summary>
    UnsupportedCertificate = 43,

    /// <summary>certificate_revoked (44).</summary>
    CertificateRevoked = 44,

    /// <summary>certificate_expired (45).</summary>
    CertificateExpired = 45,

    /// <summary>certificate_unknown (46).</summary>
    CertificateUnknown = 46,

    /// <summary>illegal_parameter (47): a field was incorrect or inconsistent with other fields.</summary>
    IllegalParameter = 47,

    /// <summary>unknown_ca (48).</summary>
    UnknownCa = 48,

    /// <summary>access_denied (49).</summary>
    AccessDenied = 49,

    /// <summary>decode_error (50): a field was out of its range or a length was incorrect.</summary>
    DecodeError = 50,

    /// <summary>decrypt_error (51): a handshake cryptographic operation, such as a Finished check, failed.</summary>
    DecryptError = 51,

    /// <summary>protocol_version (70): the version the peer offered is recognized but not supported.</summary>
    ProtocolVersion = 70,

    /// <summary>insufficient_security (71).</summary>
    InsufficientSecurity = 71,

    /// <summary>internal_error (80).</summary>
    InternalError = 80,

    /// <summary>inappropriate_fallback (86).</summary>
    InappropriateFallback = 86,

    /// <summary>user_canceled (90).</summary>
    UserCanceled = 90,

    /// <summary>missing_extension (109): a mandatory extension was absent.</summary>
    MissingExtension = 109,

    /// <summary>unsupported_extension (110).</summary>
    UnsupportedExtension = 110,

    /// <summary>unrecognized_name (112).</summary>
    UnrecognizedName = 112,

    /// <summary>bad_certificate_status_response (113).</summary>
    BadCertificateStatusResponse = 113,

    /// <summary>unknown_psk_identity (115).</summary>
    UnknownPskIdentity = 115,

    /// <summary>certificate_required (116).</summary>
    CertificateRequired = 116,

    /// <summary>no_application_protocol (120): the client offered no protocol the server supports (RFC 7301).</summary>
    NoApplicationProtocol = 120,
}
