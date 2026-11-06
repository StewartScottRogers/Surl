using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Cryptography.Srp;

/// <summary>
/// Checks each of RFC 5054 Appendix A's groups against the RFC: the bit length and generator it
/// prints, and the SHA-256 of the prime's bytes as the RFC prints them (computed from the RFC's
/// text with PowerShell's <c>Convert.FromHexString</c> and <c>SHA256.HashData</c>, 2026-11-05).
/// </summary>
[TestClass]
public sealed class SrpGroupTests
{
    [TestMethod]
    [DataRow(0, 1024, 2, "494B6A801B379F37C9EE25D5DB7CD70FFCFE53D01B7C9E4470EACA46BDA24B39")]
    [DataRow(1, 1536, 2, "72AF4A20E501A893B7DC85F4EFAC51845AB21C102D1E73F7000EC662DF7E2069")]
    [DataRow(2, 2048, 2, "91B71D6B40D439954568D412E883DE5186F9381E25AEF36E7A4607722F7E15CA")]
    [DataRow(3, 3072, 5, "48CF8B092FBCE4359D9871ABF74F98E25B6163379EAA15CD9087E800C6D1C55C")]
    [DataRow(4, 4096, 5, "4EE95187682BCB230AD26A95205F6920E84708F6251B3894329B09EC23919E33")]
    [DataRow(5, 6144, 5, "D1BFE6D0925CE7E4DA262B62861514A7755E35831E429F343E7B864848657EFD")]
    [DataRow(6, 8192, 19, "39AB4FEAB950A3128FB71ACCB9FC3965D857012E081998A85996E3EA8B3C3BCF")]
    public void Rfc5054Groups_EachGroup_IsAppendixAsPrimeAndGenerator(int index, int bits, int generator, string primeSha256)
    {
        var group = SrpGroup.Rfc5054Groups[index];

        Assert.AreEqual(bits, (int)group.Prime.GetBitLength());
        Assert.AreEqual(bits / 8, group.PrimeLength);
        Assert.AreEqual(new BigInteger(generator), group.Generator);
        Assert.AreEqual(primeSha256, Convert.ToHexString(SHA256.HashData(Srp6a.ToUnsignedBigEndian(group.Prime))));
        Assert.IsTrue(group.IsRfc5054Group);
    }

    [TestMethod]
    public void Rfc5054Groups_NamedMembers_AreInOrder()
    {
        SrpGroup[] expected =
        [
            SrpGroup.Rfc5054Group1024, SrpGroup.Rfc5054Group1536, SrpGroup.Rfc5054Group2048, SrpGroup.Rfc5054Group3072,
            SrpGroup.Rfc5054Group4096, SrpGroup.Rfc5054Group6144, SrpGroup.Rfc5054Group8192,
        ];

        CollectionAssert.AreEqual(expected, SrpGroup.Rfc5054Groups.ToArray());
    }

    [TestMethod]
    public void IsRfc5054Group_OtherPrime_IsFalse()
    {
        var group = new SrpGroup(23, 5);

        Assert.IsFalse(group.IsRfc5054Group);
        Assert.AreEqual(1, group.PrimeLength);
    }

    [TestMethod]
    public void IsRfc5054Group_AppendixAPrimeWithAnotherGenerator_IsFalse()
    {
        var group = new SrpGroup(SrpGroup.Rfc5054Group1024.Prime, 5);

        Assert.IsFalse(group.IsRfc5054Group);
    }

    [TestMethod]
    [DataRow(1, 2)]
    [DataRow(23, 1)]
    [DataRow(23, 23)]
    public void Constructor_ValueOutOfRange_Throws(int prime, int generator)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SrpGroup(prime, generator));
    }
}
