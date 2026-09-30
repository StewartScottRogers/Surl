using System.Text;
using Surl.Cryptography;

namespace Surl.Authentication;

/// <summary>
/// <see cref="NtlmV1Calculation"/> against [MS-NLMP] section 4.2.2, the specification's NTLMv1
/// example (password <c>Password</c>, server challenge <c>0123456789abcdef</c>), and against the
/// password handling of upstream curl's <c>lib/curl_ntlm_core.c</c> at <c>curl-8_21_0</c>.
/// </summary>
[TestClass]
public sealed class NtlmV1CalculationTests
{
    private static readonly byte[] ServerChallenge = Convert.FromHexString("0123456789ABCDEF");

    [TestMethod]
    public void ComputeLmHash_SpecificationPassword_IsSection4_2_2_1_1sLmowfv1()
    {
        CollectionAssert.AreEqual(
            Convert.FromHexString("E52CAC67419A9A224A3B108F3FA6CB6D"), NtlmV1Calculation.ComputeLmHash("Password"));
    }

    [TestMethod]
    public void ComputeNtHashOfWidenedUtf8_SpecificationPassword_IsSection4_2_2_1_2sNtowfv1()
    {
        CollectionAssert.AreEqual(
            Convert.FromHexString("A4F49C406510BDCAB6824EE7C30FD852"),
            NtlmV1Calculation.ComputeNtHashOfWidenedUtf8("Password"));
    }

    [TestMethod]
    public void ComputeResponse_LmHashOfSpecificationPassword_IsSection4_2_2_2_2sLmv1Response()
    {
        CollectionAssert.AreEqual(
            Convert.FromHexString("98DEF7B87F88AA5DAFE2DF779688A172DEF11C7D5CCDEF13"),
            NtlmV1Calculation.ComputeResponse(NtlmV1Calculation.ComputeLmHash("Password"), ServerChallenge));
    }

    [TestMethod]
    public void ComputeResponse_NtHashOfSpecificationPassword_IsSection4_2_2_2_1sNtlmv1Response()
    {
        CollectionAssert.AreEqual(
            Convert.FromHexString("67C43011F30298A2AD35ECE64F16331C44BDBED927841F94"),
            NtlmV1Calculation.ComputeResponse(
                NtlmV1Calculation.ComputeNtHashOfWidenedUtf8("Password"), ServerChallenge));
    }

    // The empty password's hashes are the well-known "no password" values: LMOWFv1 of fourteen
    // zero bytes, and RFC 1320's MD4 of the empty message.
    [TestMethod]
    public void ComputeLmHash_EmptyPassword_IsTheWellKnownEmptyLmHash()
    {
        CollectionAssert.AreEqual(
            Convert.FromHexString("AAD3B435B51404EEAAD3B435B51404EE"), NtlmV1Calculation.ComputeLmHash(string.Empty));
    }

    [TestMethod]
    public void ComputeNtHashOfWidenedUtf8_EmptyPassword_IsMd4OfNothing()
    {
        CollectionAssert.AreEqual(
            Convert.FromHexString("31D6CFE0D16AE931B73C59D7E0C089C0"),
            NtlmV1Calculation.ComputeNtHashOfWidenedUtf8(string.Empty));
    }

    [TestMethod]
    public void ComputeResponse_EmptyPasswordHashes_AreTwentyFourBytesEach()
    {
        Assert.HasCount(
            NtlmV1Calculation.ResponseLength,
            NtlmV1Calculation.ComputeResponse(NtlmV1Calculation.ComputeLmHash(string.Empty), ServerChallenge));
        Assert.HasCount(
            NtlmV1Calculation.ResponseLength,
            NtlmV1Calculation.ComputeResponse(
                NtlmV1Calculation.ComputeNtHashOfWidenedUtf8(string.Empty), ServerChallenge));
    }

    // Curl_ntlm_core_mk_lm_hash takes CURLMIN(strlen(password), 14) bytes: a longer password is
    // cut, so its LM hash is that of its first 14 bytes, while the NT hash still sees all of it.
    [TestMethod]
    public void ComputeLmHash_PasswordOverFourteenBytes_IsTheHashOfItsFirstFourteen()
    {
        CollectionAssert.AreEqual(
            NtlmV1Calculation.ComputeLmHash("Password123456"), NtlmV1Calculation.ComputeLmHash("Password1234567890"));
        CollectionAssert.AreNotEqual(
            NtlmV1Calculation.ComputeNtHashOfWidenedUtf8("Password123456"),
            NtlmV1Calculation.ComputeNtHashOfWidenedUtf8("Password1234567890"));
    }

    [TestMethod]
    public void ComputeLmHash_FourteenBytePassword_UsesEveryByte()
    {
        CollectionAssert.AreNotEqual(
            NtlmV1Calculation.ComputeLmHash("Password123456"), NtlmV1Calculation.ComputeLmHash("Password123457"));
        CollectionAssert.AreNotEqual(
            NtlmV1Calculation.ComputeLmHash("Password123456"), NtlmV1Calculation.ComputeLmHash("Password12345"));
    }

    [TestMethod]
    public void ComputeLmHash_AnyCaseOfAsciiLetters_IsTheSameHash()
    {
        CollectionAssert.AreEqual(NtlmV1Calculation.ComputeLmHash("PASSWORD"), NtlmV1Calculation.ComputeLmHash("password"));
    }

    // Curl_strntoupper upper-cases through a plain-ASCII table, so a non-ASCII password's UTF-8
    // bytes stay as they are: "ä" (C3 A4) and "Ä" (C3 84) are different LM passwords.
    [TestMethod]
    public void ComputeLmHash_NonAsciiPassword_UpperCasesOnlyTheAsciiLetters()
    {
        CollectionAssert.AreEqual(NtlmV1Calculation.ComputeLmHash("PäSSWORD"), NtlmV1Calculation.ComputeLmHash("pässword"));
        CollectionAssert.AreNotEqual(NtlmV1Calculation.ComputeLmHash("PÄSSWORD"), NtlmV1Calculation.ComputeLmHash("pässword"));
    }

    // ascii_to_unicode_le widens each byte of the password as given to curl (UTF-8), so "é"
    // (C3 A9) is hashed as C3 00 A9 00, not as its UTF-16LE E9 00.
    [TestMethod]
    public void ComputeNtHashOfWidenedUtf8_NonAsciiPassword_HashesEachUtf8ByteWidened()
    {
        CollectionAssert.AreEqual(
            Md4.HashData([0xC3, 0x00, 0xA9, 0x00]), NtlmV1Calculation.ComputeNtHashOfWidenedUtf8("é"));
        CollectionAssert.AreNotEqual(
            Md4.HashData(Encoding.Unicode.GetBytes("é")), NtlmV1Calculation.ComputeNtHashOfWidenedUtf8("é"));
    }
}
