using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Composition;

/// <summary>
/// The client-certificate trust check <c>--http3</c> gives QUIC's TLS 1.3 handshake with
/// <c>--cacert</c> (BL-741), judged as the TCP listener judges one (ADR-0010 section 5): the
/// end-entity certificate must chain to exactly the <c>--cacert</c> anchors, never the operating
/// system's store, with no revocation check, no certificate download, the verification time from the
/// <see cref="TimeProvider"/>, and the client-authentication extended key usage required when the
/// certificate carries one. As on TCP, only the end-entity certificate is judged; intermediates the
/// client sends are not used.
/// </summary>
/// <param name="trustAnchors">The <c>--cacert</c> anchors; at least one.</param>
/// <param name="timeProvider">Supplies the verification time.</param>
internal sealed class QuicClientCertificateTrust(IReadOnlyList<X509Certificate2> trustAnchors, TimeProvider timeProvider)
{
    private static readonly Oid ClientAuthentication = new("1.3.6.1.5.5.7.3.2", "Client Authentication");

    /// <summary>
    /// Whether the chain's end-entity certificate chains to a trust anchor. The handshake has already
    /// loaded it as an X.509 certificate before it asks.
    /// </summary>
    /// <param name="chain">The client's certificate chain, DER, end-entity certificate first.</param>
    /// <returns><see langword="true"/> when it chains to a trust anchor.</returns>
    public bool Trusts(IReadOnlyList<ReadOnlyMemory<byte>> chain)
    {
        using var leaf = X509CertificateLoader.LoadCertificate(chain[0].Span);
        using var built = new X509Chain();
        built.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        built.ChainPolicy.CustomTrustStore.AddRange(trustAnchors.ToArray());
        built.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        built.ChainPolicy.DisableCertificateDownloads = true;
        built.ChainPolicy.VerificationTime = timeProvider.GetUtcNow().UtcDateTime;
        built.ChainPolicy.ApplicationPolicy.Add(ClientAuthentication);
        return built.Build(leaf);
    }
}
