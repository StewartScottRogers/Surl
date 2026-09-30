namespace Surl.Cryptography.Cast128;

/// <summary>
/// Pins <see cref="Cast128" /> to published vectors only (ADR-0003): RFC 2144 Appendix B, the
/// B.1 single plaintext-key-ciphertext sets for 128-, 80- and 40-bit keys and the B.2 full
/// maintenance test, and checks its argument and disposal rules.
/// </summary>
[TestClass]
public sealed class Cast128Tests
{
    // RFC 2144 Appendix B.1, plaintext of every set.
    private const string Plaintext = "0123456789ABCDEF";

    // RFC 2144 Appendix B.1, "128-bit key"; also B.2's initial a and b.
    private const string Key128 = "0123456712345678234567893456789A";

    // RFC 2144 Appendix B.1: key, plaintext 0123456789ABCDEF, ciphertext.
    [TestMethod]
    [DataRow(Key128, "238B4FE5847E44B2")]
    [DataRow("01234567123456782345", "EB6A711A2C02271B")]
    [DataRow("0123456712", "7AC816D16E9B302E")]
    public void EncryptBlockAndDecryptBlock_Rfc2144AppendixB1Set_GiveTheListedCiphertextAndThePlaintextBack(
        string key, string ciphertext)
    {
        using var cast = new Cast128(Convert.FromHexString(key));
        var encrypted = new byte[Cast128.BlockSize];
        var decrypted = new byte[Cast128.BlockSize];

        cast.EncryptBlock(Convert.FromHexString(Plaintext), encrypted);
        cast.DecryptBlock(encrypted, decrypted);

        Assert.AreEqual(ciphertext, Convert.ToHexString(encrypted));
        Assert.AreEqual(Plaintext, Convert.ToHexString(decrypted));
    }

    // RFC 2144 Appendix B.1 lists the 80-bit key also zero-padded to 128 bits, but section 2.5
    // gives a 16-byte key 16 rounds, so the padded form is a different cipher.
    [TestMethod]
    public void EncryptBlock_EightyBitKeyPassedZeroPaddedTo16Bytes_RunsSixteenRoundsAndDiffers()
    {
        using var cast = new Cast128(Convert.FromHexString("01234567123456782345000000000000"));
        var encrypted = new byte[Cast128.BlockSize];

        cast.EncryptBlock(Convert.FromHexString(Plaintext), encrypted);

        Assert.AreNotEqual("EB6A711A2C02271B", Convert.ToHexString(encrypted));
    }

    // RFC 2144 Appendix B.2: a = b = 0123...789A; 1,000,000 times aL, aR under key b, then bL, bR
    // under key a; "verify a == EE A9 D0 A2 ..." and "verify b == B2 C9 5E B0 ...". It takes
    // about 21 seconds, so it is Integration; the B.1 sets cover every branch it covers.
    [TestMethod]
    [TestCategory("Integration")]
    public void EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest_GivesTheListedAAndB()
    {
        var a = Convert.FromHexString(Key128);
        var b = Convert.FromHexString(Key128);

        for (var iteration = 0; iteration < 1_000_000; iteration++)
        {
            using (var underB = new Cast128(b))
            {
                underB.EncryptBlock(a.AsSpan(0, 8), a.AsSpan(0, 8));
                underB.EncryptBlock(a.AsSpan(8), a.AsSpan(8));
            }

            using var underA = new Cast128(a);
            underA.EncryptBlock(b.AsSpan(0, 8), b.AsSpan(0, 8));
            underA.EncryptBlock(b.AsSpan(8), b.AsSpan(8));
        }

        Assert.AreEqual("EEA9D0A249FD3BA6B3436FB89D6DCA92", Convert.ToHexString(a));
        Assert.AreEqual("B2C95EB00C31AD7180AC05B8E83D696E", Convert.ToHexString(b));
    }

    // RFC 2144 section 2.5: keys are 40 to 128 bits in 8-bit steps.
    [TestMethod]
    [DataRow(4)]
    [DataRow(17)]
    public void Constructor_KeyOutsideFiveTo16Bytes_ThrowsArgumentException(int length)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => new Cast128(new byte[length]));

        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    [DataRow(5)]
    [DataRow(16)]
    public void Constructor_FiveOr16ByteKey_IsAccepted(int length)
    {
        using var cast = new Cast128(new byte[length]);

        Assert.IsNotNull(cast);
    }

    [TestMethod]
    [DataRow(7, 8, "source")]
    [DataRow(9, 8, "source")]
    [DataRow(8, 7, "destination")]
    [DataRow(8, 9, "destination")]
    public void EncryptBlockAndDecryptBlock_SpanNotEightBytes_ThrowArgumentException(
        int sourceLength, int destinationLength, string parameterName)
    {
        using var cast = new Cast128(new byte[16]);

        var encrypting = Assert.ThrowsExactly<ArgumentException>(
            () => cast.EncryptBlock(new byte[sourceLength], new byte[destinationLength]));
        var decrypting = Assert.ThrowsExactly<ArgumentException>(
            () => cast.DecryptBlock(new byte[sourceLength], new byte[destinationLength]));

        Assert.AreEqual(parameterName, encrypting.ParamName);
        Assert.AreEqual(parameterName, decrypting.ParamName);
    }

    [TestMethod]
    public void EncryptBlockAndDecryptBlock_AfterDispose_ThrowObjectDisposedException()
    {
        var cast = new Cast128(new byte[16]);
        cast.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => cast.EncryptBlock(new byte[8], new byte[8]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => cast.DecryptBlock(new byte[8], new byte[8]));
    }
}
