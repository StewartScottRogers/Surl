using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// An RSA host key: an <c>ssh-rsa</c> blob (<c>string "ssh-rsa"</c>, <c>mpint e</c>,
/// <c>mpint n</c>; RFC 4253 section 6.6) that signs with PKCS #1 v1.5 over SHA-512 or
/// SHA-256 as <c>rsa-sha2-512</c> or <c>rsa-sha2-256</c> (RFC 8332, section 3), or over SHA-1
/// as <c>ssh-rsa</c> (RFC 4253, section 6.6), which the offer lists only with
/// <c>--allow-weak-ssh-algorithms</c>.
/// </summary>
internal sealed class SshRsaHostKey : SshHostKey
{
    /// <summary>The key type, which is also the SHA-1 signature algorithm's name.</summary>
    public const string RsaKeyType = "ssh-rsa";

    private readonly RSAParameters parameters;

    /// <summary>
    /// Creates the host key from a private key.
    /// </summary>
    /// <param name="parameters">The private key.</param>
    public SshRsaHostKey(RSAParameters parameters)
        : base(RsaKeyType, Blob(parameters), ["rsa-sha2-512", "rsa-sha2-256", RsaKeyType])
    {
        this.parameters = parameters;
    }

    /// <summary>
    /// The number of bits in the modulus.
    /// </summary>
    /// <param name="parameters">The key.</param>
    /// <returns>The modulus's bit length.</returns>
    public static int ModulusBits(RSAParameters parameters) => (int)Unsigned(parameters.Modulus!).GetBitLength();

    /// <summary>
    /// The hash an RSA signature algorithm signs over: SHA-512 for <c>rsa-sha2-512</c>, SHA-256
    /// for <c>rsa-sha2-256</c>, SHA-1 for <c>ssh-rsa</c>.
    /// </summary>
    /// <param name="algorithm">One of the three.</param>
    /// <returns>The hash.</returns>
    public static HashAlgorithmName HashFor(string algorithm) => algorithm switch
    {
        "rsa-sha2-512" => HashAlgorithmName.SHA512,
        "rsa-sha2-256" => HashAlgorithmName.SHA256,
        _ => HashAlgorithmName.SHA1,
    };

    /// <inheritdoc/>
    private protected override byte[] SignRaw(string algorithm, byte[] data)
    {
        using var rsa = RSA.Create(parameters);

        return rsa.SignData(data, HashFor(algorithm), RSASignaturePadding.Pkcs1);
    }

    private static byte[] Blob(RSAParameters parameters)
    {
        var blob = new SshWireWriter();
        blob.WriteString(RsaKeyType);
        blob.WriteMpint(Unsigned(parameters.Exponent!));
        blob.WriteMpint(Unsigned(parameters.Modulus!));

        return blob.ToArray();
    }

    private static BigInteger Unsigned(byte[] bigEndian) => new(bigEndian, isUnsigned: true, isBigEndian: true);
}
