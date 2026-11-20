using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

/// <summary>
/// Verifies a client certificate against the trust anchors <c>--cacert</c> names (ADR-0010,
/// section 5): exactly those anchors, never the operating system's store, no revocation check,
/// no certificate download, the verification time from the <see cref="TimeProvider"/>, and the
/// client-authentication extended key usage required when the certificate carries one.
/// </summary>
internal sealed class ClientCertificateVerifier
{
    private static readonly Oid ClientAuthentication = new("1.3.6.1.5.5.7.3.2", "Client Authentication");

    private readonly IReadOnlyList<X509Certificate2> trustAnchors;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Creates a verifier that trusts exactly <paramref name="trustAnchors"/>.
    /// </summary>
    /// <param name="trustAnchors">The trust anchors; at least one.</param>
    /// <param name="timeProvider">Supplies the verification time.</param>
    public ClientCertificateVerifier(IReadOnlyList<X509Certificate2> trustAnchors, TimeProvider timeProvider)
    {
        this.trustAnchors = trustAnchors;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Whether <paramref name="certificate"/> chains to a trust anchor. A missing certificate is
    /// never trusted, because verification requires one.
    /// </summary>
    /// <param name="certificate">The client's certificate, or <see langword="null"/> when it sent none.</param>
    /// <returns><see langword="true"/> when the chain builds to a trust anchor.</returns>
    public bool IsTrusted(X509Certificate? certificate) =>
        certificate is not null && BuildsToAnchor(certificate.GetRawCertData(), []);

    /// <summary>
    /// Whether a certificate chain a hand-built TLS 1.3 handshake received chains to a trust anchor
    /// (ADR-0010, section 5): the first certificate is the client's, and the rest are intermediates
    /// that may be used to reach an anchor but are never trusted themselves. An empty chain is never
    /// trusted.
    /// </summary>
    /// <param name="chain">The DER certificates the client sent, its own first.</param>
    /// <returns><see langword="true"/> when the first certificate builds to a trust anchor.</returns>
    public bool IsChainTrusted(IReadOnlyList<ReadOnlyMemory<byte>> chain) =>
        chain.Count > 0 && BuildsToAnchor(chain[0].ToArray(), chain.Skip(1));

    private bool BuildsToAnchor(byte[] leafDer, IEnumerable<ReadOnlyMemory<byte>> intermediatesDer)
    {
        using var leaf = X509CertificateLoader.LoadCertificate(leafDer);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(trustAnchors.ToArray());
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.VerificationTime = timeProvider.GetUtcNow().UtcDateTime;
        chain.ChainPolicy.ApplicationPolicy.Add(ClientAuthentication);

        var intermediates = intermediatesDer.Select(der => X509CertificateLoader.LoadCertificate(der.Span)).ToArray();
        try
        {
            chain.ChainPolicy.ExtraStore.AddRange(intermediates);

            return chain.Build(leaf);
        }
        finally
        {
            foreach (var intermediate in intermediates)
            {
                intermediate.Dispose();
            }
        }
    }
    /// <summary>
    /// The <see cref="RemoteCertificateValidationCallback"/> a server handshake uses: it ignores
    /// the platform's own chain and errors and answers <see cref="IsTrusted"/>.
    /// </summary>
    /// <param name="sender">The <see cref="SslStream"/>; unused.</param>
    /// <param name="certificate">The client's certificate, or <see langword="null"/>.</param>
    /// <param name="chain">The platform's chain; unused.</param>
    /// <param name="sslPolicyErrors">The platform's verdict; unused.</param>
    /// <returns>Whether the handshake may go on.</returns>
    public bool Validate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
        IsTrusted(certificate);
}
