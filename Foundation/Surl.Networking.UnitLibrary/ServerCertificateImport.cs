using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

/// <summary>
/// Makes a server certificate one <see cref="System.Net.Security.SslStream"/> can serve on
/// every platform (ADR-0010, section 6): it exports the certificate and key to PKCS#12 in
/// memory and imports that again, with an ephemeral key on Linux and the default key storage
/// on Windows (Schannel cannot serve an ephemeral key) and macOS (which has no ephemeral key set).
/// </summary>
internal static class ServerCertificateImport
{
    /// <summary>
    /// Returns a copy of <paramref name="certificate"/> whose key the platform can serve with.
    /// </summary>
    /// <param name="certificate">The certificate, with its private key.</param>
    /// <param name="isLinux">Whether the process runs on Linux.</param>
    /// <returns>A new certificate the caller owns and disposes.</returns>
    public static X509Certificate2 ReimportForServing(X509Certificate2 certificate, bool isLinux) =>
        X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), password: null, KeyStorageFlagsFor(isLinux));

    /// <summary>
    /// The key storage the re-import uses: <see cref="X509KeyStorageFlags.EphemeralKeySet"/> on
    /// Linux, <see cref="X509KeyStorageFlags.DefaultKeySet"/> elsewhere.
    /// </summary>
    /// <param name="isLinux">Whether the process runs on Linux.</param>
    /// <returns>The flags to import with.</returns>
    public static X509KeyStorageFlags KeyStorageFlagsFor(bool isLinux) =>
        isLinux ? X509KeyStorageFlags.EphemeralKeySet : X509KeyStorageFlags.DefaultKeySet;
}
