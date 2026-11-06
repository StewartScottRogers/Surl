namespace Surl.Dtls;

/// <summary>
/// The ECDHE groups the server exchanges keys on (ADR-0087 decision 5), in its order of preference
/// as listed in <see cref="DtlsServerNegotiation"/>.
/// </summary>
public enum DtlsNamedGroup : ushort
{
    /// <summary><c>secp256r1</c> (23), mandated by RFC 7252.</summary>
    Secp256r1 = 23,

    /// <summary><c>secp384r1</c> (24).</summary>
    Secp384r1 = 24,

    /// <summary><c>x25519</c> (29), from <c>Surl.Cryptography.Curve25519</c>.</summary>
    X25519 = 29,
}
