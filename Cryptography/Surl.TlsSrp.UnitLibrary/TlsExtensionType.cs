namespace Surl.TlsSrp;

/// <summary>
/// The ClientHello extensions the TLS-SRP server reads or answers (ADR-0122 decision 3). Every
/// other type is ignored, as RFC 5246 section 7.4.1.4 requires.
/// </summary>
public static class TlsExtensionType
{
    /// <summary><c>server_name</c> (0), RFC 6066 section 3.</summary>
    public const ushort ServerName = 0x0000;

    /// <summary><c>srp</c> (12), RFC 5054 section 2.8.1: the user name.</summary>
    public const ushort Srp = 0x000C;

    /// <summary><c>application_layer_protocol_negotiation</c> (16), RFC 7301.</summary>
    public const ushort ApplicationLayerProtocolNegotiation = 0x0010;

    /// <summary><c>encrypt_then_mac</c> (22), RFC 7366.</summary>
    public const ushort EncryptThenMac = 0x0016;

    /// <summary><c>extended_master_secret</c> (23), RFC 7627.</summary>
    public const ushort ExtendedMasterSecret = 0x0017;

    /// <summary><c>supported_versions</c> (43), RFC 8446 section 4.2.1.</summary>
    public const ushort SupportedVersions = 0x002B;

    /// <summary><c>renegotiation_info</c> (65281), RFC 5746.</summary>
    public const ushort RenegotiationInfo = 0xFF01;
}
