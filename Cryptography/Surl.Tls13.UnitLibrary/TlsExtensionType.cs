namespace Surl.Tls13;

/// <summary>
/// The extension types Surl's TLS 1.3 reads or writes (RFC 8446 section 4.2, RFC 6066, RFC 7301,
/// RFC 8449 and RFC 9001 section 8.2). Any other value a client sends is kept as it came.
/// </summary>
public enum TlsExtensionType : ushort
{
    /// <summary>server_name (0, RFC 6066).</summary>
    ServerName = 0,

    /// <summary>supported_groups (10).</summary>
    SupportedGroups = 10,

    /// <summary>signature_algorithms (13).</summary>
    SignatureAlgorithms = 13,

    /// <summary>application_layer_protocol_negotiation (16, RFC 7301).</summary>
    ApplicationLayerProtocolNegotiation = 16,

    /// <summary>record_size_limit (28, RFC 8449).</summary>
    RecordSizeLimit = 28,

    /// <summary>pre_shared_key (41).</summary>
    PreSharedKey = 41,

    /// <summary>early_data (42).</summary>
    EarlyData = 42,

    /// <summary>supported_versions (43).</summary>
    SupportedVersions = 43,

    /// <summary>cookie (44).</summary>
    Cookie = 44,

    /// <summary>psk_key_exchange_modes (45).</summary>
    PskKeyExchangeModes = 45,

    /// <summary>key_share (51).</summary>
    KeyShare = 51,

    /// <summary>quic_transport_parameters (57, RFC 9001 section 8.2).</summary>
    QuicTransportParameters = 57,
}
