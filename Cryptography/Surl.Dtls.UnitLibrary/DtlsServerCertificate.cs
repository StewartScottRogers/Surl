using System.Security.Cryptography;

namespace Surl.Dtls;

/// <summary>
/// The certificate a DTLS server presents and the key that signs its <c>ServerKeyExchange</c>
/// (ADR-0087 decision 3). The caller reads both from <c>--cert</c> and <c>--key</c> or makes the
/// throwaway one; this library reads no file.
/// </summary>
public sealed class DtlsServerCertificate
{
    private const int MinimumRsaKeySize = 2048;

    private readonly AsymmetricAlgorithm key;

    /// <summary>
    /// Pairs a certificate chain with its leaf's private key.
    /// </summary>
    /// <param name="chain">The DER certificates, the server's own first, sent in this order.</param>
    /// <param name="key">The leaf's private key: RSA of 2048 bits or more, or ECDSA on P-256 or P-384.</param>
    /// <exception cref="ArgumentException">The chain is empty, or the key is another kind or size.</exception>
    public DtlsServerCertificate(IReadOnlyList<byte[]> chain, AsymmetricAlgorithm key)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(key);
        if (chain.Count == 0)
        {
            throw new ArgumentException("A certificate chain holds at least the server's own certificate.", nameof(chain));
        }

        KeyAlgorithm = key switch
        {
            RSA { KeySize: >= MinimumRsaKeySize } => DtlsCertificateKeyAlgorithm.Rsa,
            ECDsa { KeySize: 256 or 384 } => DtlsCertificateKeyAlgorithm.Ecdsa,
            _ => throw new ArgumentException(
                "The certificate key must be RSA of 2048 bits or more, or ECDSA on P-256 or P-384.", nameof(key)),
        };
        Chain = chain;
        this.key = key;
    }

    /// <summary>The DER certificates, the server's own first.</summary>
    public IReadOnlyList<byte[]> Chain { get; }

    /// <summary>The kind of key, which decides the live suites.</summary>
    public DtlsCertificateKeyAlgorithm KeyAlgorithm { get; }

    /// <summary>
    /// The key's DER <c>SubjectPublicKeyInfo</c>, which a RawPublicKey <c>Certificate</c> carries
    /// alone (RFC 7250 section 3).
    /// </summary>
    internal byte[] SubjectPublicKeyInfo => key.ExportSubjectPublicKeyInfo();

    /// <summary>
    /// The first scheme in the client's order that the key can sign with, or <see langword="null"/>.
    /// </summary>
    /// <param name="clientSchemes">The client's <c>signature_algorithms</c>, in its order.</param>
    /// <returns>The scheme, or <see langword="null"/> when the client offers none the key can make.</returns>
    internal DtlsSignatureScheme? ChooseScheme(IReadOnlyList<ushort> clientSchemes)
    {
        foreach (var offered in clientSchemes)
        {
            var scheme = (DtlsSignatureScheme)offered;
            if (CanSign(scheme))
            {
                return scheme;
            }
        }

        return null;
    }

    /// <summary>Signs <paramref name="content"/> under <paramref name="scheme"/>.</summary>
    /// <param name="scheme">A scheme <see cref="ChooseScheme"/> chose.</param>
    /// <param name="content">The bytes to sign.</param>
    /// <returns>The signature; for ECDSA, the DER <c>ECDSA-Sig-Value</c> RFC 8422 section 5.4 names.</returns>
    internal byte[] Sign(DtlsSignatureScheme scheme, byte[] content)
    {
        var hash = scheme is DtlsSignatureScheme.EcdsaSecp384r1Sha384
            or DtlsSignatureScheme.RsaPkcs1Sha384 or DtlsSignatureScheme.RsaPssRsaeSha384
            ? HashAlgorithmName.SHA384
            : HashAlgorithmName.SHA256;
        if (key is RSA rsa)
        {
            var padding = scheme is DtlsSignatureScheme.RsaPkcs1Sha256 or DtlsSignatureScheme.RsaPkcs1Sha384
                ? RSASignaturePadding.Pkcs1
                : RSASignaturePadding.Pss;
            return rsa.SignData(content, hash, padding);
        }

        return ((ECDsa)key).SignData(content, hash, DSASignatureFormat.Rfc3279DerSequence);
    }

    private bool CanSign(DtlsSignatureScheme scheme) => key switch
    {
        ECDsa { KeySize: 256 } => scheme == DtlsSignatureScheme.EcdsaSecp256r1Sha256,
        ECDsa => scheme == DtlsSignatureScheme.EcdsaSecp384r1Sha384,
        _ => scheme is DtlsSignatureScheme.RsaPssRsaeSha256 or DtlsSignatureScheme.RsaPssRsaeSha384
            or DtlsSignatureScheme.RsaPkcs1Sha256 or DtlsSignatureScheme.RsaPkcs1Sha384,
    };
}
