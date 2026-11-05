namespace Surl.Tls13;

/// <summary>The TLS 1.3 cipher suites (RFC 8446 appendix B.4). A client may offer other values, which are kept as they came.</summary>
public enum TlsCipherSuite : ushort
{
    /// <summary>TLS_AES_128_GCM_SHA256 (0x1301).</summary>
    Aes128GcmSha256 = 0x1301,

    /// <summary>TLS_AES_256_GCM_SHA384 (0x1302).</summary>
    Aes256GcmSha384 = 0x1302,

    /// <summary>TLS_CHACHA20_POLY1305_SHA256 (0x1303).</summary>
    ChaCha20Poly1305Sha256 = 0x1303,
}
