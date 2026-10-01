namespace Surl.Kerberos;

/// <summary>
/// Pins <see cref="GssApiChecksum" />, the RFC 4121 section 4.1.1 authenticator checksum: type
/// <c>0x8003</c>, at least 24 bytes, <c>Lgth</c> 16, and a delegated credential that fits.
/// </summary>
[TestClass]
public sealed class GssApiChecksumTests
{
    [TestMethod]
    public void IsWellFormed_PlainChecksum_IsTrue()
    {
        Assert.IsTrue(GssApiChecksum.IsWellFormed(0x8003, ApRequestBuilder.GssApiChecksumBytes(0x3E)));
    }

    [TestMethod]
    public void ReadFlags_GivesTheLittleEndianFlagsAfterTheBindings()
    {
        Assert.AreEqual(0x1234003EU, GssApiChecksum.ReadFlags(ApRequestBuilder.GssApiChecksumBytes(0x1234003E)));
    }

    [TestMethod]
    public void IsWellFormed_LgthNot16_IsFalse()
    {
        byte[] checksum = ApRequestBuilder.GssApiChecksumBytes(0x3E);
        checksum[0] = 15;

        Assert.IsFalse(GssApiChecksum.IsWellFormed(0x8003, checksum));
    }

    [TestMethod]
    public void IsWellFormed_DelegatedCredentialThatFits_IsTrue()
    {
        byte[] checksum = [.. ApRequestBuilder.GssApiChecksumBytes(0x3F), 0x01, 0x00, 0x03, 0x00, 0xAA, 0xBB, 0xCC];

        Assert.IsTrue(GssApiChecksum.IsWellFormed(0x8003, checksum));
    }

    [TestMethod]
    [DataRow(new byte[] { }, DisplayName = "no DlgOpt or Dlgth")]
    [DataRow(new byte[] { 0x01, 0x00 }, DisplayName = "DlgOpt only")]
    [DataRow(new byte[] { 0x01, 0x00, 0x04, 0x00, 0xAA, 0xBB, 0xCC }, DisplayName = "credential one byte short")]
    public void IsWellFormed_DelegationFlagWithoutACredentialThatFits_IsFalse(byte[] delegation)
    {
        byte[] checksum = [.. ApRequestBuilder.GssApiChecksumBytes(0x3F), .. delegation];

        Assert.IsFalse(GssApiChecksum.IsWellFormed(0x8003, checksum));
    }
}
