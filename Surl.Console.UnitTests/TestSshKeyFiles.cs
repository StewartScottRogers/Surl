using System.Security.Cryptography;
using System.Text;

namespace Surl.Console;

/// <summary>
/// The bytes of SSH host key and authorized-keys files the start-up tests read through the
/// runner's file seam, each built in memory from BCL primitives and the formats ADR-0051
/// decisions 4 and 6 name.
/// </summary>
internal static class TestSshKeyFiles
{
    /// <summary>The passphrase <see cref="EncryptedRsa2048"/> is encrypted with.</summary>
    public const string Passphrase = "correct horse";

    // One RSA key, made and exported once: tests run in parallel, and an RSA object is not
    // safe to export from several threads at once.
    private static readonly Lazy<(byte[] Pkcs8, byte[] Pkcs1, byte[] EncryptedPkcs8)> Rsa2048Files = new(ExportRsa2048Files);

    /// <summary>An unencrypted PKCS #8 RSA 2048-bit key: a host key surl serves.</summary>
    public static byte[] Rsa2048 => Rsa2048Files.Value.Pkcs8;

    /// <summary>The key of <see cref="Rsa2048"/> written as PKCS #1: a second RSA host key file.</summary>
    public static byte[] Rsa2048Pkcs1 => Rsa2048Files.Value.Pkcs1;

    /// <summary>An unencrypted PKCS #8 ECDSA P-256 key.</summary>
    public static byte[] EcdsaP256
    {
        get
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            return Pem(ecdsa.ExportPkcs8PrivateKeyPem());
        }
    }

    /// <summary><see cref="Rsa2048"/> encrypted under <see cref="Passphrase"/> with PBES2.</summary>
    public static byte[] EncryptedRsa2048 => Rsa2048Files.Value.EncryptedPkcs8;

    /// <summary>An RSA 1024-bit key: too short without <c>--allow-weak-ssh-algorithms</c>.</summary>
    public static byte[] Rsa1024
    {
        get
        {
            using var rsa = RSA.Create(1024);
            return Pem(rsa.ExportPkcs8PrivateKeyPem());
        }
    }

    /// <summary>A PKCS #8 X25519 key (RFC 8410): a key type no SSH host key has.</summary>
    public static byte[] X25519 => Pem(PemEncoding.WriteString(
        "PRIVATE KEY",
        [0x30, 0x2E, 0x02, 0x01, 0x00, 0x30, 0x05, 0x06, 0x03, 0x2B, 0x65, 0x6E, 0x04, 0x22, 0x04, 0x20, .. new byte[32]]));

    /// <summary>An <c>openssh-key-v1</c> key encrypted with <c>aes256-ctr</c> under <c>bcrypt</c>.</summary>
    public static byte[] EncryptedOpenSsh => Pem(PemEncoding.WriteString(
        "OPENSSH PRIVATE KEY",
        [
            .. Encoding.ASCII.GetBytes("openssh-key-v1\0"),
            .. SshString("aes256-ctr"u8),
            .. SshString("bcrypt"u8),
            .. SshString([]),
            0, 0, 0, 1,
            .. SshString(SshString("ssh-rsa"u8)),
            .. SshString(new byte[16]),
        ]));

    /// <summary>Text that is no private key at all.</summary>
    public static byte[] NotAKey => "not a key\n"u8.ToArray();

    /// <summary>An <c>authorized_keys</c> line for an Ed25519 public key of 32 zero bytes.</summary>
    public static string Ed25519AuthorizedKeyLine =>
        "ssh-ed25519 " + Convert.ToBase64String([.. SshString("ssh-ed25519"u8), .. SshString(new byte[32])]) + " alice@example";

    private static (byte[] Pkcs8, byte[] Pkcs1, byte[] EncryptedPkcs8) ExportRsa2048Files()
    {
        using var rsa = RSA.Create(2048);
        return (
            Pem(rsa.ExportPkcs8PrivateKeyPem()),
            Pem(rsa.ExportRSAPrivateKeyPem()),
            Pem(rsa.ExportEncryptedPkcs8PrivateKeyPem(
                Passphrase, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000))));
    }

    private static byte[] Pem(string pem) => Encoding.ASCII.GetBytes(pem + "\n");

    private static byte[] SshString(ReadOnlySpan<byte> value) =>
        [(byte)(value.Length >> 24), (byte)(value.Length >> 16), (byte)(value.Length >> 8), (byte)value.Length, .. value];
}
