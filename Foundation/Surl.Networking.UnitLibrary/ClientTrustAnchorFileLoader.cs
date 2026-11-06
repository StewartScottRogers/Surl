using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

/// <summary>
/// Reads the trust anchors that verify a client certificate from the file <c>--cacert</c>
/// names (ADR-0010, section 5): a PEM file of one or more <c>CERTIFICATE</c> blocks, or one
/// DER certificate.
/// </summary>
public static class ClientTrustAnchorFileLoader
{
    /// <summary>
    /// Loads the trust anchors.
    /// </summary>
    /// <param name="path">The <c>--cacert</c> file.</param>
    /// <returns>The certificates, in file order; never empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="TlsFileLoadException">
    /// <see cref="TlsFileLoadFailure.CaCertificateNotFound"/> when nothing exists at
    /// <paramref name="path"/>; <see cref="TlsFileLoadFailure.CaCertificateUnreadable"/> when it
    /// exists but holds no certificate Surl can read.
    /// </exception>
    public static IReadOnlyList<X509Certificate2> Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new TlsFileLoadException(
                TlsFileLoadFailure.CaCertificateNotFound, $"The --cacert file '{path}' does not exist.", null);
        }

        var fileBytes = TlsFileReading.ReadAllBytes(path, TlsFileLoadFailure.CaCertificateUnreadable, "--cacert");

        try
        {
            return CertificatesIn(fileBytes);
        }
        catch (CryptographicException exception)
        {
            throw Unreadable(path, exception);
        }
    }

    private static List<X509Certificate2> CertificatesIn(byte[] fileBytes)
    {
        var certificates = TlsFileReading.PemBlocksIn(fileBytes)
            .Where(block => block.Label == TlsFileReading.CertificateLabel)
            .Select(block => X509CertificateLoader.LoadCertificate(block.Der))
            .ToList();

        if (certificates.Count == 0 && TlsFileReading.IsOneDerValue(fileBytes))
        {
            certificates.Add(X509CertificateLoader.LoadCertificate(fileBytes));
        }

        return certificates.Count == 0 ? throw new CryptographicException("No certificate found.") : certificates;
    }

    private static TlsFileLoadException Unreadable(string path, Exception innerException) =>
        new(TlsFileLoadFailure.CaCertificateUnreadable, $"The --cacert file '{path}' holds no certificate Surl can read.", innerException);
}
