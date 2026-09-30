using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// A DSA host key, served only with <c>--allow-weak-ssh-algorithms</c>: an <c>ssh-dss</c> blob
/// (<c>string "ssh-dss"</c>, <c>mpint p</c>, <c>mpint q</c>, <c>mpint g</c>, <c>mpint y</c>)
/// that signs as <c>ssh-dss</c> with SHA-1, the signature the 40 bytes of r and s, each a
/// 160-bit unsigned integer (RFC 4253, section 6.6).
/// </summary>
internal sealed class SshDsaHostKey : SshHostKey
{
    /// <summary>The key type, which is also its signature algorithm's name.</summary>
    public const string DsaKeyType = "ssh-dss";

    /// <summary>The length of q in bytes: 160 bits, the only size <c>ssh-dss</c>'s signature holds.</summary>
    public const int SubgroupLength = 20;

    private readonly DSAParameters parameters;

    /// <summary>
    /// Creates the host key from a private key.
    /// </summary>
    /// <param name="parameters">The private key, with a 160-bit q.</param>
    public SshDsaHostKey(DSAParameters parameters)
        : base(DsaKeyType, Blob(parameters), [DsaKeyType])
    {
        this.parameters = parameters;
    }

    /// <inheritdoc/>
    private protected override byte[] SignRaw(string algorithm, byte[] data)
    {
        using var dsa = DSA.Create(parameters);

        return dsa.SignData(data, HashAlgorithmName.SHA1, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static byte[] Blob(DSAParameters parameters)
    {
        var blob = new SshWireWriter();
        blob.WriteString(DsaKeyType);
        blob.WriteMpint(Unsigned(parameters.P!));
        blob.WriteMpint(Unsigned(parameters.Q!));
        blob.WriteMpint(Unsigned(parameters.G!));
        blob.WriteMpint(Unsigned(parameters.Y!));

        return blob.ToArray();
    }

    private static BigInteger Unsigned(byte[] bigEndian) => new(bigEndian, isUnsigned: true, isBigEndian: true);
}
