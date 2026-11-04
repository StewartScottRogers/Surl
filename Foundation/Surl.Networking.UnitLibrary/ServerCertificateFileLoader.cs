using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

/// <summary>
/// Reads the server certificate, its intermediates and its private key from the files
/// <c>--cert</c> and <c>--key</c> name, in the formats <c>--cert-type</c> and <c>--key-type</c>
/// name, with <c>--pass</c> (ADR-0010, section 3). Every failure is a
/// <see cref="TlsFileLoadException"/> with <see cref="TlsFileLoadFailure.ServerCertificateUnusable"/>.
/// </summary>
public static class ServerCertificateFileLoader
{
    /// <summary>
    /// Loads the server certificate.
    /// </summary>
    /// <param name="certificatePath">The <c>--cert</c> file.</param>
    /// <param name="certificateFormat">Its format, from <c>--cert-type</c>.</param>
    /// <param name="keyPath">
    /// The <c>--key</c> file, or <see langword="null"/> for the key in a PEM <c>--cert</c> file.
    /// Required for <see cref="ServerCertificateFormat.Der"/>; never given with <see cref="ServerCertificateFormat.P12"/>.
    /// </param>
    /// <param name="keyFormat">The format of <paramref name="keyPath"/>, from <c>--key-type</c>.</param>
    /// <param name="passphrase">The <c>--pass</c> phrase, or <see langword="null"/>.</param>
    /// <returns>The certificate with an exportable private key, and its intermediates in file order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="certificatePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="keyPath"/> is given with <see cref="ServerCertificateFormat.P12"/>, which the command line refuses first.</exception>
    /// <exception cref="TlsFileLoadException">A file cannot be read, is not in its format, or holds a key Surl cannot serve.</exception>
    public static LoadedServerCertificate Load(
        string certificatePath,
        ServerCertificateFormat certificateFormat,
        string? keyPath,
        ServerKeyFormat keyFormat,
        string? passphrase)
    {
        ArgumentNullException.ThrowIfNull(certificatePath);

        if (certificateFormat == ServerCertificateFormat.P12 && keyPath is not null)
        {
            throw new ArgumentException("--key is refused with --cert-type P12.", nameof(keyPath));
        }

        var fileBytes = TlsFileReading.ReadAllBytes(certificatePath, TlsFileLoadFailure.ServerCertificateUnusable, "--cert");

        try
        {
            return certificateFormat switch
            {
                ServerCertificateFormat.P12 => LoadP12(fileBytes, certificatePath, passphrase),
                ServerCertificateFormat.Der => LoadDer(fileBytes, certificatePath, keyPath, keyFormat, passphrase),
                _ => LoadPem(fileBytes, certificatePath, keyPath, keyFormat, passphrase),
            };
        }
        catch (CryptographicException exception)
        {
            throw Unusable($"'{certificatePath}' is not a {certificateFormat} certificate Surl can read.", exception);
        }
    }

    private static LoadedServerCertificate LoadPem(
        byte[] fileBytes, string certificatePath, string? keyPath, ServerKeyFormat keyFormat, string? passphrase)
    {
        var blocks = TlsFileReading.PemBlocksIn(fileBytes);
        var certificates = blocks
            .Where(block => block.Label == TlsFileReading.CertificateLabel)
            .Select(block => X509CertificateLoader.LoadCertificate(block.Der))
            .ToList();

        if (certificates.Count == 0)
        {
            throw Unusable($"'{certificatePath}' holds no PEM certificate.", null);
        }

        var keyBlock = keyPath is null
            ? ServerPrivateKeyImport.KeyBlockIn(blocks, certificatePath)
            : ReadKeyBlock(keyPath, keyFormat);

        return JoinKey(certificates, keyBlock, passphrase);
    }

    private static LoadedServerCertificate LoadDer(
        byte[] fileBytes, string certificatePath, string? keyPath, ServerKeyFormat keyFormat, string? passphrase)
    {
        if (!TlsFileReading.IsOneDerValue(fileBytes))
        {
            throw Unusable($"'{certificatePath}' is not one DER certificate.", null);
        }

        var certificate = X509CertificateLoader.LoadCertificate(fileBytes);

        if (keyPath is null)
        {
            certificate.Dispose();
            throw Unusable($"A DER --cert '{certificatePath}' needs --key.", null);
        }

        return JoinKey([certificate], ReadKeyBlock(keyPath, keyFormat), passphrase);
    }

    private static LoadedServerCertificate LoadP12(byte[] fileBytes, string certificatePath, string? passphrase)
    {
        var certificates = X509CertificateLoader.LoadPkcs12Collection(
            fileBytes,
            passphrase,
            ServerCertificateImport.KeyStorageFlagsFor(OperatingSystem.IsLinux()) | X509KeyStorageFlags.Exportable);
        var certificate = certificates.FirstOrDefault(candidate => candidate.HasPrivateKey);

        if (certificate is null)
        {
            DisposeAll(certificates);
            throw Unusable($"'{certificatePath}' holds no certificate with its private key.", null);
        }

        RequireServableKey(certificate, certificates);

        return new LoadedServerCertificate(certificate, [.. certificates.Where(other => other != certificate)]);
    }

    private static PemBlock ReadKeyBlock(string keyPath, ServerKeyFormat keyFormat)
    {
        var fileBytes = TlsFileReading.ReadAllBytes(keyPath, TlsFileLoadFailure.ServerCertificateUnusable, "--key");

        return keyFormat == ServerKeyFormat.Der
            ? ServerPrivateKeyImport.DerKeyBlock(fileBytes, keyPath)
            : ServerPrivateKeyImport.KeyBlockIn(TlsFileReading.PemBlocksIn(fileBytes), keyPath);
    }

    private static LoadedServerCertificate JoinKey(List<X509Certificate2> certificates, PemBlock keyBlock, string? passphrase)
    {
        RequireServableKey(certificates[0], certificates);

        try
        {
            var withKey = ServerPrivateKeyImport.JoinKey(certificates[0], keyBlock, passphrase);

            return new LoadedServerCertificate(withKey, certificates[1..]);
        }
        catch (TlsFileLoadException)
        {
            DisposeAll(certificates[1..]);
            throw;
        }
        finally
        {
            certificates[0].Dispose();
        }
    }

    private static void RequireServableKey(X509Certificate2 certificate, IEnumerable<X509Certificate2> loaded)
    {
        try
        {
            ServerPrivateKeyImport.RequireServableKey(certificate);
        }
        catch (TlsFileLoadException)
        {
            DisposeAll(loaded);
            throw;
        }
    }

    private static void DisposeAll(IEnumerable<X509Certificate2> certificates)
    {
        foreach (var certificate in certificates)
        {
            certificate.Dispose();
        }
    }

    private static TlsFileLoadException Unusable(string message, Exception? innerException) =>
        new(TlsFileLoadFailure.ServerCertificateUnusable, message, innerException);
}
