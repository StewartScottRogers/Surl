namespace Surl.TlsSrp;

/// <summary>
/// The alerts the TLS-SRP server sends (RFC 5246 section 7.2, RFC 5054 section 2.9, ADR-0122
/// decision 6).
/// </summary>
public enum TlsAlertDescription : byte
{
    /// <summary><c>close_notify</c> (0).</summary>
    CloseNotify = 0,

    /// <summary><c>unexpected_message</c> (10): a message or record type out of place.</summary>
    UnexpectedMessage = 10,

    /// <summary>
    /// <c>bad_record_mac</c> (20): a record whose MAC or padding fails; the two are answered
    /// alike (RFC 5246 section 6.2.3.2).
    /// </summary>
    BadRecordMac = 20,

    /// <summary><c>record_overflow</c> (22): a record longer than RFC 5246 section 6.2 allows.</summary>
    RecordOverflow = 22,

    /// <summary><c>handshake_failure</c> (40).</summary>
    HandshakeFailure = 40,

    /// <summary><c>illegal_parameter</c> (47).</summary>
    IllegalParameter = 47,

    /// <summary><c>decode_error</c> (50).</summary>
    DecodeError = 50,

    /// <summary><c>decrypt_error</c> (51).</summary>
    DecryptError = 51,

    /// <summary><c>protocol_version</c> (70).</summary>
    ProtocolVersion = 70,

    /// <summary><c>no_renegotiation</c> (100), sent as a warning.</summary>
    NoRenegotiation = 100,

    /// <summary><c>unknown_psk_identity</c> (115), RFC 5054 section 2.5.1.2.</summary>
    UnknownPskIdentity = 115,

    /// <summary><c>no_application_protocol</c> (120), RFC 7301 section 3.2.</summary>
    NoApplicationProtocol = 120,
}
