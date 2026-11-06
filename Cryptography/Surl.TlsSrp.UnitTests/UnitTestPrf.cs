using System.Security.Cryptography;
using System.Text;

namespace Surl.TlsSrp;

/// <summary>
/// RFC 5246 section 5's formulas written out by hand for the tests, independently of
/// <see cref="TlsPseudorandomFunction"/>: <c>A(0) = seed</c>, <c>A(i) = HMAC(secret, A(i-1))</c>,
/// <c>P_SHA256 = HMAC(secret, A(1) + seed) + HMAC(secret, A(2) + seed) + ...</c>, and
/// <c>PRF(secret, label, seed) = P_SHA256(secret, label + seed)</c>.
/// </summary>
internal static class UnitTestPrf
{
    public static byte[] Prf(byte[] secret, string label, byte[] seed, int length)
    {
        var labelAndSeed = Encoding.ASCII.GetBytes(label).Concat(seed).ToArray();
        using var hmac = new HMACSHA256(secret);
        var output = new List<byte>();
        var a = labelAndSeed;
        while (output.Count < length)
        {
            a = hmac.ComputeHash(a);
            output.AddRange(hmac.ComputeHash(a.Concat(labelAndSeed).ToArray()));
        }

        return [.. output.Take(length)];
    }
}
