using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Makes the SSH keys the SSH conformance tests use, fresh for each test, with the base class
/// library only: host private keys in the PEM forms <c>--hostkey</c> reads (ADR-0051 decision 4),
/// and an RSA user key pair in the forms curl's <c>--key</c> and <c>--pubkey</c> read and an
/// <c>authorized_keys</c> line surl's <c>--authorized-keys</c> reads.
/// </summary>
internal static class SshTestKeys
{
    // RFC 8410 section 7: the PKCS #8 PrivateKeyInfo of an Ed25519 key, up to its 32-byte seed.
    private static readonly byte[] Ed25519Pkcs8Prefix = Convert.FromHexString("302E020100300506032B657004220420");

    /// <summary>Makes an RSA 2048-bit host key as PKCS #8 PEM.</summary>
    public static string RsaHostKeyPem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportPkcs8PrivateKeyPem();
    }

    /// <summary>Makes an ECDSA P-256 host key as PKCS #8 PEM.</summary>
    public static string EcdsaHostKeyPem()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return ecdsa.ExportPkcs8PrivateKeyPem();
    }

    /// <summary>Makes an Ed25519 host key as PKCS #8 PEM, from a random seed.</summary>
    public static string Ed25519HostKeyPem() =>
        PemEncoding.WriteString("PRIVATE KEY", [.. Ed25519Pkcs8Prefix, .. RandomNumberGenerator.GetBytes(32)]);

    /// <summary>
    /// Makes an RSA 2048-bit user key pair: the private key as PKCS #1 PEM, which every pinned
    /// build's libssh2 reads, and the public key as an OpenSSH one-line public key.
    /// </summary>
    public static (string PrivateKeyPem, string PublicKeyLine) RsaUserKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportRSAPrivateKeyPem(), PublicKeyLine(rsa));
    }

    /// <summary>
    /// Makes the OpenSSH one-line public key of a fresh RSA 2048-bit key, <c>ssh-rsa &lt;base64&gt;</c>,
    /// for a key the server does not hold.
    /// </summary>
    public static string OtherRsaPublicKeyLine()
    {
        using var rsa = RSA.Create(2048);
        return PublicKeyLine(rsa);
    }

    // RFC 4253 section 6.6: string "ssh-rsa", mpint e, mpint n.
    private static string PublicKeyLine(RSA rsa)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        using var blob = new MemoryStream();
        WriteString(blob, Encoding.ASCII.GetBytes("ssh-rsa"));
        WriteString(blob, AsMpint(parameters.Exponent!));
        WriteString(blob, AsMpint(parameters.Modulus!));
        return $"ssh-rsa {Convert.ToBase64String(blob.ToArray())} surl-conformance";
    }

    private static byte[] AsMpint(byte[] unsignedBigEndian) =>
        unsignedBigEndian[0] >= 0x80 ? [0, .. unsignedBigEndian] : unsignedBigEndian;

    private static void WriteString(MemoryStream blob, byte[] value)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)value.Length);
        blob.Write(length);
        blob.Write(value);
    }
}
