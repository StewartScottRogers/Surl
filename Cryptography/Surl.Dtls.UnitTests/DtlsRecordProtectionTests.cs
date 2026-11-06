using System.Security.Cryptography;
using System.Text;

namespace Surl.Dtls;

/// <summary>
/// Each suite's record is checked against a vector built in the test from the RFCs' layout,
/// written out byte by byte: the nonce is the salt then epoch and sequence number (RFC 5288
/// section 3, RFC 6655 section 3), and the additional data is epoch and sequence number, type,
/// version and plaintext length (RFC 5246 section 6.2.3.3 with RFC 6347 section 4.1.2.1).
/// </summary>
[TestClass]
public sealed class DtlsRecordProtectionTests
{
    private static readonly byte[] Key128 = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");
    private static readonly byte[] Key256 = Convert.FromHexString(
        "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F");
    private static readonly byte[] Salt = Convert.FromHexString("A0A1A2A3");
    private static readonly byte[] Plaintext = Encoding.ASCII.GetBytes("hello");

    // Epoch 1, sequence number 5, application_data, DTLS 1.2.
    private static readonly DtlsRecordHeader Header =
        new(DtlsContentType.ApplicationData, 0xFEFD, Epoch: 1, SequenceNumber: 5, Length: 0);

    private static readonly byte[] ExpectedExplicitNonce = Convert.FromHexString("0001000000000005");
    private static readonly byte[] ExpectedNonce = Convert.FromHexString("A0A1A2A3" + "0001000000000005");
    private static readonly byte[] ExpectedAdditionalData = Convert.FromHexString("0001000000000005" + "17" + "FEFD" + "0005");

    [TestMethod]
    public void Seal_Aes128Gcm_BuildsTheNonceAndAdditionalDataTheRfcsDescribe()
    {
        using var protection = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key128, Salt);
        using var gcm = new AesGcm(Key128, 16);
        var expected = ExpectedFragment(16, (ciphertext, tag) =>
            gcm.Encrypt(ExpectedNonce, Plaintext, ciphertext, tag, ExpectedAdditionalData));

        var fragment = protection.Seal(Header, Plaintext);

        CollectionAssert.AreEqual(expected, fragment);
    }

    [TestMethod]
    public void Seal_Aes256Gcm_BuildsTheNonceAndAdditionalDataTheRfcsDescribe()
    {
        using var protection = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key256, Salt);
        using var gcm = new AesGcm(Key256, 16);
        var expected = ExpectedFragment(16, (ciphertext, tag) =>
            gcm.Encrypt(ExpectedNonce, Plaintext, ciphertext, tag, ExpectedAdditionalData));

        var fragment = protection.Seal(Header, Plaintext);

        CollectionAssert.AreEqual(expected, fragment);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public void Seal_Aes128Ccm_BuildsTheNonceAndAdditionalDataTheRfcsDescribe()
    {
        using var protection = new DtlsRecordProtection(DtlsAeadAlgorithm.AesCcm, Key128, Salt);
        using var ccm = new AesCcm(Key128);
        var expected = ExpectedFragment(16, (ciphertext, tag) =>
            ccm.Encrypt(ExpectedNonce, Plaintext, ciphertext, tag, ExpectedAdditionalData));

        var fragment = protection.Seal(Header, Plaintext);

        CollectionAssert.AreEqual(expected, fragment);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public void Seal_Aes128Ccm8_BuildsTheNonceAndAdditionalDataTheRfcsDescribeWithAnEightByteTag()
    {
        using var protection = new DtlsRecordProtection(DtlsAeadAlgorithm.AesCcm8, Key128, Salt);
        using var ccm = new AesCcm(Key128);
        var expected = ExpectedFragment(8, (ciphertext, tag) =>
            ccm.Encrypt(ExpectedNonce, Plaintext, ciphertext, tag, ExpectedAdditionalData));

        var fragment = protection.Seal(Header, Plaintext);

        Assert.AreEqual(16, protection.Overhead);
        CollectionAssert.AreEqual(expected, fragment);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow(DtlsAeadAlgorithm.AesGcm)]
    [DataRow(DtlsAeadAlgorithm.AesCcm)]
    [DataRow(DtlsAeadAlgorithm.AesCcm8)]
    public void TryOpen_SealedFragment_ReturnsThePlaintext(DtlsAeadAlgorithm algorithm)
    {
        using var protection = new DtlsRecordProtection(algorithm, Key128, Salt);
        var fragment = protection.Seal(Header, Plaintext);

        var opened = protection.TryOpen(Header, fragment, out var plaintext);

        Assert.IsTrue(opened);
        CollectionAssert.AreEqual(Plaintext, plaintext);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow(DtlsAeadAlgorithm.AesGcm)]
    [DataRow(DtlsAeadAlgorithm.AesCcm8)]
    public void TryOpen_CorruptedTag_Fails(DtlsAeadAlgorithm algorithm)
    {
        using var protection = new DtlsRecordProtection(algorithm, Key128, Salt);
        var fragment = protection.Seal(Header, Plaintext);
        fragment[^1] ^= 0x01;

        var opened = protection.TryOpen(Header, fragment, out var plaintext);

        Assert.IsFalse(opened);
        Assert.IsEmpty(plaintext);
    }

    [TestMethod]
    public void TryOpen_HeaderOfAnotherSequenceNumber_FailsBecauseTheAdditionalDataDiffers()
    {
        using var protection = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key128, Salt);
        var fragment = protection.Seal(Header, Plaintext);

        var opened = protection.TryOpen(Header with { SequenceNumber = 6 }, fragment, out _);

        Assert.IsFalse(opened);
    }

    [TestMethod]
    public void TryOpen_FragmentShorterThanNonceAndTag_Fails()
    {
        using var protection = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key128, Salt);

        var opened = protection.TryOpen(Header, new byte[23], out var plaintext);

        Assert.IsFalse(opened);
        Assert.IsEmpty(plaintext);
    }

    [TestMethod]
    public void Constructor_SaltNotFourBytes_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key128, new byte[3]));
    }

    [TestMethod]
    public void Constructor_UnknownAlgorithm_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new DtlsRecordProtection((DtlsAeadAlgorithm)99, Key128, Salt));
    }

    private static byte[] ExpectedFragment(int tagSize, Action<byte[], byte[]> encrypt)
    {
        var ciphertext = new byte[Plaintext.Length];
        var tag = new byte[tagSize];
        encrypt(ciphertext, tag);
        return [.. ExpectedExplicitNonce, .. ciphertext, .. tag];
    }
}
