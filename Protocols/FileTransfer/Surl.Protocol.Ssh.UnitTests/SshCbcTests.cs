namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshCbcTests
{
    // NIST SP 800-38A, appendix F.2: the IV and the four plaintext blocks every CBC example shares.
    private const string InitializationVector = "000102030405060708090a0b0c0d0e0f";

    private const string Plaintext =
        "6bc1bee22e409f96e93d7e117393172a"
        + "ae2d8a571e03ac9c9eb76fac45af8e51"
        + "30c81c46a35ce411e5fbc1191a0a52ef"
        + "f69f2445df4f9b17ad2b417be66c3710";

    public static IEnumerable<object[]> Sp800_38aCbcVectors =>
    [
        [
            "SP 800-38A F.2.1 CBC-AES128.Encrypt",
            "2b7e151628aed2a6abf7158809cf4f3c",
            "7649abac8119b246cee98e9b12e9197d5086cb9b507219ee95db113a917678b273bed6b8e3c1743b7116e69e222295163ff1caa1681fac09120eca307586e1a7",
        ],
        [
            "SP 800-38A F.2.3 CBC-AES192.Encrypt",
            "8e73b0f7da0e6452c810f32b809079e562f8ead2522c6b7b",
            "4f021db243bc633d7178183a9fa071e8b4d9ada9ad7dedf4e5e738763f69145a571b242012fb7ae07fa9baac3df102e008b0e27988598881d920a9e64f5615cd",
        ],
        [
            "SP 800-38A F.2.5 CBC-AES256.Encrypt",
            "603deb1015ca71be2b73aef0857d77811f352c073b6108d72d9810a30914dff4",
            "f58c4c04d6e5f1ba779eabfb5f7bfbd69cfc4e967edb808d679f777bc6702c7d39f23369a9d9bacfa530e26304231461b2eb05e2c39be9fcda6c19078c6a9d1b",
        ],
    ];

    [TestMethod]
    [DynamicData(nameof(Sp800_38aCbcVectors))]
    public void Encrypt_Sp800_38aVector_GivesItsCiphertextWholeOrBlockByBlock(string vector, string key, string expected)
    {
        var whole = SshCbc.Aes(Convert.FromHexString(key), Convert.FromHexString(InitializationVector));
        var blockByBlock = SshCbc.Aes(Convert.FromHexString(key), Convert.FromHexString(InitializationVector));
        var plaintext = Convert.FromHexString(Plaintext);

        var encrypted = whole.Encrypt(plaintext);
        var encryptedBlocks = plaintext.Chunk(16).SelectMany(block => blockByBlock.Encrypt(block)).ToArray();

        Assert.AreEqual(expected, Convert.ToHexStringLower(encrypted), vector);
        Assert.AreEqual(expected, Convert.ToHexStringLower(encryptedBlocks), "The chaining block carries from one call to the next.");
    }

    [TestMethod]
    [DynamicData(nameof(Sp800_38aCbcVectors))]
    public void Decrypt_Sp800_38aVector_GivesItsPlaintextWholeOrBlockByBlock(string vector, string key, string ciphertext)
    {
        var whole = SshCbc.Aes(Convert.FromHexString(key), Convert.FromHexString(InitializationVector));
        var blockByBlock = SshCbc.Aes(Convert.FromHexString(key), Convert.FromHexString(InitializationVector));
        var bytes = Convert.FromHexString(ciphertext);

        var decrypted = whole.Decrypt(bytes);
        var decryptedBlocks = bytes.Chunk(16).SelectMany(block => blockByBlock.Decrypt(block)).ToArray();

        Assert.AreEqual(Plaintext, Convert.ToHexStringLower(decrypted), vector);
        Assert.AreEqual(Plaintext, Convert.ToHexStringLower(decryptedBlocks));
    }

    [TestMethod]
    public void EncryptAndDecrypt_NoBytes_LeaveTheChainAsItIs()
    {
        var key = Convert.FromHexString("2b7e151628aed2a6abf7158809cf4f3c");
        var cipher = SshCbc.Aes(key, Convert.FromHexString(InitializationVector));

        Assert.IsEmpty(cipher.Encrypt([]));
        Assert.IsEmpty(cipher.Decrypt([]));

        Assert.AreEqual("7649abac8119b246cee98e9b12e9197d", Convert.ToHexStringLower(cipher.Encrypt(Convert.FromHexString(Plaintext)[..16])));
    }

    [TestMethod]
    public void TripleDes_EncryptThenDecrypt_RoundTripsInEightByteBlocks()
    {
        var key = Enumerable.Range(1, 24).Select(value => (byte)(value * 7)).ToArray();
        var iv = Enumerable.Range(1, 8).Select(value => (byte)value).ToArray();
        var plaintext = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();

        var ciphertext = SshCbc.TripleDes(key, iv).Encrypt(plaintext);

        Assert.AreEqual(8, SshCbc.TripleDes(key, iv).BlockSize);
        CollectionAssert.AreNotEqual(plaintext, ciphertext);
        CollectionAssert.AreEqual(plaintext, SshCbc.TripleDes(key, iv).Decrypt(ciphertext));
    }
}
