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
    internal override byte[] SignRaw(string algorithm, byte[] data)
    {
        // Signed by hand (FIPS 186-4, section 4.6), not by the platform's DSA: BL-1388, a
        // signature from the platform's DSA on macOS now and then failed to verify.
        var p = Unsigned(parameters.P!);
        var q = Unsigned(parameters.Q!);
        var hash = Unsigned(SHA1.HashData(data));
        var z = hash >> Math.Max(0, 8 * SHA1.HashSizeInBytes - (int)q.GetBitLength());
        var (r, s) = SignHash(p, q, Unsigned(parameters.G!), Unsigned(parameters.X!), z, () => RandomK(q));

        return [.. FixedLength(r), .. FixedLength(s)];
    }

    /// <summary>
    /// Signs a hash: the pair r and s of FIPS 186-4, section 4.6, drawing another k whenever
    /// either comes out zero.
    /// </summary>
    /// <param name="p">The prime.</param>
    /// <param name="q">The prime order of g, which must be prime (the inverse of k uses Fermat's little theorem).</param>
    /// <param name="g">The generator.</param>
    /// <param name="x">The private value.</param>
    /// <param name="z">The hash, cut to the length of q.</param>
    /// <param name="nextK">Draws a per-signature secret k, from 1 to q - 1.</param>
    /// <returns>r and s.</returns>
    internal static (BigInteger R, BigInteger S) SignHash(BigInteger p, BigInteger q, BigInteger g, BigInteger x, BigInteger z, Func<BigInteger> nextK)
    {
        BigInteger r;
        BigInteger s;
        do
        {
            var k = nextK();
            r = BigInteger.ModPow(g, k, p) % q;
            s = BigInteger.ModPow(k, q - 2, q) * (z + (x * r)) % q;
        }
        while (r.IsZero || s.IsZero);

        return (r, s);
    }

    private static BigInteger RandomK(BigInteger q) =>
        (Unsigned(RandomNumberGenerator.GetBytes(SubgroupLength + 8)) % (q - 1)) + 1;

    private static byte[] FixedLength(BigInteger value)
    {
        var bytes = new byte[SubgroupLength];
        value.TryWriteBytes(bytes.AsSpan(SubgroupLength - value.GetByteCount(isUnsigned: true)), out _, isUnsigned: true, isBigEndian: true);

        return bytes;
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
