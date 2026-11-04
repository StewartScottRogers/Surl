using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

/// <summary>
/// Joins a server certificate to the private key <c>--key</c> or the <c>--cert</c> file holds
/// (ADR-0010, section 3): PKCS#8, encrypted PKCS#8, PKCS#1 or SEC1, for an RSA key of 2048 bits
/// or more or an ECDSA key on P-256, P-384 or P-521. Every failure is
/// <see cref="TlsFileLoadFailure.ServerCertificateUnusable"/>.
/// </summary>
internal static class ServerPrivateKeyImport
{
    /// <summary>The PEM label of a PKCS#8 key.</summary>
    public const string Pkcs8Label = "PRIVATE KEY";

    /// <summary>The PEM label of an encrypted PKCS#8 key.</summary>
    public const string EncryptedPkcs8Label = "ENCRYPTED PRIVATE KEY";

    /// <summary>The PEM label of a PKCS#1 RSA key.</summary>
    public const string Pkcs1Label = "RSA PRIVATE KEY";

    /// <summary>The PEM label of a SEC1 EC key.</summary>
    public const string Sec1Label = "EC PRIVATE KEY";

    private const string RsaAlgorithm = "1.2.840.113549.1.1.1";
    private const string EcAlgorithm = "1.2.840.10045.2.1";
    private const int SmallestServedRsaKeySize = 2048;

    private static readonly string[] KeyLabels = [Pkcs8Label, EncryptedPkcs8Label, Pkcs1Label, Sec1Label];

    // The DER-encoded named-curve OIDs of P-256, P-384 and P-521, as a certificate's key parameters hold them.
    private static readonly string[] ServedCurveParameters = ["06082A8648CE3D030107", "06052B81040022", "06052B81040023"];

    /// <summary>
    /// The first private key block among <paramref name="blocks"/>.
    /// </summary>
    /// <param name="blocks">The file's PEM blocks.</param>
    /// <param name="path">The file, for the message.</param>
    /// <returns>The key block.</returns>
    /// <exception cref="TlsFileLoadException">No block is a private key.</exception>
    public static PemBlock KeyBlockIn(IReadOnlyList<PemBlock> blocks, string path) =>
        blocks.FirstOrDefault(block => KeyLabels.Contains(block.Label))
            ?? throw Unusable($"'{path}' holds no PEM private key.", null);

    /// <summary>
    /// A DER key file as the PEM block its structure matches: an encrypted PKCS#8 key when its
    /// outer sequence starts with a sequence, a PKCS#8 key otherwise.
    /// </summary>
    /// <param name="fileBytes">The key file's bytes.</param>
    /// <param name="path">The file, for the message.</param>
    /// <returns>The key as a block.</returns>
    /// <exception cref="TlsFileLoadException">The file is not a DER sequence.</exception>
    public static PemBlock DerKeyBlock(byte[] fileBytes, string path)
    {
        try
        {
            var firstField = new AsnReader(fileBytes, AsnEncodingRules.BER).ReadSequence().PeekTag();
            var label = firstField.HasSameClassAndValue(Asn1Tag.Sequence) ? EncryptedPkcs8Label : Pkcs8Label;

            return new PemBlock(label, fileBytes);
        }
        catch (AsnContentException exception)
        {
            throw Unusable($"'{path}' is not a DER private key.", exception);
        }
    }

    /// <summary>
    /// Refuses a certificate whose key Surl cannot serve: anything but RSA of 2048 bits or more
    /// and ECDSA on P-256, P-384 or P-521 (Ed25519 and Ed448 among them).
    /// </summary>
    /// <param name="certificate">The server certificate.</param>
    /// <exception cref="TlsFileLoadException">The key cannot be served.</exception>
    public static void RequireServableKey(X509Certificate2 certificate)
    {
        var algorithm = certificate.GetKeyAlgorithm();

        if (algorithm == RsaAlgorithm)
        {
            using var publicKey = certificate.GetRSAPublicKey()!;
            RequireThat(publicKey.KeySize >= SmallestServedRsaKeySize, $"An RSA key of {publicKey.KeySize} bits is too small to serve.");
        }
        else
        {
            RequireThat(algorithm == EcAlgorithm, $"A key of algorithm {algorithm} cannot be served.");
            RequireThat(
                ServedCurveParameters.Contains(certificate.GetKeyAlgorithmParametersString()),
                "An ECDSA key on a curve other than P-256, P-384 or P-521 cannot be served.");
        }
    }

    /// <summary>
    /// A copy of <paramref name="certificate"/> joined to the key in <paramref name="keyBlock"/>.
    /// </summary>
    /// <param name="certificate">The server certificate, already checked by <see cref="RequireServableKey"/>.</param>
    /// <param name="keyBlock">The private key.</param>
    /// <param name="passphrase">The <c>--pass</c> phrase, or <see langword="null"/>.</param>
    /// <returns>A new certificate with an exportable private key.</returns>
    /// <exception cref="TlsFileLoadException">The key cannot be read or does not match.</exception>
    public static X509Certificate2 JoinKey(X509Certificate2 certificate, PemBlock keyBlock, string? passphrase)
    {
        try
        {
            if (certificate.GetKeyAlgorithm() == RsaAlgorithm)
            {
                using var rsa = RSA.Create();
                ImportKey(rsa, keyBlock, passphrase);

                return certificate.CopyWithPrivateKey(rsa);
            }

            using var ecdsa = ECDsa.Create();
            ImportKey(ecdsa, keyBlock, passphrase);

            return certificate.CopyWithPrivateKey(ecdsa);
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            throw Unusable("The private key cannot be read or does not match the certificate.", exception);
        }
    }

    private static void ImportKey(AsymmetricAlgorithm key, PemBlock keyBlock, string? passphrase)
    {
        switch (keyBlock.Label)
        {
            case Pkcs8Label:
                key.ImportPkcs8PrivateKey(keyBlock.Der, out _);
                break;
            case EncryptedPkcs8Label:
                key.ImportEncryptedPkcs8PrivateKey(
                    passphrase ?? throw Unusable("The private key is encrypted and no --pass was given.", null),
                    keyBlock.Der,
                    out _);
                break;
            case Pkcs1Label:
                RequireThat(key is RSA, "An RSA private key does not match the certificate.");
                ((RSA)key).ImportRSAPrivateKey(keyBlock.Der, out _);
                break;
            default:
                RequireThat(key is ECDsa, "An EC private key does not match the certificate.");
                ((ECDsa)key).ImportECPrivateKey(keyBlock.Der, out _);
                break;
        }
    }

    private static void RequireThat(bool condition, string message)
    {
        if (!condition)
        {
            throw Unusable(message, null);
        }
    }

    private static TlsFileLoadException Unusable(string message, Exception? innerException) =>
        new(TlsFileLoadFailure.ServerCertificateUnusable, message, innerException);
}
