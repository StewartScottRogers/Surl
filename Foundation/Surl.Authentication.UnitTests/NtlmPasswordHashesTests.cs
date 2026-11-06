using System.Text;
using Surl.Cryptography;

namespace Surl.Authentication;

/// <summary>
/// <see cref="NtlmPasswordHashes"/>: the four NT hashes an account keeps, one per way a pinned
/// upstream curl build hashes the password (BL-321).
/// </summary>
[TestClass]
public sealed class NtlmPasswordHashesTests
{
    [TestMethod]
    public void Compute_AsciiPassword_IsTheSpecificationsHashFourTimes()
    {
        var hashes = NtlmPasswordHashes.Compute("Password");

        Assert.HasCount(NtlmPasswordHashes.Count, hashes);
        foreach (var hash in hashes)
        {
            CollectionAssert.AreEqual(Convert.FromHexString("A4F49C406510BDCAB6824EE7C30FD852"), hash);
        }
    }

    [TestMethod]
    public void Compute_NonAsciiPassword_IsEachBuildsHashInOrder()
    {
        var hashes = NtlmPasswordHashes.Compute("pässword");

        CollectionAssert.AreEqual(Md4.HashData(Encoding.Unicode.GetBytes("pässword")), hashes[0]);
        CollectionAssert.AreEqual(
            Md4.HashData([0x70, 0, 0xC3, 0, 0xA4, 0, .. Encoding.Unicode.GetBytes("ssword")]), hashes[1]);

        // Windows-1252 E4 read as code page 437 is U+03A3; UTF-8 C3 A4 is U+251C U+00F1.
        CollectionAssert.AreEqual(Md4.HashData(Encoding.Unicode.GetBytes("pΣssword")), hashes[2]);
        CollectionAssert.AreEqual(Md4.HashData(Encoding.Unicode.GetBytes("p├ñssword")), hashes[3]);
    }

    [TestMethod]
    public void ComputeRandom_Always_IsCountDifferentSixteenByteHashes()
    {
        var hashes = NtlmPasswordHashes.ComputeRandom();

        Assert.HasCount(NtlmPasswordHashes.Count, hashes);
        Assert.IsTrue(hashes.All(hash => hash.Length == 16));
        Assert.HasCount(NtlmPasswordHashes.Count, hashes.Select(Convert.ToHexString).Distinct());
    }
}
