namespace Surl.Tls13;

/// <summary>
/// Key exchange groups of RFC 8446 section 4.2.7 that ADR-0082 decision 3 names, with
/// X25519MLKEM768 (draft-ietf-tls-ecdhe-mlkem). A client may offer other values, which are kept
/// as they came.
/// </summary>
public enum TlsNamedGroup : ushort
{
    /// <summary>secp256r1 (0x0017).</summary>
    Secp256r1 = 0x0017,

    /// <summary>secp384r1 (0x0018).</summary>
    Secp384r1 = 0x0018,

    /// <summary>secp521r1 (0x0019).</summary>
    Secp521r1 = 0x0019,

    /// <summary>x25519 (0x001D).</summary>
    X25519 = 0x001D,

    /// <summary>x448 (0x001E).</summary>
    X448 = 0x001E,

    /// <summary>ffdhe2048 (0x0100).</summary>
    Ffdhe2048 = 0x0100,

    /// <summary>X25519MLKEM768 (0x11EC).</summary>
    X25519MlKem768 = 0x11EC,
}
