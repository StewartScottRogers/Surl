namespace Surl.Authentication;

/// <summary>
/// The hash an HTTP Digest exchange uses (RFC 7616 section 3.2): the three ADR-0032 section 4
/// offers, in the order it offers them. Each also has a <c>-sess</c> form (RFC 7616 section
/// 3.4.2), told apart by <see cref="DigestAlgorithmName.IsSession"/>.
/// </summary>
public enum DigestAlgorithm
{
    /// <summary>
    /// <c>MD5</c>, from the base class library.
    /// </summary>
    Md5,

    /// <summary>
    /// <c>SHA-256</c>, from the base class library.
    /// </summary>
    Sha256,

    /// <summary>
    /// <c>SHA-512-256</c>: SHA-512/256 (FIPS 180-4), hand-built in <c>Surl.Cryptography</c>.
    /// </summary>
    Sha512Slash256,
}
