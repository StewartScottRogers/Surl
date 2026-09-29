using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

/// <summary>
/// A directory under <see cref="Path.GetTempPath"/> for the certificate and key files one test
/// writes (ADR-0010, section 6), deleted when the test disposes it; and the keys and
/// certificates those tests write, made at test time.
/// </summary>
internal sealed class TemporaryTlsFiles : IDisposable
{
    private static readonly PbeParameters Encryption = new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000);

    public TemporaryTlsFiles()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "surl-tls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }

    public string Write(string name, string text)
    {
        var path = Path.Combine(DirectoryPath, name);
        File.WriteAllText(path, text);

        return path;
    }

    public string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(DirectoryPath, name);
        File.WriteAllBytes(path, bytes);

        return path;
    }

    public string PathOf(string name) => Path.Combine(DirectoryPath, name);

    public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);

    /// <summary>A self-signed server certificate for <paramref name="key"/>, without its private key.</summary>
    public static X509Certificate2 CreateCertificate(AsymmetricAlgorithm key, string subject = "CN=localhost")
    {
        var request = key is RSA rsa
            ? new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            : new CertificateRequest(subject, (ECDsa)key, HashAlgorithmName.SHA256);
        using var withKey = request.CreateSelfSigned(TestCertificates.Now.AddDays(-1), TestCertificates.Now.AddDays(1));

        return X509CertificateLoader.LoadCertificate(withKey.RawData);
    }

    /// <summary>
    /// A certificate whose subject public key info is <paramref name="subjectPublicKeyInfo"/>,
    /// signed by a throwaway RSA key: for key types this platform cannot generate.
    /// </summary>
    public static X509Certificate2 CreateCertificateFor(byte[] subjectPublicKeyInfo)
    {
        using var signer = RSA.Create(2048);
        var publicKey = PublicKey.CreateFromSubjectPublicKeyInfo(subjectPublicKeyInfo, out _);
        var request = new CertificateRequest(new X500DistinguishedName("CN=localhost"), publicKey, HashAlgorithmName.SHA256);

        return request.Create(
            new X500DistinguishedName("CN=signer"),
            X509SignatureGenerator.CreateForRSA(signer, RSASignaturePadding.Pkcs1),
            TestCertificates.Now.AddDays(-1),
            TestCertificates.Now.AddDays(1),
            [1, 2, 3, 4]);
    }

    /// <summary>An Ed25519 subject public key info (RFC 8410).</summary>
    public static byte[] Ed25519SubjectPublicKeyInfo() =>
        [.. Convert.FromHexString("302A300506032B6570032100"), .. RandomNumberGenerator.GetBytes(32)];

    /// <summary>An Ed25519 PKCS#8 private key (RFC 8410).</summary>
    public static byte[] Ed25519Pkcs8PrivateKey() =>
        [.. Convert.FromHexString("302E020100300506032B657004220420"), .. RandomNumberGenerator.GetBytes(32)];

    /// <summary>An EC subject public key info on brainpoolP256r1, a curve Surl does not serve.</summary>
    public static byte[] BrainpoolSubjectPublicKeyInfo()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);

        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier("1.2.840.10045.2.1");
                writer.WriteObjectIdentifier("1.3.36.3.3.2.8.1.1.7");
            }

            writer.WriteBitString([0x04, .. RandomNumberGenerator.GetBytes(64)]);
        }

        return writer.Encode();
    }

    public static string EncryptedPkcs8Pem(AsymmetricAlgorithm key, string passphrase) =>
        key.ExportEncryptedPkcs8PrivateKeyPem(passphrase, Encryption);

    public static byte[] EncryptedPkcs8Der(AsymmetricAlgorithm key, string passphrase) =>
        key.ExportEncryptedPkcs8PrivateKey(passphrase, Encryption);
}
