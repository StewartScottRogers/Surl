namespace Surl.Composition;

/// <summary>
/// Holds the certificate QUIC's TLS 1.3 serves, for the servers built before it exists: the
/// composition builds its protocol servers first and the TLS composition after, so a QUIC server
/// reads the certificate from here when a connection arrives (BL-461). One source belongs to one
/// composed instance, so instances side by side never share a certificate.
/// </summary>
public sealed class QuicCertificateSource
{
    /// <summary>Gets the certificate, or <see langword="null"/> until the composition has made it and when none is served.</summary>
    public QuicServerCertificate? Certificate { get; internal set; }

    /// <summary>Gets the <c>--cacert</c> anchors a <c>coaps</c> client certificate must chain to, empty until the composition has loaded them.</summary>
    public IReadOnlyList<System.Security.Cryptography.X509Certificates.X509Certificate2> ClientTrustAnchors { get; internal set; } = [];
}
