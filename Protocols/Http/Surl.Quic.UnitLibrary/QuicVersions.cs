namespace Surl.Quic;

/// <summary>The QUIC version numbers a long header carries.</summary>
public static class QuicVersions
{
    /// <summary>The version field of a Version Negotiation packet (RFC 9000 section 17.2.1).</summary>
    public const uint VersionNegotiation = 0x00000000;

    /// <summary>QUIC version 1 (RFC 9000 section 15), the only version Surl speaks (ADR-0082 decision 4).</summary>
    public const uint Version1 = 0x00000001;
}
