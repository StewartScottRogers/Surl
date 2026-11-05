using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Composition;

/// <summary>
/// The server certificate as the hand-built TLS 1.3 of QUIC serves it (ADR-0082, decision 3): the
/// certificate and its intermediates as DER, leaf first, and the certificate's private key.
/// </summary>
/// <param name="Chain">The certificate, then its intermediates, each DER-encoded.</param>
/// <param name="Key">The certificate's private key, ECDSA or RSA.</param>
public sealed record QuicServerCertificate(IReadOnlyList<ReadOnlyMemory<byte>> Chain, AsymmetricAlgorithm Key)
{
    /// <summary>
    /// Reads <paramref name="certificate"/>'s private key and the DER of it and its intermediates.
    /// </summary>
    /// <param name="certificate">The served certificate, with its private key.</param>
    /// <param name="intermediateCertificates">Its intermediates, in the order they are sent.</param>
    /// <returns>The certificate; the caller disposes its <see cref="Key"/>.</returns>
    /// <exception cref="ArgumentException">The certificate's key is neither ECDSA nor RSA.</exception>
    public static QuicServerCertificate From(X509Certificate2 certificate, IReadOnlyList<X509Certificate2> intermediateCertificates)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(intermediateCertificates);

        AsymmetricAlgorithm key = certificate.GetECDsaPrivateKey() as AsymmetricAlgorithm
            ?? certificate.GetRSAPrivateKey()
            ?? throw new ArgumentException("The certificate's key is neither ECDSA nor RSA.", nameof(certificate));
        return new QuicServerCertificate(
            [certificate.RawData, .. intermediateCertificates.Select(intermediate => (ReadOnlyMemory<byte>)intermediate.RawData)], key);
    }
}
