using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshAesCtrTests
{
    // NIST SP 800-38A, appendix F.5: the four plaintext blocks and the initial counter every
    // CTR example shares.
    private const string InitialCounter = "f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff";

    private const string Plaintext =
        "6bc1bee22e409f96e93d7e117393172a"
        + "ae2d8a571e03ac9c9eb76fac45af8e51"
        + "30c81c46a35ce411e5fbc1191a0a52ef"
        + "f69f2445df4f9b17ad2b417be66c3710";

    [TestMethod]
    [DataRow(
        "2b7e151628aed2a6abf7158809cf4f3c",
        "874d6191b620e3261bef6864990db6ce9806f66b7970fdff8617187bb9fffdff5ae4df3edbd5d35e5b4f09020db03eab1e031dda2fbe03d1792170a0f3009cee",
        DisplayName = "SP 800-38A F.5.1 CTR-AES128.Encrypt")]
    [DataRow(
        "8e73b0f7da0e6452c810f32b809079e562f8ead2522c6b7b",
        "1abc932417521ca24f2b0459fe7e6e0b090339ec0aa6faefd5ccc2c6f4ce8e941e36b26bd1ebc670d1bd1d665620abf74f78a7f6d29809585a97daec58c6b050",
        DisplayName = "SP 800-38A F.5.3 CTR-AES192.Encrypt")]
    [DataRow(
        "603deb1015ca71be2b73aef0857d77811f352c073b6108d72d9810a30914dff4",
        "601ec313775789a5b7a7f504bbf3d228f443e3ca4d62b59aca84e990cacaf5c52b0930daa23de94ce87017ba2d84988ddfc9c58db67aada613c2dd08457941a6",
        DisplayName = "SP 800-38A F.5.5 CTR-AES256.Encrypt")]
    public void Transform_Sp800_38aVector_GivesItsCiphertextInOneCallOrBlockByBlock(string key, string ciphertext)
    {
        var whole = new SshAesCtr(Convert.FromHexString(key), Convert.FromHexString(InitialCounter));
        var blockByBlock = new SshAesCtr(Convert.FromHexString(key), Convert.FromHexString(InitialCounter));
        var plaintext = Convert.FromHexString(Plaintext);

        var encrypted = whole.Transform(plaintext);
        var encryptedBlocks = plaintext.Chunk(16).SelectMany(block => blockByBlock.Transform(block)).ToArray();

        Assert.AreEqual(ciphertext, Convert.ToHexStringLower(encrypted));
        Assert.AreEqual(ciphertext, Convert.ToHexStringLower(encryptedBlocks));
    }

    [TestMethod]
    public void Transform_Ciphertext_DecryptsToThePlaintext()
    {
        var key = Convert.FromHexString("2b7e151628aed2a6abf7158809cf4f3c");
        var ciphertext = new SshAesCtr(key, Convert.FromHexString(InitialCounter)).Transform(Convert.FromHexString(Plaintext));

        var decrypted = new SshAesCtr(key, Convert.FromHexString(InitialCounter)).Transform(ciphertext);

        Assert.AreEqual(Plaintext, Convert.ToHexStringLower(decrypted));
    }

    [TestMethod]
    public void Transform_CounterAtItsLargestValue_WrapsToZero()
    {
        var key = new byte[16];
        var largest = Enumerable.Repeat((byte)0xFF, 16).ToArray();
        using var aes = Aes.Create();
        aes.Key = key;
        var expected = aes.EncryptEcb([.. largest, .. new byte[16]], PaddingMode.None);

        var keyStream = new SshAesCtr(key, largest).Transform(new byte[32]);

        CollectionAssert.AreEqual(expected, keyStream);
    }
}
