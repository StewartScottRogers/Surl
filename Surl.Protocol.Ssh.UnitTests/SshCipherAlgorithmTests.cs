namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshCipherAlgorithmTests
{
    [TestMethod]
    [DataRow("aes128-ctr", 16, 16, 16)]
    [DataRow("aes192-ctr", 24, 16, 16)]
    [DataRow("aes256-ctr", 32, 16, 16)]
    [DataRow("aes128-cbc", 16, 16, 16)]
    [DataRow("aes192-cbc", 24, 16, 16)]
    [DataRow("aes256-cbc", 32, 16, 16)]
    [DataRow("rijndael-cbc@lysator.liu.se", 32, 16, 16)]
    [DataRow("3des-cbc", 24, 8, 8)]
    [DataRow("arcfour", 16, 0, 8)]
    [DataRow("arcfour128", 16, 0, 8)]
    public void ForName_BuiltCipher_IsKeyedAsRfc4253Says(string name, int keyLength, int ivLength, int blockSize)
    {
        var algorithm = SshCipherAlgorithm.ForName(name)!;

        // Not all zeros: that is a weak DES key, which TripleDES refuses.
        var key = Enumerable.Range(1, algorithm.KeyLength).Select(value => (byte)(value * 7)).ToArray();

        var cipher = algorithm.Create(key, new byte[algorithm.InitializationVectorLength]);

        Assert.AreEqual(keyLength, algorithm.KeyLength);
        Assert.AreEqual(ivLength, algorithm.InitializationVectorLength);
        Assert.AreEqual(blockSize, cipher.BlockSize);
    }

    [TestMethod]
    [DataRow("aes128-gcm@openssh.com", DisplayName = "An AEAD cipher")]
    [DataRow("chacha20-poly1305@openssh.com", DisplayName = "Another AEAD cipher")]
    [DataRow("twofish256-cbc", DisplayName = "A cipher not built")]
    public void ForName_CipherNotUsedWithAnHmac_IsNull(string name) =>
        Assert.IsNull(SshCipherAlgorithm.ForName(name));

    // RFC 4345 section 4: arcfour128 throws the first 1536 keystream bytes away; arcfour does not.
    [TestMethod]
    public void Arcfour128_KeyStream_IsArcfoursFrom1536BytesOn()
    {
        var key = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        var arcfour = SshCipherAlgorithm.ForName("arcfour")!.Create(key, []);
        var arcfour128 = SshCipherAlgorithm.ForName("arcfour128")!.Create(key, []);

        var skipped = arcfour.Encrypt(new byte[1536]);
        var keyStream = arcfour.Encrypt(new byte[16]);

        Assert.HasCount(1536, skipped);
        CollectionAssert.AreEqual(keyStream, arcfour128.Encrypt(new byte[16]));
    }

    [TestMethod]
    public void Arcfour128_DecryptOfEncrypt_IsThePlaintext()
    {
        var key = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        var plaintext = Enumerable.Range(0, 40).Select(value => (byte)value).ToArray();

        var ciphertext = SshCipherAlgorithm.ForName("arcfour128")!.Create(key, []).Encrypt(plaintext);

        CollectionAssert.AreEqual(plaintext, SshCipherAlgorithm.ForName("arcfour128")!.Create(key, []).Decrypt(ciphertext));
    }
}
