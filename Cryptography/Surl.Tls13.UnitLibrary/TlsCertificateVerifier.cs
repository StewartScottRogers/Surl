using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Surl.Tls13;

/// <summary>
/// Verifies a client's CertificateVerify (RFC 8446 section 4.4.3) with the public key of the
/// client's end-entity certificate, for the schemes the server's CertificateRequest offers.
/// </summary>
internal static class TlsCertificateVerifier
{
    private static readonly byte[] ClientContextPrefix =
        [.. Enumerable.Repeat((byte)0x20, 64), .. Encoding.ASCII.GetBytes("TLS 1.3, client CertificateVerify"), 0];

    /// <summary>
    /// The schemes the CertificateRequest's signature_algorithms offers, in this order: ECDSA on
    /// P-256, P-384 and P-521, then RSASSA-PSS with SHA-256, SHA-384 and SHA-512 - every TLS 1.3
    /// CertificateVerify scheme the base class library verifies for an RSA or ECDSA certificate.
    /// </summary>
    public static IReadOnlyList<TlsSignatureScheme> ClientSchemes { get; } =
    [
        TlsSignatureScheme.EcdsaSecp256r1Sha256,
        TlsSignatureScheme.EcdsaSecp384r1Sha384,
        TlsSignatureScheme.EcdsaSecp521r1Sha512,
        TlsSignatureScheme.RsaPssRsaeSha256,
        TlsSignatureScheme.RsaPssRsaeSha384,
        TlsSignatureScheme.RsaPssRsaeSha512,
    ];

    /// <summary>The content a client's CertificateVerify signs: 64 spaces, the context string, a zero byte and the transcript hash.</summary>
    public static byte[] SignedContent(ReadOnlySpan<byte> transcriptHash) => [.. ClientContextPrefix, .. transcriptHash];

    /// <summary>
    /// Whether <paramref name="verify"/>'s signature, by one of <see cref="ClientSchemes"/>, verifies
    /// over <paramref name="transcriptHash"/> with <paramref name="certificate"/>'s public key. A key
    /// of the wrong type, or an ECDSA key on another curve than the scheme's, does not verify.
    /// </summary>
    public static bool Verifies(X509Certificate2 certificate, CertificateVerify verify, ReadOnlySpan<byte> transcriptHash)
    {
        var content = SignedContent(transcriptHash);
        var signature = verify.Signature.Span;
        return verify.Algorithm switch
        {
            TlsSignatureScheme.RsaPssRsaeSha256 => VerifiesRsaPss(certificate, content, signature, HashAlgorithmName.SHA256),
            TlsSignatureScheme.RsaPssRsaeSha384 => VerifiesRsaPss(certificate, content, signature, HashAlgorithmName.SHA384),
            TlsSignatureScheme.RsaPssRsaeSha512 => VerifiesRsaPss(certificate, content, signature, HashAlgorithmName.SHA512),
            TlsSignatureScheme.EcdsaSecp256r1Sha256 => VerifiesEcdsa(certificate, 256, content, signature, HashAlgorithmName.SHA256),
            TlsSignatureScheme.EcdsaSecp384r1Sha384 => VerifiesEcdsa(certificate, 384, content, signature, HashAlgorithmName.SHA384),
            _ => VerifiesEcdsa(certificate, 521, content, signature, HashAlgorithmName.SHA512),
        };
    }

    private static bool VerifiesRsaPss(X509Certificate2 certificate, byte[] content, ReadOnlySpan<byte> signature, HashAlgorithmName hash)
    {
        using var key = certificate.GetRSAPublicKey();
        return key is not null && key.VerifyData(content, signature, hash, RSASignaturePadding.Pss);
    }

    private static bool VerifiesEcdsa(
        X509Certificate2 certificate, int keySize, byte[] content, ReadOnlySpan<byte> signature, HashAlgorithmName hash)
    {
        using var key = certificate.GetECDsaPublicKey();
        return key?.KeySize == keySize && key.VerifyData(content, signature, hash, DSASignatureFormat.Rfc3279DerSequence);
    }
}
