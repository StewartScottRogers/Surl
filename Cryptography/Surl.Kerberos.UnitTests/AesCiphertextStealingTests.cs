using System.Text;

namespace Surl.Kerberos;

/// <summary>
/// Pins <see cref="AesCiphertextStealing" /> to the CBC-with-ciphertext-stealing vectors of
/// RFC 3962 appendix B, never to values the code under test computed.
/// </summary>
[TestClass]
public sealed class AesCiphertextStealingTests
{
    // RFC 3962, appendix B: AES 128-bit key "chicken teriyaki", initial vector all zero.
    private static readonly byte[] ChickenTeriyaki = Encoding.ASCII.GetBytes("chicken teriyaki");

    // RFC 3962, appendix B, the six CTS vectors: input and output.
    [TestMethod]
    [DataRow(
        "4920776f756c64206c696b652074686520",
        "c6353568f2bf8cb4d8a580362da7ff7f97",
        DisplayName = "17 bytes: one block and one byte")]
    [DataRow(
        "4920776f756c64206c696b65207468652047656e6572616c20476175277320",
        "fc00783e0efdb2c1d445d4c8eff7ed2297687268d6ecccc0c07b25e25ecfe5",
        DisplayName = "31 bytes: one block and fifteen bytes")]
    [DataRow(
        "4920776f756c64206c696b65207468652047656e6572616c2047617527732043",
        "39312523a78662d5be7fcbcc98ebf5a897687268d6ecccc0c07b25e25ecfe584",
        DisplayName = "32 bytes: two whole blocks, swapped")]
    [DataRow(
        "4920776f756c64206c696b65207468652047656e6572616c20476175277320436869636b656e2c20706c656173652c",
        "97687268d6ecccc0c07b25e25ecfe584b3fffd940c16a18c1b5549d2f838029e39312523a78662d5be7fcbcc98ebf5",
        DisplayName = "47 bytes: two blocks and fifteen bytes")]
    [DataRow(
        "4920776f756c64206c696b65207468652047656e6572616c20476175277320436869636b656e2c20706c656173652c20",
        "97687268d6ecccc0c07b25e25ecfe5849dad8bbb96c4cdc03bc103e1a194bbd839312523a78662d5be7fcbcc98ebf5a8",
        DisplayName = "48 bytes: three whole blocks")]
    [DataRow(
        "4920776f756c64206c696b65207468652047656e6572616c20476175277320436869636b656e2c20706c656173652c20616e6420776f6e746f6e20736f75702e",
        "97687268d6ecccc0c07b25e25ecfe58439312523a78662d5be7fcbcc98ebf5a84807efe836ee89a526730dbc2f7bc8409dad8bbb96c4cdc03bc103e1a194bbd8",
        DisplayName = "64 bytes: four whole blocks")]
    public void Encrypt_Rfc3962AppendixB_GivesTheRfcOutputAndDecryptsBack(string input, string output)
    {
        byte[] plainText = Convert.FromHexString(input);

        byte[] cipherText = AesCiphertextStealing.Encrypt(ChickenTeriyaki, plainText);

        Assert.AreEqual(output, Convert.ToHexStringLower(cipherText));
        CollectionAssert.AreEqual(plainText, AesCiphertextStealing.Decrypt(ChickenTeriyaki, cipherText));
    }

    [TestMethod]
    public void EncryptAndDecrypt_OneWholeBlock_IsPlainAesAndRoundTrips()
    {
        byte[] block = Encoding.ASCII.GetBytes("I would like the");

        byte[] cipherText = AesCiphertextStealing.Encrypt(ChickenTeriyaki, block);

        CollectionAssert.AreEqual(AesCiphertextStealing.EncryptBlock(ChickenTeriyaki, block), cipherText);
        CollectionAssert.AreEqual(block, AesCiphertextStealing.Decrypt(ChickenTeriyaki, cipherText));
    }

    [TestMethod]
    public void Encrypt_LessThanOneBlock_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => AesCiphertextStealing.Encrypt(ChickenTeriyaki, new byte[15]));

        Assert.AreEqual("plainText", exception.ParamName);
    }

    [TestMethod]
    public void Decrypt_LessThanOneBlock_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => AesCiphertextStealing.Decrypt(ChickenTeriyaki, new byte[15]));

        Assert.AreEqual("cipherText", exception.ParamName);
    }
}
