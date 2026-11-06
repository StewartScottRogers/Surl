namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshBlockCbcTests
{
    // Eric Young's Blowfish CBC vector from Schneier's vectors-2.txt
    // (https://www.schneier.com/wp-content/uploads/2015/12/vectors-2.txt): a 16-byte key, the IV,
    // "7654321 Now is the time for " with its terminating NUL, padded with zeros to 32 bytes.
    private const string BlowfishKey = "0123456789abcdeff0e1d2c3b4a59687";
    private const string BlowfishInitializationVector = "fedcba9876543210";
    private const string BlowfishPlaintext = "37363534333231204e6f77206973207468652074696d6520666f722000000000";
    private const string BlowfishCiphertext = "6b77b4d63006dee605b156e27403979358deb9e7154616d959f1652bd5ff92cc";

    [TestMethod]
    public void Blowfish_EncryptVectors2CbcVector_GivesItsCiphertextWholeOrBlockByBlock()
    {
        var whole = NewBlowfish();
        var blockByBlock = NewBlowfish();
        var plaintext = Convert.FromHexString(BlowfishPlaintext);

        var encrypted = whole.Encrypt(plaintext);
        var encryptedBlocks = plaintext.Chunk(8).SelectMany(block => blockByBlock.Encrypt(block)).ToArray();

        Assert.AreEqual(BlowfishCiphertext, Convert.ToHexStringLower(encrypted));
        Assert.AreEqual(BlowfishCiphertext, Convert.ToHexStringLower(encryptedBlocks), "The chaining block carries from one call to the next.");
        Assert.AreEqual(8, whole.BlockSize);
    }

    [TestMethod]
    public void Blowfish_DecryptVectors2CbcVector_GivesItsPlaintextWholeOrBlockByBlock()
    {
        var whole = NewBlowfish();
        var blockByBlock = NewBlowfish();
        var ciphertext = Convert.FromHexString(BlowfishCiphertext);

        var decrypted = whole.Decrypt(ciphertext);
        var decryptedBlocks = ciphertext.Chunk(8).SelectMany(block => blockByBlock.Decrypt(block)).ToArray();

        Assert.AreEqual(BlowfishPlaintext, Convert.ToHexStringLower(decrypted));
        Assert.AreEqual(BlowfishPlaintext, Convert.ToHexStringLower(decryptedBlocks));
    }

    [TestMethod]
    public void Cast128_EncryptThenDecryptAcrossSeveralCalls_RoundTrips()
    {
        var key = Enumerable.Range(1, 16).Select(value => (byte)(value * 11)).ToArray();
        var iv = Enumerable.Range(1, 8).Select(value => (byte)value).ToArray();
        var plaintext = Enumerable.Range(0, 48).Select(value => (byte)value).ToArray();
        var encrypting = SshBlockCbc.Cast128(key, iv);
        var decrypting = SshBlockCbc.Cast128(key, iv);

        var ciphertext = plaintext.Chunk(16).SelectMany(part => encrypting.Encrypt(part)).ToArray();
        var decrypted = ciphertext.Chunk(24).SelectMany(part => decrypting.Decrypt(part)).ToArray();

        Assert.AreEqual(8, encrypting.BlockSize);
        CollectionAssert.AreNotEqual(plaintext, ciphertext);
        CollectionAssert.AreEqual(ciphertext, SshBlockCbc.Cast128(key, iv).Encrypt(plaintext), "Calls chain as one run would.");
        CollectionAssert.AreEqual(plaintext, decrypted);
    }

    [TestMethod]
    public void EncryptAndDecrypt_NoBytes_LeaveTheChainAsItIs()
    {
        var cipher = NewBlowfish();

        Assert.IsEmpty(cipher.Encrypt([]));
        Assert.IsEmpty(cipher.Decrypt([]));

        Assert.AreEqual(BlowfishCiphertext[..16], Convert.ToHexStringLower(cipher.Encrypt(Convert.FromHexString(BlowfishPlaintext)[..8])));
    }

    private static SshBlockCbc NewBlowfish() =>
        SshBlockCbc.Blowfish(Convert.FromHexString(BlowfishKey), Convert.FromHexString(BlowfishInitializationVector));
}
