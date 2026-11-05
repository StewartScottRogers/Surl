namespace Surl.Dtls;

/// <summary>
/// The hello extensions ADR-0087 decision 5 answers or names. Any other number is still read and
/// carried as a <see cref="DtlsExtension"/>; this list only names the ones the server acts on.
/// </summary>
public enum DtlsExtensionType : ushort
{
    /// <summary><c>server_name</c> (0), ignored (ADR-0087 decision 5).</summary>
    ServerName = 0,

    /// <summary><c>max_fragment_length</c> (1), RFC 6066 section 4.</summary>
    MaxFragmentLength = 1,

    /// <summary><c>supported_groups</c> (10), RFC 8422 section 5.1.1.</summary>
    SupportedGroups = 10,

    /// <summary><c>ec_point_formats</c> (11), RFC 8422 section 5.1.2.</summary>
    EcPointFormats = 11,

    /// <summary><c>signature_algorithms</c> (13), RFC 5246 section 7.4.1.4.1.</summary>
    SignatureAlgorithms = 13,

    /// <summary><c>client_certificate_type</c> (19), RFC 7250.</summary>
    ClientCertificateType = 19,

    /// <summary><c>server_certificate_type</c> (20), RFC 7250.</summary>
    ServerCertificateType = 20,

    /// <summary><c>extended_master_secret</c> (23), RFC 7627.</summary>
    ExtendedMasterSecret = 23,

    /// <summary><c>renegotiation_info</c> (65281), RFC 5746.</summary>
    RenegotiationInfo = 0xFF01,
}
