using System.Security.Cryptography;
using System.Text;

namespace Surl.Tls13;

/// <summary>
/// Signs a server's CertificateVerify (RFC 8446 section 4.4.3) with the certificate's key: an RSA
/// key with rsa_pss_rsae_sha256, an ECDSA key with the scheme of its curve (ADR-0082 decision 3).
/// </summary>
internal static class TlsCertificateSigner
{
    private static readonly byte[] ServerContextPrefix =
        [.. Enumerable.Repeat((byte)0x20, 64), .. Encoding.ASCII.GetBytes("TLS 1.3, server CertificateVerify"), 0];

    /// <summary>The scheme <paramref name="key"/> signs with.</summary>
    /// <exception cref="ArgumentException">The key is neither RSA nor ECDSA on P-256, P-384 or P-521.</exception>
    public static TlsSignatureScheme SchemeOf(AsymmetricAlgorithm key) => key switch
    {
        RSA => TlsSignatureScheme.RsaPssRsaeSha256,
        ECDsa { KeySize: 256 } => TlsSignatureScheme.EcdsaSecp256r1Sha256,
        ECDsa { KeySize: 384 } => TlsSignatureScheme.EcdsaSecp384r1Sha384,
        ECDsa { KeySize: 521 } => TlsSignatureScheme.EcdsaSecp521r1Sha512,
        _ => throw new ArgumentException(
            "The certificate key must be RSA, or ECDSA on P-256, P-384 or P-521.", nameof(key)),
    };

    /// <summary>The content a server's CertificateVerify signs: 64 spaces, the context string, a zero byte and the transcript hash.</summary>
    public static byte[] SignedContent(ReadOnlySpan<byte> transcriptHash) => [.. ServerContextPrefix, .. transcriptHash];

    /// <summary>Signs the CertificateVerify content for <paramref name="transcriptHash"/> and returns the message.</summary>
    public static CertificateVerify Sign(AsymmetricAlgorithm key, ReadOnlySpan<byte> transcriptHash)
    {
        var scheme = SchemeOf(key);
        var content = SignedContent(transcriptHash);
        var signature = key is RSA rsa
            ? rsa.SignData(content, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)
            : ((ECDsa)key).SignData(content, HashOf(scheme), DSASignatureFormat.Rfc3279DerSequence);
        return new CertificateVerify(scheme, signature);
    }

    private static HashAlgorithmName HashOf(TlsSignatureScheme scheme) => scheme switch
    {
        TlsSignatureScheme.EcdsaSecp256r1Sha256 => HashAlgorithmName.SHA256,
        TlsSignatureScheme.EcdsaSecp384r1Sha384 => HashAlgorithmName.SHA384,
        _ => HashAlgorithmName.SHA512,
    };
}
