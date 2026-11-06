using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Dtls;

/// <summary>
/// A client certificate the server asked for under <c>--cacert</c> (ADR-0087 decision 3, ADR-0010
/// section 5): what <c>CertificateRequest</c> asks for, the chain check with its alerts (ADR-0087
/// decision 10), and the check of the client's <c>CertificateVerify</c> under the leaf's key.
/// </summary>
internal sealed class DtlsClientCertificate : IDisposable
{
    private const byte RsaSign = 1;
    private const byte EcdsaSign = 64;

    private readonly AsymmetricAlgorithm key;

    private DtlsClientCertificate(X509Certificate2 leaf, AsymmetricAlgorithm key)
    {
        Leaf = leaf;
        this.key = key;
    }

    /// <summary>
    /// The <c>CertificateRequest</c> the server sends: <c>ecdsa_sign</c> and <c>rsa_sign</c>, the
    /// signature schemes of ADR-0087 decision 5, and an empty list of authorities.
    /// </summary>
    public static DtlsCertificateRequest Request { get; } = new(
        [EcdsaSign, RsaSign],
        [
            (ushort)DtlsSignatureScheme.EcdsaSecp256r1Sha256,
            (ushort)DtlsSignatureScheme.EcdsaSecp384r1Sha384,
            (ushort)DtlsSignatureScheme.RsaPssRsaeSha256,
            (ushort)DtlsSignatureScheme.RsaPssRsaeSha384,
            (ushort)DtlsSignatureScheme.RsaPkcs1Sha256,
            (ushort)DtlsSignatureScheme.RsaPkcs1Sha384,
        ],
        []);

    /// <summary>The client's own certificate.</summary>
    public X509Certificate2 Leaf { get; }

    /// <summary>
    /// Checks the client's chain against <paramref name="anchors"/> at <paramref name="now"/>.
    /// </summary>
    /// <param name="chain">The DER certificates of the client's <c>Certificate</c>, its own first.</param>
    /// <param name="anchors">The <c>--cacert</c> anchors.</param>
    /// <param name="now">The time on the session's <see cref="TimeProvider"/>.</param>
    /// <returns>The client certificate, whose key checks <c>CertificateVerify</c>.</returns>
    /// <exception cref="DtlsAlertException">
    /// <c>handshake_failure</c> for an empty list, <c>bad_certificate</c> for one that does not
    /// read, <c>unsupported_certificate</c> for a key neither RSA nor ECDSA,
    /// <c>certificate_expired</c> for one outside its validity, <c>unknown_ca</c> for one that does
    /// not chain to an anchor.
    /// </exception>
    public static DtlsClientCertificate Validate(IReadOnlyList<byte[]> chain, X509Certificate2Collection anchors, DateTimeOffset now)
    {
        if (chain.Count == 0)
        {
            throw new DtlsAlertException(DtlsAlertDescription.HandshakeFailure, "--cacert requires a client certificate and the client sent none.");
        }

        var certificates = Load(chain);
        try
        {
            ThrowUnlessChains(certificates, anchors, now);
            var key = (AsymmetricAlgorithm?)certificates[0].GetRSAPublicKey() ?? certificates[0].GetECDsaPublicKey()
                ?? throw new DtlsAlertException(
                    DtlsAlertDescription.UnsupportedCertificate, "The client certificate's key is neither RSA nor ECDSA.");
            var leaf = certificates[0];
            certificates.RemoveAt(0);
            return new DtlsClientCertificate(leaf, key);
        }
        finally
        {
            certificates.ForEach(certificate => certificate.Dispose());
        }
    }

    /// <summary>
    /// Whether <paramref name="verify"/> is the leaf key's signature over <paramref name="signedContent"/>
    /// under one of the schemes <see cref="Request"/> named, the scheme matching the key's kind.
    /// </summary>
    /// <param name="verify">The client's <c>CertificateVerify</c>.</param>
    /// <param name="signedContent">The handshake messages before it, as the handshake hash covers them.</param>
    /// <returns>Whether the signature verifies.</returns>
    public bool Verifies(DtlsCertificateVerify verify, byte[] signedContent)
    {
        var scheme = (DtlsSignatureScheme)verify.SignatureScheme;
        var hash = scheme is DtlsSignatureScheme.EcdsaSecp384r1Sha384 or DtlsSignatureScheme.RsaPkcs1Sha384 or DtlsSignatureScheme.RsaPssRsaeSha384
            ? HashAlgorithmName.SHA384
            : HashAlgorithmName.SHA256;
        return key is RSA rsa
            ? VerifiesRsa(rsa, scheme, hash, verify.Signature, signedContent)
            : VerifiesEcdsa((ECDsa)key, scheme, hash, verify.Signature, signedContent);
    }

    /// <summary>Disposes the leaf and its key.</summary>
    public void Dispose()
    {
        key.Dispose();
        Leaf.Dispose();
    }

    private static List<X509Certificate2> Load(IReadOnlyList<byte[]> chain)
    {
        var certificates = new List<X509Certificate2>();
        try
        {
            foreach (var der in chain)
            {
                certificates.Add(X509CertificateLoader.LoadCertificate(der));
            }

            return certificates;
        }
        catch (CryptographicException exception)
        {
            certificates.ForEach(certificate => certificate.Dispose());
            throw new DtlsAlertException(DtlsAlertDescription.BadCertificate, $"A client certificate does not read: {exception.Message}");
        }
    }

    private static void ThrowUnlessChains(List<X509Certificate2> certificates, X509Certificate2Collection anchors, DateTimeOffset now)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(anchors);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.VerificationTime = now.UtcDateTime;
        chain.ChainPolicy.ExtraStore.AddRange(certificates.Skip(1).ToArray());
        if (chain.Build(certificates[0]))
        {
            return;
        }

        if (chain.ChainStatus.Any(status => status.Status.HasFlag(X509ChainStatusFlags.NotTimeValid)))
        {
            throw new DtlsAlertException(
                DtlsAlertDescription.CertificateExpired, "A client certificate is expired or not yet valid.");
        }

        throw new DtlsAlertException(DtlsAlertDescription.UnknownCa, "The client certificate does not chain to a --cacert anchor.");
    }

    private static bool VerifiesRsa(RSA rsa, DtlsSignatureScheme scheme, HashAlgorithmName hash, byte[] signature, byte[] content) => scheme switch
    {
        DtlsSignatureScheme.RsaPkcs1Sha256 or DtlsSignatureScheme.RsaPkcs1Sha384 =>
            rsa.VerifyData(content, signature, hash, RSASignaturePadding.Pkcs1),
        DtlsSignatureScheme.RsaPssRsaeSha256 or DtlsSignatureScheme.RsaPssRsaeSha384 =>
            rsa.VerifyData(content, signature, hash, RSASignaturePadding.Pss),
        _ => false,
    };

    private static bool VerifiesEcdsa(ECDsa ecdsa, DtlsSignatureScheme scheme, HashAlgorithmName hash, byte[] signature, byte[] content) =>
        scheme is DtlsSignatureScheme.EcdsaSecp256r1Sha256 or DtlsSignatureScheme.EcdsaSecp384r1Sha384
        && ecdsa.VerifyData(content, signature, hash, DSASignatureFormat.Rfc3279DerSequence);
}
