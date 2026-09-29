using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

/// <summary>
/// The server certificate, with its exportable private key, and its intermediates, as
/// <see cref="ServerCertificateFileLoader"/> read them: what <see cref="ServerTlsSettings"/> takes.
/// </summary>
public sealed class LoadedServerCertificate : IDisposable
{
    internal LoadedServerCertificate(X509Certificate2 certificate, IReadOnlyList<X509Certificate2> intermediateCertificates)
    {
        Certificate = certificate;
        IntermediateCertificates = intermediateCertificates;
    }

    /// <summary>
    /// The server certificate, with its private key.
    /// </summary>
    public X509Certificate2 Certificate { get; }

    /// <summary>
    /// The intermediates that followed it in the file, in file order; empty for none.
    /// </summary>
    public IReadOnlyList<X509Certificate2> IntermediateCertificates { get; }

    /// <summary>
    /// Disposes the certificate and every intermediate.
    /// </summary>
    public void Dispose()
    {
        Certificate.Dispose();

        foreach (var intermediate in IntermediateCertificates)
        {
            intermediate.Dispose();
        }
    }
}
