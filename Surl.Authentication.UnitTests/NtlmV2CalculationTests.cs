namespace Surl.Authentication;

/// <summary>
/// <see cref="NtlmV2Calculation"/> against [MS-NLMP] section 4.2.4, the specification's NTLMv2
/// example: user <c>User</c>, domain <c>Domain</c>, password <c>Password</c>, server challenge
/// <c>0123456789abcdef</c>, client challenge <c>aaaaaaaaaaaaaaaa</c> and a zero timestamp.
/// </summary>
[TestClass]
public sealed class NtlmV2CalculationTests
{
    private static readonly byte[] ServerChallenge = Convert.FromHexString("0123456789ABCDEF");

    private static byte[] ResponseKeyNt() =>
        NtlmV2Calculation.ComputeResponseKeyNt(NtlmV2Calculation.ComputeNtHash("Password"), "User", "Domain");

    [TestMethod]
    public void ComputeNtHash_SpecificationPassword_IsSection4_2_2_1_2sNtowfv1()
    {
        CollectionAssert.AreEqual(
            Convert.FromHexString("A4F49C406510BDCAB6824EE7C30FD852"), NtlmV2Calculation.ComputeNtHash("Password"));
    }

    [TestMethod]
    public void ComputeResponseKeyNt_SpecificationInputs_IsSection4_2_4_1_1sNtowfv2()
    {
        CollectionAssert.AreEqual(Convert.FromHexString("0C868A403BFD7A93A3001EF22EF02E3F"), ResponseKeyNt());
    }

    [TestMethod]
    public void ComputeNtProof_SpecificationInputs_IsSection4_2_4sNtProofStr()
    {
        CollectionAssert.AreEqual(
            Convert.FromHexString("68CD0AB851E51C96AABC927BEBEF6A1C"),
            NtlmV2Calculation.ComputeNtProof(ResponseKeyNt(), ServerChallenge, NtlmTestMessages.ClientBlob));
    }

    [TestMethod]
    public void ComputeSessionBaseKey_SpecificationInputs_IsSection4_2_4_1_2sSessionBaseKey()
    {
        var proof = NtlmV2Calculation.ComputeNtProof(ResponseKeyNt(), ServerChallenge, NtlmTestMessages.ClientBlob);

        CollectionAssert.AreEqual(
            Convert.FromHexString("8DE40CCADBC14A82F15CB0AD0DE95CA3"),
            NtlmV2Calculation.ComputeSessionBaseKey(ResponseKeyNt(), proof));
    }

    [TestMethod]
    public void ComputeResponseKeyNt_UserInAnyCase_IsTheSameKeyButTheDomainIsNot()
    {
        var ntHash = NtlmV2Calculation.ComputeNtHash("Password");

        CollectionAssert.AreEqual(
            NtlmV2Calculation.ComputeResponseKeyNt(ntHash, "user", "Domain"),
            NtlmV2Calculation.ComputeResponseKeyNt(ntHash, "USER", "Domain"));
        CollectionAssert.AreNotEqual(
            NtlmV2Calculation.ComputeResponseKeyNt(ntHash, "User", "domain"),
            NtlmV2Calculation.ComputeResponseKeyNt(ntHash, "User", "Domain"));
    }
}
