using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshDsaHostKeyTests
{
    // p = 11, q = 5, g = 3, x = 2: g^3 mod 11 = 5, so k = 3 makes r zero; with z = 4, k = 1 makes s zero.
    private static readonly BigInteger P = 11;
    private static readonly BigInteger Q = 5;
    private static readonly BigInteger G = 3;
    private static readonly BigInteger X = 2;

    private static Func<BigInteger> Ks(params BigInteger[] ks)
    {
        var queue = new Queue<BigInteger>(ks);

        return queue.Dequeue;
    }

    [TestMethod]
    public void SignHash_FirstKGivesNonZeroRAndS_IsThatPair()
    {
        var (r, s) = SshDsaHostKey.SignHash(P, Q, G, X, 0, Ks(1));

        Assert.AreEqual(new BigInteger(3), r);
        Assert.AreEqual(BigInteger.One, s);
    }

    [TestMethod]
    public void SignHash_KMakesRZero_DrawsAnotherK()
    {
        var (r, s) = SshDsaHostKey.SignHash(P, Q, G, X, 0, Ks(3, 1));

        Assert.AreEqual(new BigInteger(3), r);
        Assert.AreEqual(BigInteger.One, s);
    }

    [TestMethod]
    public void SignHash_KMakesSZero_DrawsAnotherK()
    {
        var (r, s) = SshDsaHostKey.SignHash(P, Q, G, X, 4, Ks(1, 2));

        Assert.AreEqual(new BigInteger(4), r);
        Assert.AreEqual(BigInteger.One, s);
    }

    [TestMethod]
    public void Sign_PrivateValueWithLeadingZeroBytes_VerifiesEveryTime()
    {
        var dsa = SshTestKeys.Dsa1024.ExportParameters(false);
        var (p, q, g) = (Unsigned(dsa.P!), Unsigned(dsa.Q!), Unsigned(dsa.G!));
        var x = new BigInteger(12345);
        var key = SshHostKeyFile.FromDsa(p, q, g, null, x);
        var y = BigInteger.ModPow(g, x, p);
        var failures = 0;

        for (var run = 0; run < 2000; run++)
        {
            var data = new byte[] { (byte)run, (byte)(run >> 8) };
            var reader = new SshWireReader(key.Sign("ssh-dss", data));
            reader.ReadString();
            var raw = reader.ReadString().ToArray();
            var (r, s) = (Unsigned(raw[..20]), Unsigned(raw[20..]));
            var z = Unsigned(SHA1.HashData(data));
            var w = BigInteger.ModPow(s, q - 2, q);
            var v = BigInteger.ModPow(g, z * w % q, p) * BigInteger.ModPow(y, r * w % q, p) % p % q;
            failures += raw.Length == 40 && v == r ? 0 : 1;
        }

        Assert.AreEqual(0, failures);
    }

    private static BigInteger Unsigned(byte[] bigEndian) => new(bigEndian, isUnsigned: true, isBigEndian: true);
}
