using System.Security.Cryptography;

namespace Surl.TlsSrp;

/// <summary>
/// The certificate chain and private key the TLS-SRP server signs its ServerKeyExchange with for
/// RFC 5054's RSA and DSS suites (ADR-0122 decision 2). The caller reads both; this library reads no
/// file.
/// </summary>
public sealed class TlsSrpServerCertificate
{
    private static readonly TlsSrpCipherSuite[] RsaSuites =
        [TlsSrpCipherSuite.SrpShaRsa3DesEdeCbcSha, TlsSrpCipherSuite.SrpShaRsaAes128CbcSha, TlsSrpCipherSuite.SrpShaRsaAes256CbcSha];

    private static readonly TlsSrpCipherSuite[] DssSuites =
        [TlsSrpCipherSuite.SrpShaDss3DesEdeCbcSha, TlsSrpCipherSuite.SrpShaDssAes128CbcSha, TlsSrpCipherSuite.SrpShaDssAes256CbcSha];

    private static readonly TlsSignatureScheme[] RsaSchemes =
    [
        TlsSignatureScheme.RsaPkcs1Sha1, TlsSignatureScheme.RsaPkcs1Sha256, TlsSignatureScheme.RsaPkcs1Sha384,
        TlsSignatureScheme.RsaPssRsaeSha256, TlsSignatureScheme.RsaPssRsaeSha384,
    ];

    private static readonly TlsSignatureScheme[] DsaSchemes =
        [TlsSignatureScheme.DsaSha1, TlsSignatureScheme.DsaSha256, TlsSignatureScheme.DsaSha384];

    private readonly AsymmetricAlgorithm key;

    /// <summary>Pairs a certificate chain with its leaf's private key.</summary>
    /// <param name="chain">The DER certificates, the server's own first, sent in this order.</param>
    /// <param name="key">The leaf's private key: RSA or DSA.</param>
    /// <exception cref="ArgumentException">The chain is empty, or the key is neither RSA nor DSA.</exception>
    public TlsSrpServerCertificate(IReadOnlyList<byte[]> chain, AsymmetricAlgorithm key)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(key);
        if (chain.Count == 0)
        {
            throw new ArgumentException("A certificate chain holds at least the server's own certificate.", nameof(chain));
        }

        if (key is not (RSA or DSA))
        {
            throw new ArgumentException("The certificate key must be RSA or DSA.", nameof(key));
        }

        Chain = chain;
        this.key = key;
    }

    /// <summary>The DER certificates, the server's own first.</summary>
    public IReadOnlyList<byte[]> Chain { get; }

    /// <summary>The signed suites this key can serve: the three RSA ones for an RSA key, the three DSS ones for a DSA key.</summary>
    public IReadOnlyList<TlsSrpCipherSuite> SignedSuites => key is RSA ? RsaSuites : DssSuites;

    /// <summary>
    /// The first scheme in the client's order the key can make; with no <c>signature_algorithms</c>
    /// (<paramref name="clientSchemes"/> <see langword="null"/>) SHA-1 with the key's algorithm (RFC 5246 section 7.4.1.4.1).
    /// </summary>
    /// <param name="clientSchemes">The client's <c>signature_algorithms</c> in its order, or <see langword="null"/> when it sent none.</param>
    /// <returns>The scheme, or <see langword="null"/> when the client offers none the key can make.</returns>
    public TlsSignatureScheme? ChooseScheme(IReadOnlyList<ushort>? clientSchemes)
    {
        var made = key is RSA ? RsaSchemes : DsaSchemes;
        if (clientSchemes is null)
        {
            return made[0];
        }

        foreach (var offered in clientSchemes)
        {
            if (made.Contains((TlsSignatureScheme)offered))
            {
                return (TlsSignatureScheme)offered;
            }
        }

        return null;
    }

    /// <summary>Signs <paramref name="content"/> under <paramref name="scheme"/>.</summary>
    /// <param name="scheme">A scheme <see cref="ChooseScheme"/> chose.</param>
    /// <param name="content">The bytes to sign.</param>
    /// <returns>The signature; for DSA, the DER <c>Dss-Sig-Value</c> RFC 5246 section 4.7 names.</returns>
    public byte[] Sign(TlsSignatureScheme scheme, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var hash = HashOf(scheme);
        if (key is RSA rsa)
        {
            var padding = scheme is TlsSignatureScheme.RsaPssRsaeSha256 or TlsSignatureScheme.RsaPssRsaeSha384
                ? RSASignaturePadding.Pss
                : RSASignaturePadding.Pkcs1;
            return rsa.SignData(content, hash, padding);
        }

        // DSA signs a digest of the length of q, so the scheme's hash is cut here for every platform.
        var dsa = (DSA)key;
        var digest = CryptographicOperations.HashData(hash, content);
        var qLength = dsa.ExportParameters(false).Q!.Length;
        return dsa.CreateSignature(digest.AsSpan(0, Math.Min(digest.Length, qLength)).ToArray(), DSASignatureFormat.Rfc3279DerSequence);
    }

    private static HashAlgorithmName HashOf(TlsSignatureScheme scheme) => scheme switch
    {
        TlsSignatureScheme.RsaPkcs1Sha1 or TlsSignatureScheme.DsaSha1 => HashAlgorithmName.SHA1,
        TlsSignatureScheme.RsaPkcs1Sha384 or TlsSignatureScheme.DsaSha384 or TlsSignatureScheme.RsaPssRsaeSha384 => HashAlgorithmName.SHA384,
        _ => HashAlgorithmName.SHA256,
    };
}
