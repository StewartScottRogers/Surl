using System.Numerics;

namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshModpGroupTests
{
    // RFC 3526: p = 2^n - 2^(n-64) - 1 + 2^64 * { [2^(n-130) pi] + k }, generator 2.
    [TestMethod]
    [DataRow(14, 2048, 124476)]
    [DataRow(15, 3072, 1690314)]
    [DataRow(16, 4096, 240904)]
    [DataRow(17, 6144, 929484)]
    [DataRow(18, 8192, 4743158)]
    public void Prime_IsRfc3526sFormulaOverPi(int groupNumber, int bits, int k)
    {
        var group = SshModpGroup.All[groupNumber - 14];

        var expected = BigInteger.Pow(2, bits) - BigInteger.Pow(2, bits - 64) - 1
            + (BigInteger.Pow(2, 64) * (PiTimesPowerOfTwo(bits - 130) + k));

        Assert.AreEqual(expected, group.Prime);
        Assert.AreEqual(bits, group.Bits);
        Assert.AreEqual(bits, (int)group.Prime.GetBitLength());
        Assert.AreEqual(new BigInteger(2), group.Generator);
    }

    [TestMethod]
    [DataRow(2048u, 3072u, 8192u, 3072, DisplayName = "libssh2-like range: the preferred size")]
    [DataRow(2048u, 2048u, 2048u, 2048, DisplayName = "Exactly 2048")]
    [DataRow(1024u, 2048u, 2048u, 2048, DisplayName = "Minimum below every group")]
    [DataRow(3000u, 3500u, 5000u, 4096, DisplayName = "The smallest at least the preferred size")]
    [DataRow(2048u, 9000u, 8192u, 8192, DisplayName = "Preferred above every group: the largest within")]
    [DataRow(2048u, 5000u, 4096u, 4096, DisplayName = "Preferred above the maximum: the largest within")]
    public void ForGroupExchange_PicksTheSmallestAtLeastThePreferredSizeElseTheLargest(uint min, uint preferred, uint max, int bits)
    {
        Assert.AreEqual(bits, SshModpGroup.ForGroupExchange(min, preferred, max)!.Bits);
    }

    [TestMethod]
    [DataRow(8193u, 8193u, 9000u)]
    [DataRow(1024u, 1024u, 2047u)]
    [DataRow(4097u, 5000u, 6143u)]
    public void ForGroupExchange_NoGroupWithinTheRange_IsNull(uint min, uint preferred, uint max)
    {
        Assert.IsNull(SshModpGroup.ForGroupExchange(min, preferred, max));
    }

    // floor(pi * 2^exponent), from Machin's formula pi = 16 atan(1/5) - 4 atan(1/239) in
    // fixed point with 64 guard bits.
    private static BigInteger PiTimesPowerOfTwo(int exponent)
    {
        const int GuardBits = 64;
        var one = BigInteger.One << (exponent + GuardBits);
        var pi = (16 * ArcTangentOfInverse(5, one)) - (4 * ArcTangentOfInverse(239, one));

        return pi >> GuardBits;
    }

    private static BigInteger ArcTangentOfInverse(int x, BigInteger one)
    {
        var sum = BigInteger.Zero;
        var power = one / x;
        var squared = x * x;
        for (var n = 1; !power.IsZero; n += 2)
        {
            sum += (n % 4 == 1 ? power : -power) / n;
            power /= squared;
        }

        return sum;
    }
}
